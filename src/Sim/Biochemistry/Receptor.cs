namespace CreaturesReborn.Sim.Biochemistry;

/// <summary>
/// One chemical receptor site that reads a chemical concentration and writes to a locus.
/// Direct port of c2e's <c>Receptor</c> struct (Receptor.h).
/// </summary>
public sealed class Receptor
{
    // ---- Locus ID (filled from genome) ----
    public int IDOrgan;   // target organ  (OrganID.*)
    public int IDTissue;  // target tissue in that organ
    public int IDLocus;   // target locus in that tissue

    // ---- Genetically determined ----
    public int   Chem;       // chemical I'm sensitive to (0 = none)
    public float Threshold;  // chemical must exceed this before I respond
    public float Nominal;    // base output value; chemical modulates around this
    public float Gain;       // how strongly the chemical modulates the output
    public int   Effect;     // ReceptorFlags bitmask

    // ---- Dynamically bound ----
    /// <summary>The locus this receptor writes its computed signal to.</summary>
    public FloatLocus Dest = FloatLocus.Invalid;

    /// <summary>True if this receptor targets ORGAN_ORGAN / RLOCUS_CLOCKRATE (processed at sub-tick rate).</summary>
    public bool isClockRateReceptor;
}
