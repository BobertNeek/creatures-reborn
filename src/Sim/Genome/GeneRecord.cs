using System;

namespace CreaturesReborn.Sim.Genome;

public enum GenePayloadKind
{
    Unknown = 0,
    BrainLobe,
    BrainOrgan,
    BrainTract,
    BiochemistryReceptor,
    BiochemistryEmitter,
    BiochemistryReaction,
    BiochemistryHalfLife,
    BiochemistryInject,
    BiochemistryNeuroEmitter,
    CreatureStimulus,
    CreatureGenus,
    CreatureAppearance,
    CreaturePose,
    CreatureGait,
    CreatureInstinct,
    CreaturePigment,
    CreaturePigmentBleed,
    CreatureExpression,
    Organ
}

public readonly record struct GeneIdentity(int Type, int Subtype, int Id, int Generation)
{
    public GeneFamilyIdentity FamilyIdentity => new(Type, Subtype, Id);

    public override string ToString()
        => $"{GeneNames.TypeName(Type)}/{GeneNames.SubtypeName(Type, Subtype)} #{Id} gen {Generation}";
}

public readonly record struct GeneFamilyIdentity(int Type, int Subtype, int Id);

public readonly record struct GeneHeader(
    int Offset,
    int Length,
    int Type,
    int Subtype,
    int Id,
    int Generation,
    int SwitchOnAge,
    byte Flags,
    int Mutability,
    int Variant)
{
    public GeneIdentity Identity => new(Type, Subtype, Id, Generation);
    public MutFlags MutabilityFlags => (MutFlags)Flags;
    public bool IsMutable => (MutabilityFlags & MutFlags.MUT) != 0;
    public bool CanDuplicate => (MutabilityFlags & MutFlags.DUP) != 0;
    public bool CanCut => (MutabilityFlags & MutFlags.CUT) != 0;
    public bool IsSilent => (MutabilityFlags & MutFlags.MIGNORE) != 0;
    public bool MaleLinked => (MutabilityFlags & MutFlags.LINKMALE) != 0;
    public bool FemaleLinked => (MutabilityFlags & MutFlags.LINKFEMALE) != 0;
}

public sealed record GenePayload(int Offset, int Length, GenePayloadKind Kind, byte[] Bytes);

public sealed record GeneRecord(GeneHeader Header, GenePayload Payload, byte[] RawBytes)
{
    public int Offset => Header.Offset;
    public int Length => Header.Length;
    public int Type => Header.Type;
    public int Subtype => Header.Subtype;
    public int Id => Header.Id;
    public int Generation => Header.Generation;
    public int SwitchOnAge => Header.SwitchOnAge;
    public byte Flags => Header.Flags;
    public int Mutability => Header.Mutability;
    public int Variant => Header.Variant;
    public GeneIdentity Identity => Header.Identity;
    public GeneFamilyIdentity FamilyIdentity => Identity.FamilyIdentity;
    public string DisplayName => GeneNames.DisplayName(Type, Subtype);
}

internal static class GeneNames
{
    public static string DisplayName(int type, int subtype)
        => $"{TypeName(type)}/{SubtypeName(type, subtype)}";

    public static string TypeName(int type) => type switch
    {
        (int)GeneType.BRAINGENE => "Brain",
        (int)GeneType.BIOCHEMISTRYGENE => "Biochemistry",
        (int)GeneType.CREATUREGENE => "Creature",
        (int)GeneType.ORGANGENE => "Organ",
        _ => $"UnknownType{type}"
    };

