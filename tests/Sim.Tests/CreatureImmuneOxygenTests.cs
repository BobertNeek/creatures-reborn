using System;
using System.IO;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Util;
using C = CreaturesReborn.Sim.Creature.Creature;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class CreatureImmuneOxygenTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    private static C LoadStarter(int seed = 90)
        => C.LoadFromFile(Path.GetFullPath(StarterGenomePath), new Rng(seed));

    [Fact]
    public void LowAirQuality_ReducesOxygenAndRaisesSuffocationStress()
    {
        var creature = LoadStarter();
        creature.SetChemical(ChemID.ATP, 1.0f);
        creature.SetChemical(ChemID.ADP, 0.0f);
        creature.SetChemical(ChemID.Oxygen, 0.8f);
        creature.SetChemical(ChemID.Punishment, 0.0f);
        creature.SetChemical(ChemID.Fear, 0.0f);
        var trace = new BiochemistryTrace();

        float suffocation = creature.ApplyAirQuality(0.05f, trace);
        creature.Biochemistry.Update(trace);

        Assert.True(suffocation > 0.0f);
        Assert.True(creature.GetChemical(ChemID.Oxygen) < 0.8f);
        Assert.True(creature.GetChemical(ChemID.ATP) < 1.0f);
        Assert.True(creature.GetChemical(ChemID.ADP) > 0.0f);
        Assert.True(creature.GetChemical(ChemID.Punishment) > 0.0f);
        Assert.Equal(
            0.05f,
            creature.Biochemistry.GetCreatureLocus((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.AirQuality).Value,
            precision: 6);
        Assert.Contains(trace.Deltas, d =>
            d.Source == ChemicalDeltaSource.Respiration &&
            d.ChemicalId == ChemID.Oxygen &&
            d.Amount < 0.0f);
    }

    [Fact]
    public void AtpDecoupler_ConvertsAtpToAdpAndTracesToxin()
    {
        var creature = LoadStarter(seed: 91);
        creature.SetChemical(ChemID.ATP, 1.0f);
        creature.SetChemical(ChemID.ADP, 0.0f);
        creature.SetChemical(ChemID.AtpDecoupler, 0.7f);
        var trace = new BiochemistryTrace();

        creature.Biochemistry.Update(trace);

        Assert.True(creature.GetChemical(ChemID.ATP) < 1.0f);
        Assert.True(creature.GetChemical(ChemID.ADP) > 0.0f);
        Assert.Contains(trace.Deltas, d =>
            d.Source == ChemicalDeltaSource.Toxin &&
            d.ChemicalId == ChemID.ATP &&
            d.Amount < 0.0f);
    }

    [Fact]
    public void MatchingAntibody_ReducesAntigenAndTracesImmuneResponse()
    {
        var creature = LoadStarter(seed: 92);
        creature.SetChemical(ChemID.FirstAntigen, 0.6f);
        creature.SetChemical(ChemID.FirstAntibody, 0.6f);
        var trace = new BiochemistryTrace();

        creature.Biochemistry.Update(trace);

        Assert.True(creature.GetChemical(ChemID.FirstAntigen) < 0.6f);
        Assert.True(creature.GetChemical(ChemID.FirstAntibody) < 0.6f);
        Assert.Contains(trace.Deltas, d =>
            d.Source == ChemicalDeltaSource.Immune &&
            d.ChemicalId == ChemID.FirstAntigen &&
            d.Amount < 0.0f);
    }

    [Fact]
    public void AntigenSideEffects_AreTraceableAndConservative()
    {
        var creature = LoadStarter(seed: 93);
        creature.SetChemical(ChemID.Antigen5, 0.8f);
        creature.SetChemical(ChemID.Wounded, 0.0f);
        var trace = new BiochemistryTrace();

        creature.Biochemistry.Update(trace);

        Assert.True(creature.GetChemical(ChemID.Wounded) > 0.0f);
        Assert.Contains(trace.Deltas, d =>
            d.Source == ChemicalDeltaSource.Immune &&
            d.ChemicalId == ChemID.Wounded &&
            d.Amount > 0.0f);
    }

    [Fact]
    public void ToxinLoad_QueuesOrganStressAndRaisesInjury()
    {
        var creature = LoadStarter(seed: 94);
        Organ organ = creature.Biochemistry.GetOrgan(0);
        float lifeForceBefore = organ.CreateSnapshot(0).ShortTermLifeForce;
        creature.SetChemical(ChemID.ATP, 0.0f);
        creature.SetChemical(ChemID.Endorphin, 0.0f);
        creature.SetChemical(ChemID.Muscle, 0.8f);
        creature.SetChemical(ChemID.MuscleToxin, 0.9f);
        var trace = new BiochemistryTrace();

        creature.Biochemistry.Update(trace);
        creature.Biochemistry.Update(trace);

        Assert.True(creature.GetChemical(ChemID.Muscle) < 0.8f);
        Assert.True(creature.GetChemical(ChemID.Injury) > 0.0f);
        Assert.True(organ.CreateSnapshot(0).ShortTermLifeForce < lifeForceBefore);
        Assert.Contains(trace.Deltas, d =>
            d.Source == ChemicalDeltaSource.Toxin &&
            d.ChemicalId == ChemID.Injury &&
            d.Amount > 0.0f);
    }
}
