using System.Collections.Generic;
using CreaturesReborn.Sim.Biochemistry;

namespace CreaturesReborn.Sim.Brain;

public enum ReinforcementKind
{
    Reward,
    Punishment
}

public sealed record ReinforcementTrace(
    int Tick,
    int TractIndex,
    int ChemicalId,
    float Level,
    float BeforeWeight,
    float AfterWeight,
    ReinforcementKind Kind)
{
    public int DendriteId { get; init; } = -1;
    public int SourceNeuronId { get; init; } = -1;
    public int DestinationNeuronId { get; init; } = -1;
}

public sealed record InstinctTrace(int Tick, int RemainingInstincts, bool Fired);

public sealed class LearningTrace
{
    private readonly List<ReinforcementTrace> _reinforcements = new();
    private readonly List<InstinctTrace> _instincts = new();
    private readonly List<ChemicalReinforcementSignal> _chemicalSignals = new();

    public IReadOnlyList<ReinforcementTrace> Reinforcements => _reinforcements;
    public IReadOnlyList<InstinctTrace> Instincts => _instincts;
    public IReadOnlyList<ChemicalReinforcementSignal> ChemicalSignals => _chemicalSignals;

    public void RecordReinforcement(ReinforcementTrace trace)
        => _reinforcements.Add(trace);

    public void RecordInstinct(InstinctTrace trace)
        => _instincts.Add(trace);

    public void RecordChemicalReinforcement(BrainReinforcementInput input)
        => _chemicalSignals.AddRange(input.Signals);
}
