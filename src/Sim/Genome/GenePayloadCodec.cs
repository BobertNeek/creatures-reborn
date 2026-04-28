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
            GenePayloadKind.BrainTract when bytes.Length >= 29 => DecodeBrainTract(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryReceptor when bytes.Length >= 8 => DecodeReceptor(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryEmitter when bytes.Length >= 8 => DecodeEmitter(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryReaction when bytes.Length >= 9 => DecodeReaction(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryHalfLife when bytes.Length >= 2 => DecodeHalfLife(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryInject when bytes.Length >= 2 => DecodeInject(record.Payload.Kind, bytes, fields),
            GenePayloadKind.BiochemistryNeuroEmitter when bytes.Length >= 8 => DecodeNeuroEmitter(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreatureStimulus when bytes.Length >= 9 => DecodeStimulus(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreatureInstinct when bytes.Length >= 18 => DecodeInstinct(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreatureAppearance when bytes.Length >= 5 => DecodeAppearance(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreaturePigment when bytes.Length >= 4 => DecodePigment(record.Payload.Kind, bytes, fields),
            GenePayloadKind.CreaturePigmentBleed when bytes.Length >= 4 => DecodePigmentBleed(record.Payload.Kind, bytes, fields),
            GenePayloadKind.Organ when bytes.Length >= 13 => DecodeOrgan(record.Payload.Kind, bytes, fields),
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
        AddToken(fields, bytes, "spare_token", 19);
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
        AddByte(fields, bytes, "chemical", 0);
        AddByte(fields, bytes, "half_life", 1);
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
        AddByte(fields, bytes, "source_lobe", 0);
        AddByte(fields, bytes, "source_neuron", 1);
        AddByte(fields, bytes, "source_state", 2);
        AddByte(fields, bytes, "chemical", 3);
        AddFloat(fields, bytes, "threshold", 4);
        AddFloat(fields, bytes, "gain", 5);
        AddByte(fields, bytes, "effect", 6);
        AddByte(fields, bytes, "flags", 7);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeStimulus(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "stimulus", 0);
        AddByte(fields, bytes, "verb", 1);
        AddByte(fields, bytes, "noun", 2);
        AddByte(fields, bytes, "drive", 3);
        AddFloat(fields, bytes, "drive_adjustment", 4);
        AddByte(fields, bytes, "chemical1", 5);
        AddFloat(fields, bytes, "chemical1_amount", 6);
        AddByte(fields, bytes, "chemical2", 7);
        AddFloat(fields, bytes, "chemical2_amount", 8);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeInstinct(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddToken(fields, bytes, "source_lobe", 0);
        AddInt16(fields, bytes, "source_neuron", 4);
        AddToken(fields, bytes, "destination_lobe", 6);
        AddInt16(fields, bytes, "destination_neuron", 10);
        AddByte(fields, bytes, "drive", 12);
        AddByte(fields, bytes, "verb", 13);
        AddByte(fields, bytes, "noun", 14);
        AddFloat(fields, bytes, "reward", 15);
        AddFloat(fields, bytes, "punishment", 16);
        AddByte(fields, bytes, "flags", 17);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeAppearance(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "body_region", 0);
        AddByte(fields, bytes, "breed_slot", 1);
        AddByte(fields, bytes, "species", 2);
        AddByte(fields, bytes, "sex", 3);
        AddByte(fields, bytes, "variant", 4);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodePigment(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "color_r", 0);
        AddByte(fields, bytes, "color_g", 1);
        AddByte(fields, bytes, "color_b", 2);
        AddByte(fields, bytes, "rotation", 3);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodePigmentBleed(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "swap_r", 0);
        AddByte(fields, bytes, "swap_g", 1);
        AddByte(fields, bytes, "swap_b", 2);
        AddByte(fields, bytes, "amount", 3);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload DecodeOrgan(GenePayloadKind kind, byte[] bytes, List<GenePayloadField> fields)
    {
        AddByte(fields, bytes, "organ", 0);
        AddByte(fields, bytes, "clock_rate", 1);
        AddFloat(fields, bytes, "life_force", 2);
        AddFloat(fields, bytes, "biotick_start", 3);
        AddFloat(fields, bytes, "atp_damage_coefficient", 4);
        AddFloat(fields, bytes, "repair_rate", 5);
        AddFloat(fields, bytes, "injury_to_apply", 6);
        AddFloat(fields, bytes, "life_force_repair_rate", 7);
        AddFloat(fields, bytes, "damage_rate", 8);
        AddFloat(fields, bytes, "energy_cost", 9);
        AddFloat(fields, bytes, "capacity", 10);
        AddFloat(fields, bytes, "initial_health", 11);
        AddByte(fields, bytes, "flags", 12);
        return new EditableGenePayload(kind, fields, bytes, false);
    }

    private static EditableGenePayload RawFallback(GenePayloadKind kind, byte[] bytes)
        => new(kind, new[] { new GenePayloadField("raw", GenePayloadFieldKind.RawBytes, 0, bytes.Length, Convert.ToHexString(bytes)) }, bytes, true);

    private static void AddByte(List<GenePayloadField> fields, byte[] bytes, string name, int offset)
        => fields.Add(new(name, GenePayloadFieldKind.Byte, offset, 1, bytes[offset].ToString()));

    private static void AddInt16(List<GenePayloadField> fields, byte[] bytes, string name, int offset)
        => fields.Add(new(name, GenePayloadFieldKind.Int16, offset, 2, EditableGenePayload.ReadInt16(bytes, offset).ToString()));

    private static void AddFloat(List<GenePayloadField> fields, byte[] bytes, string name, int offset)
        => fields.Add(new(name, GenePayloadFieldKind.FloatByte, offset, 1, (bytes[offset] / 255f).ToString("0.###")));

    private static void AddToken(List<GenePayloadField> fields, byte[] bytes, string name, int offset)
        => fields.Add(new(name, GenePayloadFieldKind.Token, offset, 4, EditableGenePayload.ReadToken(bytes, offset)));

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
