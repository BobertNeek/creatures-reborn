using System.Collections.Generic;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;

namespace CreaturesReborn.Sim.Creature;

public enum CreatureTickStage
{
    Context,
    Drives,
    Biochemistry,
    Brain,
    Learning,
    Motor,
    Action,
    Reproduction,
    Age
}

public sealed record CreatureTraceOptions(
    bool IncludeBiochemistryTrace = false,
    bool IncludeBrainSnapshot = false,
    bool IncludeLearningTrace = false);

public sealed record CreatureTickStageRecord(CreatureTickStage Stage, string Detail);

public sealed class CreatureTickTrace
{
    private readonly List<CreatureTickStageRecord> _stages = new();

    public IReadOnlyList<CreatureTickStageRecord> Stages => _stages;
    public BiochemistryTrace? Biochemistry { get; internal set; }
    public LearningTrace? Learning { get; internal set; }
    public BrainSnapshot? BrainBefore { get; internal set; }
    public BrainSnapshot? BrainAfter { get; internal set; }
    public int MotorVerb { get; internal set; }
    public int MotorNoun { get; internal set; }
    public byte AgeBefore { get; internal set; }
    public byte AgeAfter { get; internal set; }

    public void Record(CreatureTickStage stage, string detail)
        => _stages.Add(new CreatureTickStageRecord(stage, detail));
}

public sealed class CreatureTraceCollector
{
    public CreatureTraceCollector(CreatureTraceOptions options)
    {
        Options = options;
        Trace = new CreatureTickTrace();
    }

    public CreatureTraceOptions Options { get; }
    public CreatureTickTrace Trace { get; }

    public void Record(CreatureTickStage stage, string detail)
        => Trace.Record(stage, detail);
}
