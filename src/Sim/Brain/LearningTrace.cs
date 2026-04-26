using System.Collections.Generic;

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
    ReinforcementKind Kind);

public sealed record InstinctTrace(int Tick, int RemainingInstincts, bool Fired);

public sealed class LearningTrace
{
    private readonly List<ReinforcementTrace> _reinforcements = new();
    private readonly List<InstinctTrace> _instincts = new();

    public IReadOnlyList<ReinforcementTrace> Reinforcements => _reinforcements;
    public IReadOnlyList<InstinctTrace> Instincts => _instincts;

    public void RecordReinforcement(ReinforcementTrace trace)
        => _reinforcements.Add(trace);

    public void RecordInstinct(InstinctTrace trace)
        => _instincts.Add(trace);
}
