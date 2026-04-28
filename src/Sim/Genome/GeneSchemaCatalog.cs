using System;
using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Genome;

public sealed record GenePayloadFieldSchema(string Name, GenePayloadFieldKind Kind, int Offset, int Length);

public sealed record GenePayloadSchema(
    GenePayloadKind PayloadKind,
    int Type,
    int Subtype,
    int ExactLength,
    string Source,
    IReadOnlyList<GenePayloadFieldSchema> Fields);

public static class GeneSchemaCatalog
{
    private const string Source = "Creatures Wiki GEN files and C3/DS Genetics Kit gene layouts";
    private static readonly IReadOnlyDictionary<GenePayloadKind, GenePayloadSchema> Schemas = BuildSchemas();

    public static IReadOnlyCollection<GenePayloadSchema> All => Schemas.Values.ToArray();

    public static GenePayloadSchema Get(GenePayloadKind kind)
        => Schemas.TryGetValue(kind, out GenePayloadSchema? schema)
            ? schema
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "No C3/DS schema is defined for this payload kind.");

    public static bool TryGet(GenePayloadKind kind, out GenePayloadSchema? schema)
        => Schemas.TryGetValue(kind, out schema);

    private static IReadOnlyDictionary<GenePayloadKind, GenePayloadSchema> BuildSchemas()
    {
        var schemas = new Dictionary<GenePayloadKind, GenePayloadSchema>
        {
            [GenePayloadKind.BrainLobe] = Schema(
                GenePayloadKind.BrainLobe,
                GeneType.BRAINGENE,
                BrainSubtype.G_LOBE,
                121,
                Field("token", GenePayloadFieldKind.Token, 0, 4),
                Field("update", GenePayloadFieldKind.Int16, 4, 2),
                Field("x", GenePayloadFieldKind.Int16, 6, 2),
                Field("y", GenePayloadFieldKind.Int16, 8, 2),
                Field("width", GenePayloadFieldKind.Byte, 10, 1),
                Field("height", GenePayloadFieldKind.Byte, 11, 1),
                Field("red", GenePayloadFieldKind.Byte, 12, 1),
                Field("green", GenePayloadFieldKind.Byte, 13, 1),
                Field("blue", GenePayloadFieldKind.Byte, 14, 1),
                Field("winner_takes_all", GenePayloadFieldKind.Byte, 15, 1),
                Field("tissue", GenePayloadFieldKind.Byte, 16, 1),
                Field("run_init_rule_always", GenePayloadFieldKind.Byte, 17, 1),
                Field("padding", GenePayloadFieldKind.RawBytes, 18, 7),
                Field("init_rule", GenePayloadFieldKind.RawBytes, 25, 48),
                Field("update_rule", GenePayloadFieldKind.RawBytes, 73, 48)),
            [GenePayloadKind.BrainOrgan] = OrganLikeSchema(GenePayloadKind.BrainOrgan, GeneType.BRAINGENE, BrainSubtype.G_BORGAN),
            [GenePayloadKind.BrainTract] = Schema(
                GenePayloadKind.BrainTract,
                GeneType.BRAINGENE,
                BrainSubtype.G_TRACT,
                128,
                Field("update", GenePayloadFieldKind.Int16, 0, 2),
                Field("source_lobe", GenePayloadFieldKind.Token, 2, 4),
                Field("source_min", GenePayloadFieldKind.Int16, 6, 2),
                Field("source_max", GenePayloadFieldKind.Int16, 8, 2),
                Field("source_dendrites_per_neuron", GenePayloadFieldKind.Int16, 10, 2),
                Field("destination_lobe", GenePayloadFieldKind.Token, 12, 4),
                Field("destination_min", GenePayloadFieldKind.Int16, 16, 2),
                Field("destination_max", GenePayloadFieldKind.Int16, 18, 2),
                Field("destination_dendrites_per_neuron", GenePayloadFieldKind.Int16, 20, 2),
                Field("random_connect_and_migrate", GenePayloadFieldKind.Byte, 22, 1),
                Field("random_dendrite_count", GenePayloadFieldKind.Byte, 23, 1),
                Field("source_growth_sv", GenePayloadFieldKind.Byte, 24, 1),
                Field("destination_growth_sv", GenePayloadFieldKind.Byte, 25, 1),
                Field("run_init_rule_always", GenePayloadFieldKind.Byte, 26, 1),
                Field("padding", GenePayloadFieldKind.RawBytes, 27, 5),
                Field("init_rule", GenePayloadFieldKind.RawBytes, 32, 48),
                Field("update_rule", GenePayloadFieldKind.RawBytes, 80, 48)),
            [GenePayloadKind.BiochemistryReceptor] = Schema(GenePayloadKind.BiochemistryReceptor, GeneType.BIOCHEMISTRYGENE, BiochemSubtype.G_RECEPTOR, 8),
            [GenePayloadKind.BiochemistryEmitter] = Schema(GenePayloadKind.BiochemistryEmitter, GeneType.BIOCHEMISTRYGENE, BiochemSubtype.G_EMITTER, 8),
            [GenePayloadKind.BiochemistryReaction] = Schema(GenePayloadKind.BiochemistryReaction, GeneType.BIOCHEMISTRYGENE, BiochemSubtype.G_REACTION, 9),
            [GenePayloadKind.BiochemistryHalfLife] = Schema(
                GenePayloadKind.BiochemistryHalfLife,
                GeneType.BIOCHEMISTRYGENE,
                BiochemSubtype.G_HALFLIFE,
                256,
                Enumerable.Range(0, 256)
                    .Select(i => Field($"chemical_{i:D3}", GenePayloadFieldKind.Byte, i, 1))
                    .ToArray()),
            [GenePayloadKind.BiochemistryInject] = Schema(GenePayloadKind.BiochemistryInject, GeneType.BIOCHEMISTRYGENE, BiochemSubtype.G_INJECT, 2),
            [GenePayloadKind.BiochemistryNeuroEmitter] = Schema(GenePayloadKind.BiochemistryNeuroEmitter, GeneType.BIOCHEMISTRYGENE, BiochemSubtype.G_NEUROEMITTER, 15),
            [GenePayloadKind.CreatureStimulus] = Schema(GenePayloadKind.CreatureStimulus, GeneType.CREATUREGENE, CreatureSubtype.G_STIMULUS, 13),
            [GenePayloadKind.CreatureGenus] = Schema(GenePayloadKind.CreatureGenus, GeneType.CREATUREGENE, CreatureSubtype.G_GENUS, 65),
            [GenePayloadKind.CreatureAppearance] = Schema(GenePayloadKind.CreatureAppearance, GeneType.CREATUREGENE, CreatureSubtype.G_APPEARANCE, 3),
            [GenePayloadKind.CreaturePose] = Schema(GenePayloadKind.CreaturePose, GeneType.CREATUREGENE, CreatureSubtype.G_POSE, 17),
            [GenePayloadKind.CreatureGait] = Schema(GenePayloadKind.CreatureGait, GeneType.CREATUREGENE, CreatureSubtype.G_GAIT, 9),
            [GenePayloadKind.CreatureInstinct] = Schema(GenePayloadKind.CreatureInstinct, GeneType.CREATUREGENE, CreatureSubtype.G_INSTINCT, 9),
            [GenePayloadKind.CreaturePigment] = Schema(GenePayloadKind.CreaturePigment, GeneType.CREATUREGENE, CreatureSubtype.G_PIGMENT, 2),
            [GenePayloadKind.CreaturePigmentBleed] = Schema(GenePayloadKind.CreaturePigmentBleed, GeneType.CREATUREGENE, CreatureSubtype.G_PIGMENTBLEED, 2),
            [GenePayloadKind.CreatureExpression] = Schema(GenePayloadKind.CreatureExpression, GeneType.CREATUREGENE, CreatureSubtype.G_EXPRESSION, 11),
            [GenePayloadKind.Organ] = OrganLikeSchema(GenePayloadKind.Organ, GeneType.ORGANGENE, OrganSubtype.G_ORGAN),
        };

        return schemas;
    }

    private static GenePayloadSchema OrganLikeSchema<TSubtype>(GenePayloadKind kind, GeneType type, TSubtype subtype)
        where TSubtype : Enum
        => Schema(
            kind,
            type,
            subtype,
            5,
            Field("clock_rate", GenePayloadFieldKind.Byte, 0, 1),
            Field("damage_rate", GenePayloadFieldKind.Byte, 1, 1),
            Field("life_force", GenePayloadFieldKind.Byte, 2, 1),
            Field("biotick_start", GenePayloadFieldKind.Byte, 3, 1),
            Field("atp_damage_coefficient", GenePayloadFieldKind.Byte, 4, 1));

    private static GenePayloadSchema Schema<TSubtype>(
        GenePayloadKind kind,
        GeneType type,
        TSubtype subtype,
        int length,
        params GenePayloadFieldSchema[] fields)
        where TSubtype : Enum
        => new(kind, (int)type, Convert.ToInt32(subtype), length, Source, fields.Length == 0 ? ByteFields(length) : fields);

    private static GenePayloadFieldSchema[] ByteFields(int length)
        => Enumerable.Range(0, length).Select(i => Field($"byte_{i:D2}", GenePayloadFieldKind.Byte, i, 1)).ToArray();

    private static GenePayloadFieldSchema Field(string name, GenePayloadFieldKind kind, int offset, int length)
        => new(name, kind, offset, length);
}
