using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Util;
using Xunit;
using C = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Sim.Tests;

public class BiochemistryObservabilityTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void ChemicalCatalog_NamesKnownChemicalsAndProvidesFallbacksForAllSlots()
    {
        Assert.Equal(BiochemConst.NUMCHEM, ChemicalCatalog.All.Count);

        ChemicalDefinition reward = ChemicalCatalog.Get(ChemID.Reward);
        Assert.Equal("reward", reward.Token);
        Assert.Equal("Reward", reward.DisplayName);
        Assert.Equal(ChemicalCategory.Reinforcement, reward.Category);
        Assert.Equal(0.0f, reward.Range.HardMin);
        Assert.Equal(1.0f, reward.Range.HardMax);

        ChemicalDefinition fallback = ChemicalCatalog.Get(150);
        Assert.Equal("chem150", fallback.Token);
        Assert.Equal(ChemicalCategory.Unknown, fallback.Category);
    }

    [Fact]
    public void HalfLifeMetadata_ReflectsCurrentDecayRatesWithoutChangingChemistry()
    {
        var biochemistry = new Biochemistry.Biochemistry();

        ChemicalHalfLifeView view = biochemistry.GetHalfLifeView(ChemID.ATP);

        Assert.Equal(ChemID.ATP, view.ChemicalId);
        Assert.Equal("ATP", view.Chemical.DisplayName);
        Assert.Equal(0.0f, view.DecayRate);
        Assert.False(view.HasDecay);
    }

    [Fact]
    public void OrganAndReactionViewsExposeCurrentOrganState()
    {
        var organ = new Organ();

        OrganSnapshot snapshot = organ.CreateSnapshot(index: 3);
        var reactions = organ.GetReactionDefinitionViews();

        Assert.Equal(3, snapshot.Index);
        Assert.True(snapshot.InitialLifeForce > 0);
        Assert.Equal(0, snapshot.ReactionCount);
        Assert.Equal(0, snapshot.EmitterCount);
        Assert.Equal(0, snapshot.ReceptorCount);
        Assert.Empty(reactions);
    }

    [Fact]
    public void BiochemistryTrace_DisabledUpdateMatchesEnabledTraceUpdate()
    {
        var plain = new Biochemistry.Biochemistry();
        var traced = new Biochemistry.Biochemistry();
        plain.SetChemical(ChemID.ATP, 0.75f);
        traced.SetChemical(ChemID.ATP, 0.75f);

        plain.Update();
        var trace = new BiochemistryTrace();
        traced.Update(trace);

        Assert.Equal(plain.GetChemical(ChemID.ATP), traced.GetChemical(ChemID.ATP), precision: 6);
        Assert.Contains(trace.Deltas, d => d.ChemicalId == ChemID.ATP && d.Source == ChemicalDeltaSource.HalfLifeDecay);
    }

    [Fact]
    public void TraceCapturesDirectChemicalChangesWithBeforeAndAfterValues()
    {
        var biochemistry = new Biochemistry.Biochemistry();
        var trace = new BiochemistryTrace();

        biochemistry.AddChemical(ChemID.Pain, 0.25f, ChemicalDeltaSource.DirectAdd, "test add", trace);
        biochemistry.SubChemical(ChemID.Pain, 0.10f, ChemicalDeltaSource.DirectSubtract, "test sub", trace);

        Assert.Equal(0.15f, biochemistry.GetChemical(ChemID.Pain), precision: 6);
        Assert.Equal(2, trace.Deltas.Count);
        Assert.Equal(0.0f, trace.Deltas[0].Before);
        Assert.Equal(0.25f, trace.Deltas[0].After);
        Assert.Equal(0.25f, trace.Deltas[1].Before);
        Assert.Equal(0.15f, trace.Deltas[1].After);
    }

    [Fact]
    public void StimulusTrace_RecordsEachChemicalInjection()
    {
        var creature = LoadStarter();
        creature.SetChemical(ChemID.HungerForCarb, 0.8f);
        var trace = new BiochemistryTrace();

        StimulusTable.Apply(creature, StimulusId.AteFoodSuccess, trace);

        Assert.Contains(trace.Deltas, d => d.ChemicalId == ChemID.Glycogen && d.Source == ChemicalDeltaSource.Stimulus);
        Assert.Contains(trace.Deltas, d => d.ChemicalId == ChemID.Reward && d.Source == ChemicalDeltaSource.Stimulus);
        Assert.Contains(trace.Deltas, d => d.ChemicalId == ChemID.HungerForCarb && d.Source == ChemicalDeltaSource.Stimulus);
        Assert.True(creature.GetChemical(ChemID.HungerForCarb) < 0.8f);
    }

    private static C LoadStarter()
        => C.LoadFromFile(Path.GetFullPath(StarterGenomePath), new Rng(42));
}
