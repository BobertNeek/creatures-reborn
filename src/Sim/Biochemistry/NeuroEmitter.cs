namespace CreaturesReborn.Sim.Biochemistry;

/// <summary>
/// Emits chemicals in proportion to the product of three brain neuron outputs.
/// Direct port of c2e's <c>NeuroEmitter</c> struct (NeuroEmitter.h/.cpp).
/// </summary>
/// <remarks>
/// Each tick accumulates <see cref="bioTick"/> += <see cref="bioTickRate"/>.
/// When it overflows 1.0: multiply all three neuronal inputs together, then emit each
/// configured chemical scaled by that product. This lets a brain state pattern
/// directly drive biochemistry.
/// </remarks>
public sealed class NeuroEmitter
{
    public const int NumNeuronalInputs  = BiochemConst.NeuroEmitter_NeuronalInputs;  // 3
    public const int NumChemEmissions   = BiochemConst.NeuroEmitter_ChemEmissions;   // 4

    public float bioTickRate;
    public float bioTick;

    /// <summary>
    /// Pointers to the three brain neuron output loci.  Default = <see cref="FloatLocus.DefaultNeuronInput"/>
    /// (value 1.0) so un-wired neuroemitters treat the input as fully active.
    /// </summary>
    public readonly FloatLocus[] NeuronalInputs = new FloatLocus[NumNeuronalInputs];

    public struct ChemEmission
    {
        public int   ChemId;
        public float Amount;
    }
    public readonly ChemEmission[] ChemEmissions = new ChemEmission[NumChemEmissions];

    public NeuroEmitter()
    {
        for (int i = 0; i < NumNeuronalInputs; i++)
            NeuronalInputs[i] = FloatLocus.DefaultNeuronInput;
        // ChemEmissions default to ChemId=0 ("none") and Amount=0 — zero-initialised.
    }
}