    public static string SubtypeName(int type, int subtype) => type switch
    {
        (int)GeneType.BRAINGENE => subtype switch
        {
            (int)BrainSubtype.G_LOBE => "Lobe",
            (int)BrainSubtype.G_BORGAN => "BrainOrgan",
            (int)BrainSubtype.G_TRACT => "Tract",
            _ => $"UnknownBrainSubtype{subtype}"
        },
        (int)GeneType.BIOCHEMISTRYGENE => subtype switch
        {
            (int)BiochemSubtype.G_RECEPTOR => "Receptor",
            (int)BiochemSubtype.G_EMITTER => "Emitter",
            (int)BiochemSubtype.G_REACTION => "Reaction",
            (int)BiochemSubtype.G_HALFLIFE => "HalfLife",
            (int)BiochemSubtype.G_INJECT => "Inject",
            (int)BiochemSubtype.G_NEUROEMITTER => "NeuroEmitter",
            _ => $"UnknownBiochemSubtype{subtype}"
        },
        (int)GeneType.CREATUREGENE => subtype switch
        {
            (int)CreatureSubtype.G_STIMULUS => "Stimulus",
            (int)CreatureSubtype.G_GENUS => "Genus",
            (int)CreatureSubtype.G_APPEARANCE => "Appearance",
            (int)CreatureSubtype.G_POSE => "Pose",
            (int)CreatureSubtype.G_GAIT => "Gait",
            (int)CreatureSubtype.G_INSTINCT => "Instinct",
            (int)CreatureSubtype.G_PIGMENT => "Pigment",
            (int)CreatureSubtype.G_PIGMENTBLEED => "PigmentBleed",
            (int)CreatureSubtype.G_EXPRESSION => "Expression",
            _ => $"UnknownCreatureSubtype{subtype}"
        },
        (int)GeneType.ORGANGENE => subtype switch
        {
            (int)OrganSubtype.G_ORGAN => "Organ",
            _ => $"UnknownOrganSubtype{subtype}"
        },
        _ => $"Subtype{subtype}"
    };

    public static GenePayloadKind PayloadKind(int type, int subtype) => type switch
    {
        (int)GeneType.BRAINGENE => subtype switch
        {
            (int)BrainSubtype.G_LOBE => GenePayloadKind.BrainLobe,
            (int)BrainSubtype.G_BORGAN => GenePayloadKind.BrainOrgan,
            (int)BrainSubtype.G_TRACT => GenePayloadKind.BrainTract,
            _ => GenePayloadKind.Unknown
        },
        (int)GeneType.BIOCHEMISTRYGENE => subtype switch
        {
            (int)BiochemSubtype.G_RECEPTOR => GenePayloadKind.BiochemistryReceptor,
            (int)BiochemSubtype.G_EMITTER => GenePayloadKind.BiochemistryEmitter,
            (int)BiochemSubtype.G_REACTION => GenePayloadKind.BiochemistryReaction,
            (int)BiochemSubtype.G_HALFLIFE => GenePayloadKind.BiochemistryHalfLife,
            (int)BiochemSubtype.G_INJECT => GenePayloadKind.BiochemistryInject,
            (int)BiochemSubtype.G_NEUROEMITTER => GenePayloadKind.BiochemistryNeuroEmitter,
            _ => GenePayloadKind.Unknown
        },
        (int)GeneType.CREATUREGENE => subtype switch
        {
            (int)CreatureSubtype.G_STIMULUS => GenePayloadKind.CreatureStimulus,
            (int)CreatureSubtype.G_GENUS => GenePayloadKind.CreatureGenus,
            (int)CreatureSubtype.G_APPEARANCE => GenePayloadKind.CreatureAppearance,
            (int)CreatureSubtype.G_POSE => GenePayloadKind.CreaturePose,
            (int)CreatureSubtype.G_GAIT => GenePayloadKind.CreatureGait,
            (int)CreatureSubtype.G_INSTINCT => GenePayloadKind.CreatureInstinct,
            (int)CreatureSubtype.G_PIGMENT => GenePayloadKind.CreaturePigment,
            (int)CreatureSubtype.G_PIGMENTBLEED => GenePayloadKind.CreaturePigmentBleed,
            (int)CreatureSubtype.G_EXPRESSION => GenePayloadKind.CreatureExpression,
            _ => GenePayloadKind.Unknown
        },
        (int)GeneType.ORGANGENE => subtype == (int)OrganSubtype.G_ORGAN ? GenePayloadKind.Organ : GenePayloadKind.Unknown,
        _ => GenePayloadKind.Unknown
    };
}
