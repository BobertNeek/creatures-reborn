namespace CreaturesReborn.Sim.Genome;

/// <summary>
/// All genome-format constants, enums, and field offsets — ported 1:1 from c2e's
/// <c>engine/Creature/Genome.h</c>. Keeping the identifiers and comments verbatim so
/// a grep across the C++ source and this port finds the same symbols.
/// </summary>
public static class GeneConstants
{
    // Number of behaviour variants a creature may express.
    public const int NUM_BEHAVIOUR_VARIANTS = 8;

    // 4-byte tokens as little-endian packed ints. Matches c2e's Tok('d','n','a','3').
    // ASCII 'd'=0x64 'n'=0x6E 'a'=0x61 '3'=0x33 → 0x33616E64
    public const int DNA3TOKEN      = ('3' << 24) | ('a' << 16) | ('n' << 8) | 'd';  // "dna3"
    public const int GENETOKEN      = ('e' << 24) | ('n' << 16) | ('e' << 8) | 'g';  // "gene"
    public const int ENDGENOMETOKEN = ('d' << 24) | ('n' << 16) | ('e' << 8) | 'g';  // "gend"

    // Crossover & mutation parameters (Genome.h:66-76)
    public const int LINKAGE       = 50;    // average # genes between crossover points
    public const int CUTERRORRATE  = 80;    // 1/n probability a crossover causes dup/cut
    public const int MUTATIONRATE  = 4800;  // 1/n probability a codon mutates

    // Length-guard used by CopyGene (Genome.cpp:37)
    public const int OUR_LONGER_THAN_ANY_GENE = 1024;

    // Sex constants used by gene filtering in GetGeneType.
    public const int MALE   = 1;
    public const int FEMALE = 2;

    /// <summary>Invalid type/subtype sentinel used by GetGeneType endType parameters.</summary>
    public const int INVALIDGENE    = -1;
    public const int INVALIDSUBGENE = -1;
}

/// <summary>
/// Offsets into a gene header, counted from the <c>'gene'</c> start marker.
/// Mirrors <c>geneheaderoffsets</c> in Genome.h.
/// </summary>
public static class GeneHeaderOffsets
{
    public const int GH_TYPE       = 4;  // BRAINGENE etc.
    public const int GH_SUB        = 5;  // G_LOBE etc.
    public const int GH_ID         = 6;  // ID# (for gene editor tracking)
    public const int GH_GEN        = 7;  // generation# (for clones)
    public const int GH_SWITCHON   = 8;  // switch-on time
    public const int GH_FLAGS      = 9;  // mutability flags
    public const int GH_MUTABILITY = 10; // breadth of mutability
    public const int GH_VARIANT    = 11; // variant
    public const int GH_LENGTH     = 12; // total bytes in header, INCLUDING start marker
}

/// <summary>
/// Offsets into the Header Gene (gene(0)) — fields written during conception.
/// Mirrors <c>headeroffsets</c> in Genome.h.
/// </summary>
public static class HeaderGeneOffsets
{
    public const int GO_GENUS = GeneHeaderOffsets.GH_LENGTH;  // 1st field is genus
    public const int GO_MUM   = GO_GENUS + 1;                 // mum's moniker (32 bytes)
    public const int GO_DAD   = GO_MUM + 32;                  // dad's moniker (32 bytes)
}

/// <summary>Gene header mutability flag bits. Mirrors <c>mutflags</c> in Genome.h.</summary>
[System.Flags]
public enum MutFlags : byte
{
    None       = 0,
    MUT        = 1,   // gene allows mutations
    DUP        = 2,   // gene may be duplicated
    CUT        = 4,   // gene may be excised
    LINKMALE   = 8,   // gene must only express in males
    LINKFEMALE = 16,  // gene must only express in females
    MIGNORE    = 32,  // gene must be carried but not expressed
}

/// <summary>Top-level gene TYPE numbers. Mirrors anonymous enum in Genome.h.</summary>
public enum GeneType : byte
{
    BRAINGENE        = 0,  // CBrain class
    BIOCHEMISTRYGENE = 1,  // emitter, receptors, reactions, half-lives
    CREATUREGENE     = 2,  // sensory stimuli, appearance, etc.
    ORGANGENE        = 3,
}

public static class GeneTypeInfo
{
    public const int NUMGENETYPES = 4;
}

/// <summary>BRAINGENE subtypes. Mirrors anonymous enum at Genome.h:95.</summary>
public enum BrainSubtype : byte
{
    G_LOBE   = 0,  // define a brain lobe & its cells
    G_BORGAN = 1,  // configure its organ characteristics
    G_TRACT  = 2,  // define a brain tract & its dendrites (C3+)
}

public static class BrainSubtypeInfo { public const int NUMBRAINSUBTYPES = 3; }

/// <summary>BIOCHEMISTRYGENE subtypes. Mirrors anonymous enum at Genome.h:104.</summary>
public enum BiochemSubtype : byte
{
    G_RECEPTOR     = 0,
    G_EMITTER      = 1,
    G_REACTION     = 2,
    G_HALFLIFE     = 3,
    G_INJECT       = 4,
    G_NEUROEMITTER = 5,
}

public static class BiochemSubtypeInfo { public const int NUMBIOCHEMSUBTYPES = 6; }

/// <summary>ORGANGENE subtypes. Mirrors anonymous enum at Genome.h:116.</summary>
public enum OrganSubtype : byte
{
    G_ORGAN = 0,
}

public static class OrganSubtypeInfo { public const int NUMORGANSUBTYPES = 1; }

/// <summary>CREATUREGENE subtypes. Mirrors anonymous enum at Genome.h:122.</summary>
public enum CreatureSubtype : byte
{
    G_STIMULUS     = 0,
    G_GENUS        = 1,
    G_APPEARANCE   = 2,
    G_POSE         = 3,
    G_GAIT         = 4,
    G_INSTINCT     = 5,
    G_PIGMENT      = 6,
    G_PIGMENTBLEED = 7,
    G_EXPRESSION   = 8,
}

public static class CreatureSubtypeInfo { public const int NUMCREATURESUBTYPES = 9; }

/// <summary>Body regions that G_APPEARANCE genes control. Mirrors <c>bodyregions</c>.</summary>
public enum BodyRegion : byte
{
    REGION_HEAD = 0,
    REGION_BODY = 1,
    REGION_LEGS = 2,
    REGION_ARMS = 3,
    REGION_TAIL = 4,
    REGION_HAIR = 5,
}

public static class BodyRegionInfo { public const int NUMREGIONS = 6; }

/// <summary>
/// Gene-switching flag overrides passed to <c>GetGeneType</c>. Mirrors
/// <c>geneswitchoverrides</c> in Genome.h:162.
/// </summary>
public enum GeneSwitchOverride
{
    /// <summary>DEFAULT: switch on if gene is timed to go off at this myAge.</summary>
    SWITCH_AGE = 0,

    /// <summary>Switch on every time the genome is scanned (e.g. re-read APPEARANCE genes).</summary>
    SWITCH_ALWAYS = 1,

    /// <summary>Switch on if myAge == 0, regardless of stored switch-on time.</summary>
    SWITCH_EMBRYO = 2,

    /// <summary>Age of gene is younger or equal to creature age.</summary>
    SWITCH_UPTOAGE = 3,
}

/// <summary>Thrown when a genome cannot be initialised or parsed.</summary>
public sealed class GenomeException : System.Exception
{
    public int SourceLine { get; }
    public GenomeException(string message, int sourceLine = 0) : base(message)
    {
        SourceLine = sourceLine;
    }
}
