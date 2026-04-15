namespace CreaturesReborn.Sim.Brain;

/// <summary>
/// Extensibility seam for pluggable brain modules.
///
/// The SVRule lobe stack is the default implementation, built by <see cref="Brain.ReadFromGenome"/>.
/// A future hierarchical / attention / episodic-memory brain can implement this interface and be
/// registered via <see cref="Brain.RegisterModule"/> to run alongside or in place of specific lobes.
///
/// Usage:
/// <code>
///   brain.RegisterModule(new MyCustomModule());
/// </code>
/// The module's <see cref="Update"/> is called once per world tick, after all SVRule lobe components
/// have been updated (or instead of the shadowed lobe, if <see cref="ShadowedLobeToken"/> is set).
/// </summary>
public interface IBrainModule
{
    /// <summary>
    /// Called once, immediately after <see cref="Brain.ReadFromGenome"/> has fully initialised the
    /// lobe stack. Use this to cache references to specific lobes or neurons.
    /// </summary>
    void Initialise(Brain brain);

    /// <summary>
    /// Called once per world tick. Can read and write neuron states directly via
    /// <see cref="Brain.GetLobe"/> / <see cref="Lobe.GetNeuronState"/> /
    /// <see cref="Lobe.SetInput"/>.
    /// </summary>
    void Update(Brain brain);

    /// <summary>
    /// When non-null, the brain will skip the normal <see cref="BrainComponent.DoUpdate"/> for the
    /// lobe whose <see cref="Lobe.Token"/> matches this value, calling this module's
    /// <see cref="Update"/> instead.  Set to <c>null</c> to run in addition to the lobe stack.
    /// </summary>
    int? ShadowedLobeToken { get; }
}
