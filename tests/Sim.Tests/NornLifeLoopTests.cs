using System;
using System.IO;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Util;
using C = CreaturesReborn.Sim.Creature.Creature;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class NornLifeLoopTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    private static C LoadStarter(int seed = 42)
        => C.LoadFromFile(Path.GetFullPath(StarterGenomePath), new Rng(seed));

    [Fact]
    public void EatingStimulus_ReducesCarbHungerAndRewardsCreature()
    {
        var creature = LoadStarter();
        creature.SetChemical(ChemID.HungerForCarb, 0.8f);
        float rewardBefore = creature.GetChemical(ChemID.Reward);

        StimulusTable.Apply(creature, StimulusId.AteFoodSuccess);

        Assert.True(creature.GetChemical(ChemID.HungerForCarb) < 0.8f);
        Assert.True(creature.GetChemical(ChemID.Reward) > rewardBefore);
        Assert.True(creature.GetChemical(ChemID.Glycogen) > 0);
    }

    [Theory]
    [InlineData(FoodKind.Fruit, ChemID.HungerForCarb, ChemID.Glycogen)]
    [InlineData(FoodKind.Seed, ChemID.HungerForFat, ChemID.Adipose)]
    [InlineData(FoodKind.Food, ChemID.HungerForProtein, ChemID.Muscle)]
    public void FoodKindStimulus_TargetsDistinctC3DsNutrition(
        FoodKind kind,
        int hungerChem,
        int storageChem)
    {
        var creature = LoadStarter();
        creature.SetChemical(hungerChem, 0.8f);

        FoodNutrition nutrition = FoodNutrition.ForKind(kind);
        StimulusTable.Apply(creature, nutrition.StimulusId);

        Assert.True(creature.GetChemical(hungerChem) < 0.8f);
        Assert.True(creature.GetChemical(storageChem) > 0);
    }

    [Fact]
    public void WallBumpStimulus_RaisesPainAndFear()
    {
        var creature = LoadStarter();

        StimulusTable.Apply(creature, StimulusId.WallBump);

        Assert.True(creature.GetChemical(ChemID.Pain) > 0);
        Assert.True(creature.GetChemical(ChemID.Fear) > 0);
    }

    [Fact]
    public void LaidEggStimulus_BurnsProgesteroneAndRewardsCreature()
    {
        var creature = LoadStarter();
        creature.SetChemical(ChemID.Progesterone, 0.9f);
        float rewardBefore = creature.GetChemical(ChemID.Reward);

        StimulusTable.Apply(creature, StimulusId.LaidEgg);

        Assert.True(creature.GetChemical(ChemID.Progesterone) < 0.9f);
        Assert.True(creature.GetChemical(ChemID.Reward) > rewardBefore);
    }

    [Fact]
    public void StarterNorn_WithSameSeedTicksDeterministically()
    {
        var a = LoadStarter(seed: 99);
        var b = LoadStarter(seed: 99);
        a.SetChemical(ChemID.ATP, 1.0f);
        b.SetChemical(ChemID.ATP, 1.0f);

        for (int i = 0; i < 500; i++)
        {
            a.Tick();
            b.Tick();
        }

        Assert.Equal(a.Motor.CurrentVerb, b.Motor.CurrentVerb);
        Assert.Equal(a.GetChemical(ChemID.ATP), b.GetChemical(ChemID.ATP), precision: 6);
        Assert.Equal(a.GetChemical(ChemID.HungerForCarb), b.GetChemical(ChemID.HungerForCarb), precision: 6);
    }
}
