using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Sim.Brain;

/// <summary>
/// Abstract base shared by <see cref="Lobe"/> and <see cref="Tract"/>.
/// Direct port of c2e's <c>BrainComponent</c> class (BrainComponent.h).
/// </summary>
public abstract class BrainComponent
{
    public int IdInList;

    /// <summary>
    /// Tick at which this component is updated (0 = never).
    /// Determines sort order in <c>myBrainComponents</c>.
    /// </summary>
    public int UpdateAtTime;

    protected bool  _initialised;
    protected bool  _runInitRuleAlways;
    protected bool  _supportsReinforcement;

    protected SVRule InitRule   = new();
    protected SVRule UpdateRule = new();

    protected float[]? _chemicals;

    // -------------------------------------------------------------------------
    // Component-level API
    // -------------------------------------------------------------------------

    public void RegisterBiochemistry(float[] chemicals)
    {
        _chemicals = chemicals;
        InitRule.RegisterChemicals(chemicals);
        UpdateRule.RegisterChemicals(chemicals);
    }

    public void RegisterRng(IRng rng)
    {
        InitRule.RegisterRng(rng);
        UpdateRule.RegisterRng(rng);
    }

    /// <summary>True when this component is a Tract and supports reward/punishment opcodes.</summary>
    public bool SupportsReinforcement() => _supportsReinforcement;

    public abstract void Initialise();
    public abstract void DoUpdate();

    /// <summary>Comparison predicate for <c>List.Sort</c> — lower UpdateAtTime is processed first.</summary>
    public static int CompareByUpdateTime(BrainComponent a, BrainComponent b)
        => a.UpdateAtTime.CompareTo(b.UpdateAtTime);
}
