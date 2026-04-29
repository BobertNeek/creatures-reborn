using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;

namespace CreaturesReborn.Sim.Brain;

public enum BrainLearningMode
{
    ClassicOnly = 0,
    ChemicalBusClassic,
    ChemicalBusModules,
    TrainingOnly
}

public sealed record BrainReinforcementInput(
    BrainLearningMode Mode,
    IReadOnlyList<ChemicalReinforcementSignal> Signals,
    float PositiveStrength,
    float NegativeStrength)
{
    public static BrainReinforcementInput Empty(BrainLearningMode mode)
        => new(mode, [], 0.0f, 0.0f);
}

public sealed record TractReinforcementPolicy(
    BrainLearningMode Mode = BrainLearningMode.ClassicOnly,
    float PositiveScale = 1.0f,
    float NegativeScale = 1.0f);

public sealed record ModuleReinforcementPolicy(
    BrainLearningMode Mode = BrainLearningMode.ClassicOnly,
    bool FeedEnabledModules = false,
    float SignalScale = 1.0f);

public static class ChemicalLearningAdapter
{
    public static BrainReinforcementInput ToBrainInput(
        ChemicalReinforcementTrace? trace,
        BrainLearningMode mode)
    {
        if (trace == null || mode == BrainLearningMode.ClassicOnly)
            return BrainReinforcementInput.Empty(mode);

        IReadOnlyList<ChemicalReinforcementSignal> signals = trace.Signals
            .Where(signal => signal.Strength > 0.0f)
            .ToArray();

        float positive = signals
            .Where(signal => signal.Valence == ChemicalReinforcementValence.Positive)
            .Sum(signal => signal.Strength);
        float negative = signals
            .Where(signal => signal.Valence == ChemicalReinforcementValence.Negative)
            .Sum(signal => signal.Strength);

        return new BrainReinforcementInput(mode, signals, positive, negative);
    }
}
