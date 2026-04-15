namespace CreaturesReborn.Sim.Biochemistry;

// -------------------------------------------------------------------------
// Capacity limits (from biochemistry.h / organ.h / receptor.h / emitter.h)
// -------------------------------------------------------------------------
public static class BiochemConst
{
    public const int NUMCHEM             = 256;   // chemical slot count (0 = "none")
    public const int MAXORGANS           = 128;
    public const int MAXREACTIONS        = 128;
    public const int MAXREACTANTS        = 16;    // max proportion per reactant
    public const int MAXRECEPTORS        = 128;   // per organ
    public const int MAXRECEPTORGROUPS   = 255;   // per organ
    public const int MAXEMITTERS         = 128;   // per organ
    public const int MAX_NEUROEMITTERS   = 128;

    // Brain locus stride (each neuron exposes this many loci)
    public const int NoOfNeuronVariablesAsLoci = 4;

    // NeuroEmitter structure sizes
    public const int NeuroEmitter_NeuronalInputs   = 3;
    public const int NeuroEmitter_ChemEmissions    = 4;

    // Life-force base values (Organ statics, mirrored here for tests)
    public const float BaseLifeForce  = 1e6f;
    public const float MinLifeForce   = 0.5f;
    public const float RateOfDecay    = 1e-5f;
    public const float BaseADPCost    = 0.0078f;

    // Age stages — Age is a byte (0-255); 0 = embryo/baby.
    public const byte AGE_BABY = 0;

    // Max loci per creature tissue.  Sensorimotor emitter has the most
    // (~39 entries including gaits), so 64 gives comfortable headroom.
    public const int MAX_LOCI_PER_TISSUE = 64;
}

// -------------------------------------------------------------------------
// Specialised chemical IDs (from BiochemistryConstants.h)
// -------------------------------------------------------------------------
public static class ChemID
{
    public const int None            = 0;
    public const int Glycogen        = 4;
    public const int ATP             = 35;
    public const int ADP             = 36;
    public const int Progesterone    = 48;
    public const int Injury          = 127;
    public const int Tiredness       = 154;
    public const int Sleepiness      = 155;
    public const int FirstSmell      = 165;
    public const int FirstAntigen    = 82;
    public const int LastAntigen     = 89;
}

// -------------------------------------------------------------------------
// Organ IDs — used in RECEPTOR/EMITTER genes to identify target organ.
// -------------------------------------------------------------------------
public static class OrganID
{
    public const int ORGAN_BRAIN           = 0;  // brain lobes
    public const int ORGAN_CREATURE        = 1;  // creature locus table
    public const int ORGAN_ORGAN           = 2;  // this organ's own internal loci
    public const int NUM_EMITTER_ORGANS    = 3;  // (ORGAN_REACTION is receptor-only)
    public const int ORGAN_REACTION        = 3;  // reaction rate locus
    public const int NUM_RECEPTOR_ORGANS   = 4;
}

// -------------------------------------------------------------------------
// Creature tissue IDs (ORGAN_CREATURE sub-groups)
// -------------------------------------------------------------------------
public enum CreatureTissue
{
    Somatic        = 0,
    Circulatory    = 1,
    Reproductive   = 2,
    Immune         = 3,
    Sensorimotor   = 4,
    Drives         = 5,
    Count          = 6,
}

// -------------------------------------------------------------------------
// Locus type passed to GetLocusAddress
// -------------------------------------------------------------------------
public enum LocusType { Receptor = 0, Emitter = 1 }

// -------------------------------------------------------------------------
// Organ-internal locus IDs (ORGAN_ORGAN)
// -------------------------------------------------------------------------
public static class OrganReceptorLocus
{
    public const int ClockRate       = 0;  // RLOCUS_CLOCKRATE
    public const int RateOfRepair    = 1;  // RLOCUS_RATEOFREPAIR
    public const int Injury          = 2;  // RLOCUS_INJURY
}

public static class OrganEmitterLocus
{
    public const int ClockRate       = 0;  // ELOCUS_CLOCKRATE
    public const int RateOfRepair    = 1;  // ELOCUS_RATEOFREPAIR
    public const int LifeForce       = 2;  // ELOCUS_LIFEFORCE
}

// -------------------------------------------------------------------------
// Creature receptor loci (ORGAN_CREATURE : tissue : locus)
// Each tissue's loci start at 0.
// -------------------------------------------------------------------------
public static class SomaticReceptorLocus
{
    public const int Age0 = 0; public const int Age1 = 1; public const int Age2 = 2;
    public const int Age3 = 3; public const int Age4 = 4; public const int Age5 = 5;
    public const int Age6 = 6;
}

public static class SomaticEmitterLocus
{
    public const int Muscles = 0;
}

public static class CirculatoryLocus
{
    // "Floating" loci — both receptor and emitter.
    public const int First = 0;
    public const int Last  = 31;
    public const int Count = 32;
}

public static class ReproductiveReceptorLocus
{
    public const int Ovulate          = 0;
    public const int Receptive        = 1;
    public const int ChanceOfMutation = 2;
    public const int DegreeOfMutation = 3;
}

public static class ReproductiveEmitterLocus
{
    public const int Fertile          = 0;
    public const int Pregnant         = 1;
    public const int Ovulate          = 2;
    public const int Receptive        = 3;
    public const int ChanceOfMutation = 4;
    public const int DegreeOfMutation = 5;
}

public static class ImmuneLocus
{
    public const int Die  = 0; // receptor
    public const int Dead = 0; // emitter
}

public static class SensorimotorEmitterLocus
{
    public const int Const        = 0;
    public const int Asleep       = 1;
    public const int Coldness     = 2;
    public const int Hotness      = 3;
    public const int LightLevel   = 4;
    public const int Crowdedness  = 5;
    public const int Radiation    = 6;
    public const int TimeOfDay    = 7;
    public const int Season       = 8;
    public const int AirQuality   = 9;
    public const int Upslope      = 10;
    public const int Downslope    = 11;
    public const int HeadWind     = 12;
    public const int TailWind     = 13;
    // LOC_E_INVOLUNTARY0..7 = 14..21
    public const int Involuntary0 = 14;
    // LOC_E_GAIT0..16 = 22..38
    public const int Gait0        = 22;
}

public static class SensorimotorReceptorLocus
{
    public const int Involuntary0 = 0;   // ..7 = 0..7
    public const int Gait0        = 8;   // ..16 = 8..24
}

public static class DriveLocus
{
    public const int Drive0 = 0;
    public const int Drive19 = 19;
    public const int Count = 20;
}

// -------------------------------------------------------------------------
// Receptor / Emitter flag enums (from Receptor.h / Emitter.h)
// -------------------------------------------------------------------------
[System.Flags]
public enum ReceptorFlags : int
{
    None      = 0,
    RE_REDUCE  = 1,  // reduce nominal when chemical present (default: raise)
    RE_DIGITAL = 2,  // output = Gain if any signal present (not proportional)
}

[System.Flags]
public enum EmitterFlags : int
{
    None      = 0,
    EM_REMOVE  = 1,  // zero the source locus after emission
    EM_DIGITAL = 2,  // emit Gain regardless of signal magnitude
    EM_INVERT  = 4,  // invert source value (1 - v) before processing
}
