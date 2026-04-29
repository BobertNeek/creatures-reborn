using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Util;
using Xunit;
using C = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Sim.Tests;

public class CreatureTraceTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void TickWithTrace_PreservesCoreOutputs()
    {
        var plain = LoadStarter(seed: 77);
        var traced = LoadStarter(seed: 77);
        plain.SetChemical(ChemID.ATP, 1.0f);
        traced.SetChemical(ChemID.ATP, 1.0f);

        plain.Tick();
        CreatureTickTrace trace = traced.Tick(new CreatureTraceOptions(
            IncludeBiochemistryTrace: true,
            IncludeBrainSnapshot: true));

        Assert.Equal(plain.Motor.CurrentVerb, traced.Motor.CurrentVerb);
        Assert.Equal(plain.Motor.CurrentNoun, traced.Motor.CurrentNoun);
        Assert.Equal(plain.GetChemical(ChemID.ATP), traced.GetChemical(ChemID.ATP), precision: 6);
        Assert.NotNull(trace.Biochemistry);
        Assert.NotNull(trace.BrainAfter);
    }

    [Fact]
    public void TickTrace_RecordsStablePipelineStages()
    {
        var creature = LoadStarter(seed: 88);

        CreatureTickTrace trace = creature.Tick(new CreatureTraceOptions());

        Assert.Equal(new[]
        {
            CreatureTickStage.Context,
            CreatureTickStage.Drives,
            CreatureTickStage.Biochemistry,
            CreatureTickStage.Brain,
            CreatureTickStage.Learning,
            CreatureTickStage.Motor,
            CreatureTickStage.Action,
            CreatureTickStage.Reproduction,
            CreatureTickStage.Age
        }, trace.Stages.Select(stage => stage.Stage).ToArray());
    }

    [Fact]
    public void CreatureTraceCollector_CapturesMotorAndAgeTransition()
    {
        var creature = LoadStarter(seed: 99);
        byte ageBefore = creature.Genome.Age;

        CreatureTickTrace trace = creature.Tick(new CreatureTraceOptions());

        Assert.Equal(ageBefore, trace.AgeBefore);
        Assert.Equal(creature.Genome.Age, trace.AgeAfter);
        Assert.Equal(creature.Motor.CurrentVerb, trace.MotorVerb);
        Assert.Equal(creature.Motor.CurrentNoun, trace.MotorNoun);
    }

    [Fact]
    public void TickWithLearningTrace_ProvidesLearningTraceContainer()
    {
        var creature = LoadStarter(seed: 100);

        CreatureTickTrace trace = creature.Tick(new CreatureTraceOptions(IncludeLearningTrace: true));

        Assert.NotNull(trace.Learning);
    }

    [Fact]
    public void TickWithChemicalReinforcementTrace_ProjectsBiochemistryDeltas()
    {
        var creature = LoadStarter(seed: 101);
        creature.SetChemical(ChemID.HungerForCarb, 0.8f);
        creature.SetChemical(ChemID.Glycogen, 1.0f);

        CreatureTickTrace trace = creature.Tick(new CreatureTraceOptions(IncludeChemicalReinforcementTrace: true));

        Assert.NotNull(trace.Biochemistry);
        Assert.NotNull(trace.ChemicalReinforcement);
        Assert.Contains(trace.ChemicalReinforcement!.Signals, signal => signal.Domain == ChemicalReinforcementDomain.Hunger);
    }

    private static C LoadStarter(int seed)
        => C.LoadFromFile(Path.GetFullPath(StarterGenomePath), new Rng(seed));
}
