using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Lab;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class LabRunnerTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void LabRunner_RunsDeterministicallyWithSameSeed()
    {
        LabRunConfig config = CreateConfig(seed: 140, ticks: 25);
        var runner = new LabRunner();

        LabRunMetrics first = runner.Run(config);
        LabRunMetrics second = runner.Run(config);

        Assert.Equal(first.TicksRun, second.TicksRun);
        Assert.Equal(first.FinalPopulation, second.FinalPopulation);
        Assert.Equal(
            first.Creatures.Select(creature => creature.Behavior.FinalVerb),
            second.Creatures.Select(creature => creature.Behavior.FinalVerb));
        Assert.Equal(
            first.Creatures.Select(creature => creature.Chemicals.Get(ChemID.ATP)),
            second.Creatures.Select(creature => creature.Chemicals.Get(ChemID.ATP)));
    }

    [Fact]
    public void LabRunner_RecordsFounderLineageAndOrganismMetrics()
    {
        LabRunMetrics metrics = new LabRunner().Run(CreateConfig(seed: 141, ticks: 10));

        Assert.Equal(2, metrics.InitialPopulation);
        Assert.Equal(2, metrics.FinalPopulation);
        Assert.Equal(0, metrics.Births);
        Assert.Equal(0, metrics.Deaths);
        Assert.Equal(2, metrics.Lineage.Count);
        Assert.All(metrics.Lineage, lineage =>
        {
            Assert.Equal(0, lineage.Generation);
            Assert.Null(lineage.MotherMoniker);
            Assert.Null(lineage.FatherMoniker);
        });
        Assert.All(metrics.Creatures, creature =>
        {
            Assert.True(creature.Brain.LobeCount > 0);
            Assert.True(creature.Brain.TractCount > 0);
            Assert.Equal(10, creature.Behavior.TicksObserved);
            Assert.Contains(creature.Chemicals.Values, value => value.ChemicalId == ChemID.ATP);
            Assert.Contains(creature.Chemicals.Values, value => value.ChemicalId == ChemID.HungerForCarb);
        });
    }

    [Fact]
    public void LabRunner_BreedsFirstPairOnlyWhenConfigured()
    {
        LabRunConfig config = CreateConfig(seed: 142, ticks: 5) with
        {
            BreedFirstPairOnStart = true
        };

        LabRunMetrics metrics = new LabRunner().Run(config);

        Assert.Equal(1, metrics.Births);
        Assert.Equal(3, metrics.FinalPopulation);
        LineageRecord child = Assert.Single(metrics.Lineage, lineage => lineage.Generation == 1);
        Assert.Equal("lab-child-0001", child.Moniker);
        Assert.Equal("lab-mum", child.MotherMoniker);
        Assert.Equal("lab-dad", child.FatherMoniker);
        Assert.Single(metrics.CrossoverReports);
        Assert.Single(metrics.MutationReports);
    }

    [Fact]
    public void LabRunner_RecordsWorldPresetAndEnvironmentEffects()
    {
        LabRunConfig config = CreateConfig(seed: 143, ticks: 3) with
        {
            WorldPreset = new LabWorldPreset("hot-low-air", Temperature: 1.0f, Light: 0.8f, Radiation: 0.0f, AirQuality: 0.25f)
        };

        LabRunMetrics metrics = new LabRunner().Run(config);

        Assert.Equal("hot-low-air", metrics.WorldPresetName);
        Assert.All(metrics.Creatures, creature =>
        {
            Assert.True(creature.Environment.Hotness > 0.0f);
            Assert.Equal(0.25f, creature.Environment.AirQuality, precision: 6);
            Assert.True(creature.Chemicals.Get(ChemID.Oxygen) < 1.0f);
        });
    }

    [Fact]
    public void LabRunner_RejectsInvalidConfig()
    {
        var runner = new LabRunner();
        LabRunConfig invalid = new(
            Seed: 144,
            Ticks: -1,
            Population: Array.Empty<LabCreatureSeed>());

        Assert.Throws<ArgumentException>(() => runner.Run(invalid));
    }

    private static LabRunConfig CreateConfig(int seed, int ticks)
        => new(
            Seed: seed,
            Ticks: ticks,
            Population: new[]
            {
                new LabCreatureSeed(Path.GetFullPath(StarterGenomePath), "lab-mum", GeneConstants.FEMALE),
                new LabCreatureSeed(Path.GetFullPath(StarterGenomePath), "lab-dad", GeneConstants.MALE)
            });
}
