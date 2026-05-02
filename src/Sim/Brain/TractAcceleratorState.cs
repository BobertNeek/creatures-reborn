using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.Brain;

public sealed record TractAcceleratorState(
    int UpdateAtTime,
    int SourceToken,
    int DestinationToken,
    bool RunInitRuleAlways,
    bool RandomConnectAndMigrate,
    bool RewardSupported,
    bool PunishmentSupported,
    float STtoLTRate,
    int SourceWinningNeuronId,
    float[] SourceNeuronStates,
    float[] DestinationNeuronStates,
    int[] SourceNeuronIds,
    int[] DestinationNeuronIds,
    float[] DendriteWeights,
    IReadOnlyList<SVRuleEntrySnapshot> InitRule,
    IReadOnlyList<SVRuleEntrySnapshot> UpdateRule)
{
    public int SourceNeuronCount => SourceNeuronStates.Length / BrainConst.NumSVRuleVariables;
    public int DestinationNeuronCount => DestinationNeuronStates.Length / BrainConst.NumSVRuleVariables;
    public int DendriteCount => SourceNeuronIds.Length;

    public static bool CanRunDeterministically(
        bool randomConnectAndMigrate,
        int sourceToken,
        int destinationToken,
        IEnumerable<SVRuleEntrySnapshot> initRule,
        IEnumerable<SVRuleEntrySnapshot> updateRule,
        out string? reason)
    {
        if (randomConnectAndMigrate)
        {
            reason = "Tract uses dendrite migration.";
            return false;
        }

        if (sourceToken == destinationToken)
        {
            reason = "Tract source and destination lobes alias the same state buffer.";
            return false;
        }

        foreach (SVRuleEntrySnapshot entry in initRule)
        {
            if (IsReinforcementConfigurationOperation(entry.Operation))
            {
                reason = "Tract configures chemical reinforcement.";
                return false;
            }
        }

        foreach (SVRuleEntrySnapshot entry in updateRule)
        {
            if (IsReinforcementConfigurationOperation(entry.Operation))
            {
                reason = "Tract configures chemical reinforcement.";
                return false;
            }
        }

        return LobeAcceleratorState.RulesCanRunDeterministically(initRule, updateRule, out reason);
    }

    public bool CanRunDeterministically(out string? reason)
    {
        if (RewardSupported || PunishmentSupported)
        {
            reason = "Tract has active chemical reinforcement.";
            return false;
        }

        return CanRunDeterministically(
            RandomConnectAndMigrate,
            SourceToken,
            DestinationToken,
            InitRule,
            UpdateRule,
            out reason);
    }

    public void ValidateResult(float[] sourceNeuronStates, float[] destinationNeuronStates, float[] dendriteWeights)
    {
        if (sourceNeuronStates.Length != SourceNeuronStates.Length)
            throw new ArgumentException($"Expected {SourceNeuronStates.Length} source neuron state values, got {sourceNeuronStates.Length}.", nameof(sourceNeuronStates));
        if (destinationNeuronStates.Length != DestinationNeuronStates.Length)
            throw new ArgumentException($"Expected {DestinationNeuronStates.Length} destination neuron state values, got {destinationNeuronStates.Length}.", nameof(destinationNeuronStates));
        if (dendriteWeights.Length != DendriteWeights.Length)
            throw new ArgumentException($"Expected {DendriteWeights.Length} dendrite weight values, got {dendriteWeights.Length}.", nameof(dendriteWeights));
    }

    private static bool IsReinforcementConfigurationOperation(SVRule.Op operation)
        => operation is SVRule.Op.SetRewardThreshold
            or SVRule.Op.SetRewardRate
            or SVRule.Op.SetRewardChemicalIndex
            or SVRule.Op.SetPunishmentThreshold
            or SVRule.Op.SetPunishmentRate
            or SVRule.Op.SetPunishmentChemicalIndex;
}
