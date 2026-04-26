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
    public void HungerMetabolism_RaisesHungerWhenStorageIsLowAndTracesChange()
    {
        var creature = LoadStarter();
        creature.SetChemical(ChemID.ATP, 1.0f);
        creature.SetChemical(ChemID.Glycogen, 0.0f);
        creature.SetChemical(ChemID.HungerForCarb, 0.0f);
        var trace = new BiochemistryTrace();

        creature.Biochemistry.Update(trace);

        Assert.True(creature.GetChemical(ChemID.HungerForCarb) > 0.05f);
        Assert.Contains(trace.Deltas, d =>
            d.ChemicalId == ChemID.HungerForCarb &&
            d.Source == ChemicalDeltaSource.Metabolism &&
            d.Amount > 0);
    }

    [Fact]
    public void HungerMetabolism_ConvertsStorageToAtpWhenEnergyIsLow()
    {
        var creature = LoadStarter();
        creature.SetChemical(ChemID.ATP, 0.02f);
        creature.SetChemical(ChemID.Glycogen, 0.8f);
        var trace = new BiochemistryTrace();

        creature.Biochemistry.Update(trace);

        Assert.True(creature.GetChemical(ChemID.ATP) > 0.02f);
        Assert.True(creature.GetChemical(ChemID.Glycogen) < 0.8f);
        Assert.Contains(trace.Deltas, d =>
            d.ChemicalId == ChemID.ATP &&
            d.Source == ChemicalDeltaSource.Metabolism &&
            d.Amount > 0);
    }

    [Fact]
    public void EatingReward_IsStrongerWhenHungryThanWhenFull()
    {
        var hungry = LoadStarter(seed: 50);
        hungry.SetChemical(ChemID.HungerForCarb, 0.9f);
        hungry.SetChemical(ChemID.Glycogen, 0.0f);

        var full = LoadStarter(seed: 50);
        full.SetChemical(ChemID.HungerForCarb, 0.05f);
        full.SetChemical(ChemID.Glycogen, 0.95f);

        var hungryTrace = new BiochemistryTrace();
        var fullTrace = new BiochemistryTrace();

        StimulusTable.Apply(hungry, StimulusId.AteFruit, hungryTrace);
        StimulusTable.Apply(full, StimulusId.AteFruit, fullTrace);

        Assert.True(hungry.GetChemical(ChemID.Reward) > full.GetChemical(ChemID.Reward));
        Assert.Contains(hungryTrace.Deltas, d =>
            d.ChemicalId == ChemID.Reward &&
            d.Source == ChemicalDeltaSource.Stimulus &&
            d.Detail == "nutrition:ate_fruit:reward");
        Assert.Contains(fullTrace.Deltas, d =>
            d.ChemicalId == ChemID.HungerForCarb &&
            d.Source == ChemicalDeltaSource.Stimulus &&
            d.Detail == "nutrition:ate_fruit:hunger");
    }

    [Fact]
    public void FatigueMetabolism_RaisesTirednessWhileAwake()
    {
        var creature = LoadStarter(seed: 60);
        creature.SetChemical(ChemID.ATP, 1.0f);
        creature.SetChemical(ChemID.Tiredness, 0.0f);
        creature.SetChemical(ChemID.Sleepiness, 0.0f);
        var trace = new BiochemistryTrace();

        creature.Biochemistry.Update(trace);

        Assert.True(creature.GetChemical(ChemID.Tiredness) > 0.0f);
        Assert.Equal(0.0f, creature.Biochemistry.GetCreatureLocus((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Asleep).Value);
        Assert.Contains(trace.Deltas, d =>
            d.ChemicalId == ChemID.Tiredness &&
            d.Source == ChemicalDeltaSource.Fatigue &&
            d.Amount > 0);
    }

    [Fact]
    public void SleepPressure_DerivesAsleepLocusAndRecoversChemistry()
    {
        var creature = LoadStarter(seed: 61);
        creature.SetChemical(ChemID.ATP, 0.0f);
        creature.SetChemical(ChemID.Tiredness, 0.9f);
        creature.SetChemical(ChemID.Sleepiness, 0.9f);
        var trace = new BiochemistryTrace();

        creature.Biochemistry.Update(trace);

        Assert.Equal(1.0f, creature.Biochemistry.GetCreatureLocus((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Asleep).Value);
        Assert.Equal(1.0f, creature.Biochemistry.GetCreatureLocus((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Involuntary0 + 5).Value);
        Assert.True(creature.GetChemical(ChemID.Tiredness) < 0.9f);
        Assert.True(creature.GetChemical(ChemID.Sleepiness) < 0.9f);
        Assert.True(creature.GetChemical(ChemID.ATP) > 0.0f);
        Assert.Contains(trace.Deltas, d =>
            d.ChemicalId == ChemID.Sleepiness &&
            d.Source == ChemicalDeltaSource.Fatigue &&
            d.Amount < 0);
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
