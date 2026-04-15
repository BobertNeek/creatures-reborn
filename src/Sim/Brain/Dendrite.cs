namespace CreaturesReborn.Sim.Brain;

/// <summary>
/// One synaptic connection between two neurons: holds an 8-variable weight vector.
/// Direct port of c2e's <c>Dendrite</c> struct (Dendrite.h).
/// </summary>
public sealed class Dendrite
{
    public int     IdInList;
    public Neuron? SrcNeuron;
    public Neuron? DstNeuron;

    /// <summary>
    /// 8-variable weight vector: [ST weight, LT weight, vars 2-6, Strength].
    /// Indices defined in <see cref="DendriteVar"/>.
    /// </summary>
    public readonly float[] Weights = new float[BrainConst.NumSVRuleVariables];

    public Dendrite() { }

    public Dendrite(Neuron src, Neuron dst)
    {
        SrcNeuron = src;
        DstNeuron = dst;
    }

    /// <summary>Run the tract's init SVRule on this dendrite's weight vector.</summary>
    public void InitByRule(SVRule initRule, Tract owner)
    {
        float[] inputVars  = SVRule.InvalidVariables;
        float[] srcStates  = SrcNeuron?.States ?? SVRule.InvalidVariables;
        float[] dstStates  = DstNeuron?.States ?? SVRule.InvalidVariables;
        initRule.Process(
            inputVars, Weights,
            srcStates, dstStates,
            SrcNeuron?.IdInList ?? 0,
            DstNeuron?.IdInList ?? 0,
            owner);
    }

    public void ClearWeights()
    {
        for (int i = 0; i < BrainConst.NumSVRuleVariables; i++)
            Weights[i] = 0.0f;
    }
}
