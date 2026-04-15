namespace CreaturesReborn.Sim.Brain;

/// <summary>
/// One brain neuron: holds an 8-variable state vector operated on by SVRules.
/// Direct port of c2e's <c>Neuron</c> struct (Neuron.h).
/// </summary>
public sealed class Neuron
{
    /// <summary>Index of this neuron within its parent lobe's neuron list.</summary>
    public int IdInList;

    /// <summary>
    /// 8-variable state vector: [State, Input, Output, Third, Fourth, Fifth, Sixth, NGF].
    /// Indices defined in <see cref="NeuronVar"/>.
    /// </summary>
    public readonly float[] States = new float[BrainConst.NumSVRuleVariables];

    public void ClearStates()
    {
        for (int i = 0; i < BrainConst.NumSVRuleVariables; i++)
            States[i] = 0.0f;
    }
}
