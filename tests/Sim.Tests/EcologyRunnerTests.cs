using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Lab;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class EcologyRunnerTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void EcologyRunner_ReplaysDeterministicallyWithSameSeed()
    {
        EcologyRunConfig config = CreateConfig(seed: 600, generations: 2, ticks: 5);
        var runner = new EcologyRunner();

        EcologyRunResult first = runner.Run(config);
        EcologyRunResult second = runner.Run(config);

        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(
            first.Generations.Select(generation => generation.FinalPopulation),
            second.Generations.Select(generation => generation.FinalPopulation));
        Assert.Equal(
            first.Journal.Events.Select(e => (e.Tick, e.Kind, e.Moniker, e.Detail)),
            second.Journal.Events.Select(e => (e.Tick, e.Kind, e.Moniker, e.Detail)));
    }

    [Fact]
    public void EcologyRunner_HandlesPopulationCapExtinction()
    {
        EcologyRunConfig config = CreateConfig(seed: 601, generations: 4, ticks: 5) with
        {
            PopulationPolicy = new EcologyPopulationPolicy(PopulationCap: 0)
        };

        EcologyRunResult result = new EcologyRunner().Run(config);

        Assert.True(result.Summary.Extinct);
        Assert.Equal(0, result.Summary.GenerationsRun);
        Assert.Empty(result.Generations);
    }

    [Fact]
    public void EcologyRunner_RecordsMultiGenerationEvolutionJournal()
    {
        EcologyRunConfig config = CreateConfig(seed: 602, generations: 2, ticks: 3);

        EcologyRunResult result = new EcologyRunner().Run(config);

        Assert.Equal(2, result.Summary.GenerationsRun);
        Assert.True(result.Summary.LivingPopulation > 0);
        Assert.True(result.Summary.ReproductionCount >= 1);
        Assert.Contains(result.Journal.Events, e => e.Kind == NaturalSelectionEventKind.Birth);
        Assert.Contains(result.Journal.Events, e => e.Kind == NaturalSelectionEventKind.Reproduction);
        Assert.Equal(result.Generations.Sum(g => g.EvolutionJournal.SurvivalFrames.Count), result.Journal.SurvivalFrames.Count);
    }

    private static EcologyRunConfig CreateConfig(int seed, int generations, int ticks)
        => new(
            Seed: seed,
            Generations: generations,
            TicksPerGeneration: ticks,
            Founders:
            [
                new LabCreatureSeed(Path.GetFullPath(StarterGenomePath), "eco-mum", GeneConstants.FEMALE),
                new LabCreatureSeed(Path.GetFullPath(StarterGenomePath), "eco-dad", GeneConstants.MALE)
            ]);
}
