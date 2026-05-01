using System;
using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Brain;

public sealed record LobeAcceleratorState(
    int Token,
    int Width,
    int Height,
    bool RunInitRuleAlways,
    int WinningNeuronId,
    float[] NeuronStates,
    float[] NeuronInputs,
    IReadOnlyList<SVRuleEntrySnapshot> InitRule,
    IReadOnlyList<SVRuleEntrySnapshot> UpdateRule)
{
    public int NeuronCount => Width * Height;

    public static bool RulesCanRunDeterministically(
        IEnumerable<SVRuleEntrySnapshot> initRule,
        IEnumerable<SVRuleEntrySnapshot> updateRule,
        out string? reason)
    {
        foreach (SVRuleEntrySnapshot entry in initRule.Concat(updateRule))
        {
            if (entry.Operand == SVRule.Operand.Random)
            {
                reason = "SVRule reads the random operand.";
                return false;
            }

            if (entry.Operation == SVRule.Op.DoSignalNoise)
            {
                reason = "SVRule uses signal-noise RNG.";
                return false;
            }
        }

        reason = null;
        return true;
    }

    public bool CanRunDeterministically(out string? reason)
        => RulesCanRunDeterministically(InitRule, UpdateRule, out reason);

    public void ValidateResult(float[] neuronStates)
    {
        int expected = NeuronCount * BrainConst.NumSVRuleVariables;
        if (neuronStates.Length != expected)
            throw new ArgumentException($"Expected {expected} neuron state values, got {neuronStates.Length}.", nameof(neuronStates));
    }
}
