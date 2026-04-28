using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CreaturesReborn.Sim.Genome;

public enum GenePayloadFieldKind
{
    Byte,
    Int16,
    FloatByte,
    Token,
    DerivedInt,
    RawBytes
}

public sealed record GenePayloadField(
    string Name,
    GenePayloadFieldKind Kind,
    int Offset,
    int Length,
    string DisplayValue);

public sealed class EditableGenePayload
{
    public EditableGenePayload(
        GenePayloadKind kind,
        IReadOnlyList<GenePayloadField> fields,
        byte[] rawBytes,
        bool isRawFallback)
    {
        Kind = kind;
        Fields = fields;
        RawBytes = rawBytes.ToArray();
        IsRawFallback = isRawFallback;
    }

    public GenePayloadKind Kind { get; }

    public IReadOnlyList<GenePayloadField> Fields { get; }

    public byte[] RawBytes { get; }

    public bool IsRawFallback { get; }

    public int GetInt(string name)
    {
        GenePayloadField field = Find(name);
        return field.Kind switch
        {
            GenePayloadFieldKind.Byte => RawBytes[field.Offset],
            GenePayloadFieldKind.Int16 => ReadInt16(RawBytes, field.Offset),
            GenePayloadFieldKind.FloatByte => RawBytes[field.Offset],
            GenePayloadFieldKind.DerivedInt => int.Parse(field.DisplayValue),
            _ => throw new InvalidOperationException($"Field '{name}' is not numeric.")
        };
    }

    public string GetString(string name)
    {
        GenePayloadField field = Find(name);
        if (field.Kind != GenePayloadFieldKind.Token)
            return field.DisplayValue;
        return ReadToken(RawBytes, field.Offset);
    }

    private GenePayloadField Find(string name)
        => Fields.FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? throw new KeyNotFoundException($"Payload field '{name}' was not decoded.");

    internal static int ReadInt16(byte[] bytes, int offset)
        => bytes[offset] * 256 + bytes[offset + 1];

    internal static string ReadToken(byte[] bytes, int offset)
        => Encoding.ASCII.GetString(bytes, offset, 4);
}

public enum GeneFieldEditKind
{
    Int,
    String,
    Raw
}

public sealed record GeneFieldEdit(
    string Name,
    GeneFieldEditKind Kind,
    int IntValue = 0,
    string StringValue = "",
    byte[]? RawValue = null)
{
    public static GeneFieldEdit Int(string name, int value)
        => new(name, GeneFieldEditKind.Int, IntValue: value);

    public static GeneFieldEdit String(string name, string value)
        => new(name, GeneFieldEditKind.String, StringValue: value);

    public static GeneFieldEdit Raw(byte[] value)
        => new("raw", GeneFieldEditKind.Raw, RawValue: value.ToArray());
}

