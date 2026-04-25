using System;
using System.IO;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using C = CreaturesReborn.Sim.Creature.Creature;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class NornReproductionTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    private static C LoadStarter(int sex, byte age = 128, int seed = 42)
        => C.LoadFromFile(Path.GetFullPath(StarterGenomePath), new Rng(seed), sex, age);

    [Fact]
    public void ReproductionRules_RequireFemaleMaleAdultPairInRangeAndOffCooldown()
    {
        var female = LoadStarter(GeneConstants.FEMALE);
        var male = LoadStarter(GeneConstants.MALE);
        var baby = LoadStarter(GeneConstants.MALE, age: 0);

        Assert.True(NornReproductionRules.CanLayEgg(female, male, distance: 1.5f, cooldownSeconds: 0));
        Assert.True(NornReproductionRules.CanLayEgg(male, female, distance: 1.5f, cooldownSeconds: 0));
        Assert.False(NornReproductionRules.CanLayEgg(female, female, distance: 1.5f, cooldownSeconds: 0));
        Assert.False(NornReproductionRules.CanLayEgg(female, male, distance: 4.0f, cooldownSeconds: 0));
        Assert.False(NornReproductionRules.CanLayEgg(female, male, distance: 1.5f, cooldownSeconds: 10));
        Assert.False(NornReproductionRules.CanLayEgg(female, baby, distance: 1.5f, cooldownSeconds: 0));
    }

    [Fact]
    public void CreatureAge_AdvancesDeterministicallyAndStopsAtMaximum()
    {
        var creature = LoadStarter(GeneConstants.FEMALE, age: 254);

        creature.AdvanceAge(C.TicksPerAgeStep);
        creature.AdvanceAge(C.TicksPerAgeStep);

        Assert.Equal(255, creature.Genome.Age);
        Assert.Equal(CreatureAgeStage.Senior, creature.AgeStage);
    }

    [Fact]
    public void CreatureLoadFromFile_CanStartVisuallyAdultWithoutDroppingBrainLobes()
    {
        var adult = LoadStarter(GeneConstants.FEMALE, age: 128);

        Assert.Equal(128, adult.Genome.Age);
        Assert.True(adult.Brain.LobeCount > 0);
        Assert.True(adult.Brain.TractCount > 0);
    }
}
