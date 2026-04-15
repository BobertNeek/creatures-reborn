namespace CreaturesReborn.Sim.Biochemistry;

/// <summary>
/// One chemical emitter site that reads a locus and emits a chemical.
/// Direct port of c2e's <c>Emitter</c> struct (Emitter.h).
/// </summary>
public sealed class Emitter
{
    // ---- Locus ID (filled from genome) ----
    public int IDOrgan;   // source organ  (OrganID.*)
    public int IDTissue;  // source tissue in that organ
    public int IDLocus;   // source locus in that tissue

    // ---- Genetically determined ----
    public int   Chem;         // chemical to emit
    public float Threshold;    // source signal must exceed this before emitting
    public float bioTickRate;  // fractional ticks per update (1/N from genome)
    public float Gain;         // emission amount or attenuation factor
    public int   Effect;       // EmitterFlags bitmask

    // ---- Runtime state ----
    public float bioTick;      // accumulator: emits when ≥ 1.0

    // ---- Dynamically bound ----
    /// <summary>The locus this emitter reads its signal from.</summary>
    public FloatLocus Source = FloatLocus.DefaultNeuronInput;
}
