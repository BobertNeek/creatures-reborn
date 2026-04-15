namespace CreaturesReborn.Sim.Biochemistry;

/// <summary>
/// A bindable float value — the C# equivalent of c2e's <c>float*</c> locus pointer.
/// Receptors write to a <see cref="FloatLocus.Value"/>; emitters read from one.
/// Using a heap object lets multiple components share the same locus without unsafe code.
/// <para>
/// When created with <see cref="FloatLocus(float[], int)"/>, reads and writes pass through
/// directly to the backing array slot — equivalent to c2e's raw <c>float*</c>.
/// </para>
/// </summary>
public sealed class FloatLocus
{
    private float _value;
    private readonly float[]? _backing;
    private readonly int _backingIdx;

    /// <summary>Standalone locus with initial value 0.</summary>
    public FloatLocus() { }

    /// <summary>
    /// Backed locus: reads and writes go directly to <paramref name="arr"/>[<paramref name="index"/>].
    /// Equivalent to storing a <c>float*</c> pointing into a neuron's state vector.
    /// </summary>
    public FloatLocus(float[] arr, int index) { _backing = arr; _backingIdx = index; }

    public float Value
    {
        get => _backing != null ? _backing[_backingIdx] : _value;
        set { if (_backing != null) _backing[_backingIdx] = value; else _value = value; }
    }

    /// <summary>Returned instead of null when a locus cannot be resolved. Writes are discarded; reads return 0.</summary>
    public static readonly FloatLocus Invalid = new();

    /// <summary>
    /// Default neuronal input (= 1.0) used by <see cref="NeuroEmitter"/> before Brain wires real neuron loci.
    /// Value is intentionally shared; do not write to it.
    /// </summary>
    public static readonly FloatLocus DefaultNeuronInput = new() { Value = 1.0f };
}

/// <summary>
/// Implemented by any subsystem that can expose float loci to external binders
/// (e.g. the Brain exposes neuron output loci to NeuroEmitters and Emitters).
/// </summary>
public interface IBrainLocusProvider
{
    /// <summary>
    /// Return the FloatLocus for the given lobe/neuron index combination.
    /// Returns <see cref="FloatLocus.DefaultNeuronInput"/> if not yet wired.
    /// </summary>
    FloatLocus GetBrainLocus(int lobeId, int neuronLocusIdx);
}
