using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using Xunit;
using B = CreaturesReborn.Sim.Brain.Brain;

namespace CreaturesReborn.Sim.Tests;

public class BrainLearningTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void BrainUpdateWithLearningTrace_CapturesRewardReinforcement()
    {
        var (brain, chemicals) = LoadBrain(seed: 100);
        EnableRewardTracing(brain, rate: 0.2f);
        chemicals[ChemID.Reward] = 1.0f;
        var trace = new LearningTrace();

        UpdateConfiguredTracts(brain, trace);

        ReinforcementTrace reward = trace.Reinforcements.First(
            r => r.Kind == ReinforcementKind.Reward && r.ChemicalId == ChemID.Reward);
        Assert.True(reward.Level > 0.0f);
        Assert.NotEqual(reward.BeforeWeight, reward.AfterWeight);
        Assert.True(reward.AfterWeight > reward.BeforeWeight);
        Assert.True(reward.DendriteId >= 0);
        Assert.True(reward.SourceNeuronId >= 0);
        Assert.True(reward.DestinationNeuronId >= 0);
    }

    [Fact]
    public void BrainUpdateWithLearningTrace_CapturesPunishmentReinforcement()
    {
        var (brain, chemicals) = LoadBrain(seed: 101);
        EnablePunishmentTracing(brain, rate: -0.2f);
        chemicals[ChemID.Punishment] = 1.0f;
        var trace = new LearningTrace();

        UpdateConfiguredTracts(brain, trace);

        ReinforcementTrace punishment = trace.Reinforcements.First(
            r => r.Kind == ReinforcementKind.Punishment && r.ChemicalId == ChemID.Punishment);
        Assert.True(punishment.Level > 0.0f);
        Assert.NotEqual(punishment.BeforeWeight, punishment.AfterWeight);
        Assert.True(punishment.AfterWeight < punishment.BeforeWeight);
    }

    [Fact]
    public void BrainUpdateWithLearningTrace_MatchesUntracedBrainOutput()
    {
        var (plain, plainChemicals) = LoadBrain(seed: 102);
        var (traced, tracedChemicals) = LoadBrain(seed: 102);
        EnableRewardTracing(plain, rate: 0.2f);
        EnableRewardTracing(traced, rate: 0.2f);
        plainChemicals[ChemID.Reward] = 1.0f;
        tracedChemicals[ChemID.Reward] = 1.0f;
        var trace = new LearningTrace();

        UpdateConfiguredTracts(plain, trace: null);
        UpdateConfiguredTracts(traced, trace);

        Assert.Equal(
            plain.CreateSnapshot(new BrainSnapshotOptions(MaxNeuronsPerLobe: 1, MaxDendritesPerTract: 1))
                .Tracts[0].Dendrites[0].Weights[DendriteVar.WeightST],
            traced.CreateSnapshot(new BrainSnapshotOptions(MaxNeuronsPerLobe: 1, MaxDendritesPerTract: 1))
                .Tracts[0].Dendrites[0].Weights[DendriteVar.WeightST],
            precision: 6);
        Assert.NotEmpty(trace.Reinforcements);
    }

    private static (B Brain, float[] Chemicals) LoadBrain(int seed)
    {
        var genome = GenomeReader.LoadNew(new Rng(seed), Path.GetFullPath(StarterGenomePath));
        var brain = new B();
        brain.ReadFromGenome(genome, new Rng(seed));
        var chemicals = new float[BiochemConst.NUMCHEM];
        chemicals[ChemID.ATP] = 1.0f;
        brain.RegisterBiochemistry(chemicals);
        return (brain, chemicals);
    }

    private static void UpdateConfiguredTracts(B brain, LearningTrace? trace)
    {
        SetAllNeuronOutputs(brain, 1.0f);
        for (int i = 0; i < brain.TractCount; i++)
        {
            Tract? tract = brain.GetTract(i);
            tract?.DoUpdate(trace, tick: 0);
        }
    }

    private static void SetAllNeuronOutputs(B brain, float value)
    {
        for (int i = 0; i < brain.LobeCount; i++)
        {
            Lobe? lobe = brain.GetLobe(i);
            if (lobe == null) continue;
            for (int n = 0; n < lobe.GetNoOfNeurons(); n++)
                lobe.GetNeuron(n).States[NeuronVar.Output] = value;
        }
    }

    private static void EnableRewardTracing(B brain, float rate)
    {
        for (int i = 0; i < brain.TractCount; i++)
        {
            Tract? tract = brain.GetTract(i);
            if (tract == null) continue;
            tract.Reward.SetSupported(true);
            tract.Reward.SetThreshold(0.0f);
            tract.Reward.SetRate(rate);
            tract.Reward.SetChemIndex(ChemID.Reward);
        }
    }

    private static void EnablePunishmentTracing(B brain, float rate)
    {
        for (int i = 0; i < brain.TractCount; i++)
        {
            Tract? tract = brain.GetTract(i);
            if (tract == null) continue;
            tract.Punishment.SetSupported(true);
            tract.Punishment.SetThreshold(0.0f);
            tract.Punishment.SetRate(rate);
            tract.Punishment.SetChemIndex(ChemID.Punishment);
        }
    }
}