public static class GenePayloadCodec
{
    public static EditableGenePayload Decode(GeneRecord record)
    {
        byte[] bytes = record.Payload.Bytes.ToArray();
        var fields = new List<GenePayloadField>();

        return record.Payload.Kind switch
        {
            GenePayloadKind.BrainLobe when bytes.Length >= 23 => DecodeBrainLobe(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BrainOrgan when bytes.Length >= 5 => DecodeOrgan(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BrainTract when bytes.Length >= 29 => DecodeBrainTract(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryReceptor when bytes.Length >= 8 => DecodeReceptor(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryEmitter when bytes.Length >= 8 => DecodeEmitter(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryReaction when bytes.Length >= 9 => DecodeReaction(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryHalfLife when bytes.Length >= 256 => DecodeHalfLife(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryInject when bytes.Length >= 2 => DecodeInject(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryNeuroEmitter when bytes.Length >= 15 => DecodeNeuroEmitter(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreatureStimulus when bytes.Length >= 13 => DecodeStimulus(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreatureGenus when bytes.Length >= 65 => DecodeGenus(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreaturePose when bytes.Length >= 17 => DecodePose(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreatureGait when bytes.Length >= 9 => DecodeGait(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreatureInstinct when bytes.Length >= 9 => DecodeInstinct(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreatureAppearance when bytes.Length >= 3 => DecodeAppearance(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreaturePigment when bytes.Length >= 2 => DecodePigment(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreaturePigmentBleed when bytes.Length >= 2 => DecodePigmentBleed(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreatureExpression when bytes.Length >= 11 => DecodeExpression(record.Payload.Kind, bytes, fields),
            GenePayloadKind.Organ when bytes.Length >= 5 => DecodeOrgan(record.Payload.Kind, bytes, fields),
            _ => RawFallback(record.Payload.Kind, bytes)
        };
    }

    public static byte[] Encode(GeneRecord record, IEnumerable<GeneFieldEdit> edits)
    {
        byte[] bytes = record.Payload.Bytes.ToArray();
        GeneFieldEdit? raw = edits.FirstOrDefault(edit => edit.Kind == GeneFieldEditKind.Raw);
        if (raw?.RawValue != null)
            return raw.RawValue.ToArray();

        EditableGenePayload decoded = Decode(record);
        if (decoded.IsRawFallback)
            return bytes;

        Dictionary<string, GenePayloadField> fields = decoded.Fields.ToDictionary(
            field => field.Name,
            StringComparer.OrdinalIgnoreCase);

        foreach (GeneFieldEdit edit in edits)
        {
            if (!fields.TryGetValue(edit.Name, out GenePayloadField? field))
                continue;

            switch (edit.Kind)
            {
                case GeneFieldEditKind.Int:
                    WriteInt(bytes, field, edit.IntValue);
                    break;
                case GeneFieldEditKind.String:
                    WriteToken(bytes, field, edit.StringValue);
                    break;
            }
        }

        return bytes;
    }

    private static EditableGenePayload DecodeBrainLobe(
        GenePayloadKind kind,
        byte[] bytes,
        List<GenePayloadField> fields)
    {
        AddToken(fields, bytes, "token", 0);
        AddInt16(fields, bytes, "update", 4);
        AddInt16(fields, bytes, "x", 6);
        AddInt16(fields, bytes, "y", 8);
        AddByte(fields, bytes, "width", 10);
        AddByte(fields, bytes, "height", 11);
        AddByte(fields, bytes, "color_r", 12);
        AddByte(fields, bytes, "color_g", 13);
        AddByte(fields, bytes, "color_b", 14);
        AddByte(fields, bytes, "winner_takes_all", 15);
        AddByte(fields, bytes, "tissue", 16);
        AddByte(fields, bytes, "run_init_rule_always", 17);
        if (bytes.Length >= 25)
            AddRaw(fields, bytes, "padding", 18, 7);
        if (bytes.Length >= 73)
            AddRaw(fields, bytes, "init_rule", 25, 48);
        if (bytes.Length >= 121)
            AddRaw(fields, bytes, "update_rule", 73, 48);
        return new EditableGenePayload(kind, fields, bytes, isRawFallback: false);
    }

    private static EditableGenePayload DecodeBrainTract(
        GenePayloadKind kind,
        byte[] bytes,
        List<GenePayloadField> fields)
    {
        AddInt16(fields, bytes, "update", 0);
        AddToken(fields, bytes, "source_lobe", 2);
        AddInt16(fields, bytes, "source_min", 6);
        AddInt16(fields, bytes, "source_max", 8);
        AddInt16(fields, bytes, "source_dendrites_per_neuron", 10);
        AddToken(fields, bytes, "destination_lobe", 12);
        AddInt16(fields, bytes, "destination_min", 16);
        AddInt16(fields, bytes, "destination_max", 18);
        AddInt16(fields, bytes, "destination_dendrites_per_neuron", 20);
        AddByte(fields, bytes, "random_connect_and_migrate", 22);
        AddByte(fields, bytes, "random_dendrite_count", 23);
        AddByte(fields, bytes, "source_growth_sv", 24);
        AddByte(fields, bytes, "destination_growth_sv", 25);
        AddByte(fields, bytes, "run_init_rule_always", 26);
        if (bytes.Length >= 32)
            AddRaw(fields, bytes, "padding", 27, 5);
        if (bytes.Length >= 80)
            AddRaw(fields, bytes, "init_rule", 32, 48);
        if (bytes.Length >= 128)
            AddRaw(fields, bytes, "update_rule", 80, 48);
        return new EditableGenePayload(kind, fields, bytes, isRawFallback: false);
    }

    private static EditableGenePayload DecodeReceptor(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "organ", 0);
        AddByte(fields, bytes, "tissue", 1);
        AddByte(fields, bytes, "locus", 2);
        AddByte(fields, bytes, "chemical", 3);
        AddFloat(fields, bytes, "threshold", 4);
        AddFloat(fields, bytes, "nominal", 5);
        AddFloat(fields, bytes, "gain", 6);
        AddByte(fields, bytes, "effect", 7);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeEmitter(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "organ", 0);
        AddByte(fields, bytes, "tissue", 1);
        AddByte(fields, bytes, "locus", 2);
        AddByte(fields, bytes, "chemical", 3);
        AddFloat(fields, bytes, "threshold", 4);
        AddByte(fields, bytes, "tick_rate", 5);
        AddFloat(fields, bytes, "gain", 6);
        AddByte(fields, bytes, "effect", 7);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeReaction(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "reactant1_amount", 0);
        AddByte(fields, bytes, "reactant1", 1);
        AddByte(fields, bytes, "reactant2_amount", 2);
        AddByte(fields, bytes, "reactant2", 3);
        AddByte(fields, bytes, "product1_amount", 4);
        AddByte(fields, bytes, "product1", 5);
        AddByte(fields, bytes, "product2_amount", 6);
        AddByte(fields, bytes, "product2", 7);
        AddFloat(fields, bytes, "rate", 8);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeHalfLife(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        for (int i = 0; i < 256; i++)
            AddByte(fields, bytes, $"chemical_{i:D3}", i);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeInject(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "chemical", 0);
        AddFloat(fields, bytes, "amount", 1);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeNeuroEmitter(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "lobe0", 0);
        AddByte(fields, bytes, "neuron0", 1);
        AddByte(fields, bytes, "lobe1", 2);
        AddByte(fields, bytes, "neuron1", 3);
        AddByte(fields, bytes, "lobe2", 4);
        AddByte(fields, bytes, "neuron2", 5);
        AddByte(fields, bytes, "rate", 6);
        AddByte(fields, bytes, "chemical0", 7);
        AddFloat(fields, bytes, "amount0", 8);
        AddByte(fields, bytes, "chemical1", 9);
        AddFloat(fields, bytes, "amount1", 10);
        AddByte(fields, bytes, "chemical2", 11);
        AddFloat(fields, bytes, "amount2", 12);
        AddByte(fields, bytes, "chemical3", 13);
        AddFloat(fields, bytes, "amount3", 14);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeStimulus(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "stimulus", 0);
        AddByte(fields, bytes, "significance", 1);
        AddByte(fields, bytes, "input", 2);
        AddByte(fields, bytes, "intensity", 3);
        AddByte(fields, bytes, "features", 4);
        for (int i = 0; i < 4; i++)
        {
            int chemicalOffset = 5 + i * 2;
            AddByte(fields, bytes, $"chemical{i}", chemicalOffset);
            AddDerivedInt(fields, $"chemical{i}_biochemical", chemicalOffset, StimulusChemicalToBiochemical(bytes[chemicalOffset]));
            AddFloat(fields, bytes, $"amount{i}", chemicalOffset + 1);
        }
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeGenus(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "genus", 0);
        AddRaw(fields, bytes, "mother_moniker", 1, 32);
        AddRaw(fields, bytes, "father_moniker", 33, 32);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodePose(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "pose_number", 0);
        AddRaw(fields, bytes, "pose_string", 1, 16);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeGait(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "gait_number", 0);
        AddRaw(fields, bytes, "pose_sequence", 1, 8);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeInstinct(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "lobe0", 0);
        AddByte(fields, bytes, "cell0", 1);
        AddByte(fields, bytes, "lobe1", 2);
        AddByte(fields, bytes, "cell1", 3);
        AddByte(fields, bytes, "lobe2", 4);
        AddByte(fields, bytes, "cell2", 5);
        AddByte(fields, bytes, "action", 6);
        AddByte(fields, bytes, "reinforcement_chemical", 7);
        AddFloat(fields, bytes, "reinforcement_amount", 8);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeAppearance(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "body_region", 0);
        AddByte(fields, bytes, "breed_slot", 1);
        AddByte(fields, bytes, "species", 2);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodePigment(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "pigment", 0);
        AddByte(fields, bytes, "amount", 1);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodePigmentBleed(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "rotation", 0);
        AddByte(fields, bytes, "swap", 1);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeExpression(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "expression", 0);
        AddByte(fields, bytes, "padding", 1);
        AddByte(fields, bytes, "weight", 2);
        for (int i = 0; i < 4; i++)
        {
            AddByte(fields, bytes, $"drive{i}", 3 + i * 2);
            AddFloat(fields, bytes, $"amount{i}", 4 + i * 2);
        }
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeOrgan(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddFloat(fields, bytes, "clock_rate", 0);
        AddFloat(fields, bytes, "damage_rate", 1);
        AddFloat(fields, bytes, "life_force", 2);
        AddFloat(fields, bytes, "biotick_start", 3);
        AddFloat(fields, bytes, "atp_damage_coefficient", 4);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload RawFallback(GenePayloadKind kind, byte[] bytes)
        => new(kind, new[] { new GenePayloadField("raw", GenePayloadFieldKind.RawBytes, 0, bytes.Length, Convert.ToHexString(bytes)) }, bytes, true);

    private static void AddByte(List<GenePayloadField> fields, byte[] bytes, string name, int offset)
        => fields.Add(new(name, GenePayloadFieldKind.Byte, offset, 1, bytes[offset].ToString()));

    private static void AddDerivedInt(List<GenePayloadField> fields, string name, int offset, int value)
        => fields.Add(new(name, GenePayloadFieldKind.DerivedInt, offset, 0, value.ToString()));

    private static void AddRaw(List<GenePayloadField> fields, byte[] bytes, string name, int offset, int length)
        => fields.Add(new(name, GenePayloadFieldKind.RawBytes, offset, length, Convert.ToHexString(bytes.AsSpan(offset, length))));

    private static void AddInt16(List<GenePayloadField> fields, byte[] bytes, string name, int offset)
        => fields.Add(new(name, GenePayloadFieldKind.Int16, offset, 2, EditableGenePayload.ReadInt16(bytes, offset).ToString()));

    private static void AddFloat(List<GenePayloadField> fields, byte[] bytes, string name, int offset)
        => fields.Add(new(name, GenePayloadFieldKind.FloatByte, offset, 1, (bytes[offset] / 255f).ToString("0.###")));

    private static void AddToken(List<GenePayloadField> fields, byte[] bytes, string name, int offset)
        => fields.Add(new(name, GenePayloadFieldKind.Token, offset, 4, EditableGenePayload.ReadToken(bytes, offset)));

    public static int StimulusChemicalToBiochemical(int stimulusChemical)
        => stimulusChemical == 255
            ? 0
            : ((stimulusChemical + 148) % 256) + (stimulusChemical >= 108 ? 1 : 0);

    private static void WriteInt(byte[] bytes, GenePayloadField field, int value)
    {
        value = Math.Clamp(value, 0, field.Kind == GenePayloadFieldKind.Int16 ? 65535 : 255);
        switch (field.Kind)
        {
            case GenePayloadFieldKind.Byte:
            case GenePayloadFieldKind.FloatByte:
                bytes[field.Offset] = (byte)value;
                break;
            case GenePayloadFieldKind.Int16:
                bytes[field.Offset] = (byte)(value / 256);
                bytes[field.Offset + 1] = (byte)(value % 256);
                break;
        }
    }

    private static void WriteToken(byte[] bytes, GenePayloadField field, string value)
    {
        if (field.Kind != GenePayloadFieldKind.Token)
            return;

        string token = (value ?? string.Empty).PadRight(4).Substring(0, 4);
        Encoding.ASCII.GetBytes(token, bytes.AsSpan(field.Offset, 4));
    }
}
