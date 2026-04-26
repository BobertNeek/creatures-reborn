using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using Xunit;
using B = CreaturesReborn.Sim.Brain.Brain;

namespace CreaturesReborn.Sim.Tests;

public class PlasticityBrainModuleTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void PlasticityBrainModule_IsNotRegisteredByDefault()
    {
        var (brain, _) = LoadBrain(seed: 120);

        Assert.DoesNotContain(brain.GetModuleDescriptors(), descriptor => descriptor.Name == nameof(PlasticityBrainModule));
        Assert.DoesNotContain(brain.CreateSnapshot().ModuleSnapshots, snapshot => snapshot.ModuleName == nameof(PlasticityBrainModule));
    }

    [Fact]
    public void DisabledPlasticityBrainModule_DoesNotShadowOrLearn()
    {
        var (brain, _) = LoadBrain(seed: 121);
        int decn = B.TokenFromString("decn");
        var module = new PlasticityBrainModule(new PlasticityBrainModuleOptions
        {
            Enabled = false,
            ShadowedLobeToken = decn,
            SourceNeuronCount = 2,
            TargetNeuronCount = 2,
            LearningRate = 1.0f
        });

        brain.RegisterModule(module);
        SetNeuronState(brain, B.TokenFromString("driv"), 0, 0.75f);
        SetNeuronState(brain, decn, 0, 0.50f);
        module.Update(brain);

        Assert.Null(module.ShadowedLobeToken);
        Assert.All(module.LatestSnapshot.Weights, weight => Assert.Equal(0.0f, weight));
        Assert.Contains(brain.GetModuleDescriptors(), descriptor =>
            descriptor.Name == nameof(PlasticityBrainModule) &&
            descriptor.IsPassive);
    }

    [Fact]
    public void EnabledPlasticityBrainModule_UpdatesWeightsDeterministically()
    {
        var (brainA, _) = LoadBrain(seed: 122);
        var (brainB, _) = LoadBrain(seed: 122);
        var options = new PlasticityBrainModuleOptions
        {
            Enabled = true,
            SourceLobeToken = B.TokenFromString("driv"),
            TargetLobeToken = B.TokenFromString("decn"),
            SourceNeuronCount = 2,
            TargetNeuronCount = 2,
            LearningRate = 0.25f
        };
        var moduleA = new PlasticityBrainModule(options);
        var moduleB = new PlasticityBrainModule(options);
        brainA.RegisterModule(moduleA);
        brainB.RegisterModule(moduleB);

        SetNeuronState(brainA, options.SourceLobeToken, 0, 0.50f);
        SetNeuronState(brainA, options.SourceLobeToken, 1, 0.25f);
        SetNeuronState(brainA, options.TargetLobeToken, 0, 0.40f);
        SetNeuronState(brainA, options.TargetLobeToken, 1, 0.10f);
        SetNeuronState(brainB, options.SourceLobeToken, 0, 0.50f);
        SetNeuronState(brainB, options.SourceLobeToken, 1, 0.25f);
        SetNeuronState(brainB, options.TargetLobeToken, 0, 0.40f);
        SetNeuronState(brainB, options.TargetLobeToken, 1, 0.10f);

        moduleA.Update(brainA);
        moduleB.Update(brainB);

        PlasticityBrainModuleSnapshot snapshot = moduleA.LatestSnapshot;
        Assert.Equal(moduleA.LatestSnapshot.Weights, moduleB.LatestSnapshot.Weights);
        Assert.Equal(0.05f, snapshot.Weights[0], precision: 5);
        Assert.Equal(0.025f, snapshot.Weights[1], precision: 5);
        Assert.Equal(0.0125f, snapshot.Weights[2], precision: 5);
        Assert.Equal(0.00625f, snapshot.Weights[3], precision: 5);
        Assert.All(snapshot.LastWeightDeltas, delta => Assert.True(delta > 0.0f));
    }

    [Fact]
    public void PlasticityBrainModule_CanShadowConfiguredLobeOnlyWhenEnabled()
    {
        var (brain, _) = LoadBrain(seed: 123);
        int decn = B.TokenFromString("decn");
        var module = new PlasticityBrainModule(new PlasticityBrainModuleOptions
        {
            Enabled = true,
            ShadowedLobeToken = decn,
            TargetLobeToken = decn,
            SourceNeuronCount = 1,
            TargetNeuronCount = 1
        });

        brain.RegisterModule(module);

        Assert.Equal(decn, module.ShadowedLobeToken);
        Assert.Contains(brain.GetModuleDescriptors(), descriptor =>
            descriptor.Name == nameof(PlasticityBrainModule) &&
            !descriptor.IsPassive &&
            descriptor.ShadowedLobeTokenText == "decn");
    }

    [Fact]
    public void PlasticityBrainModule_SnapshotsAreCopiedAndExportedThroughBrainSnapshot()
    {
        var (brain, _) = LoadBrain(seed: 124);
        var module = new PlasticityBrainModule(new PlasticityBrainModuleOptions
        {
            Enabled = true,
            SourceNeuronCount = 1,
            TargetNeuronCount = 1,
            LearningRate = 0.50f
        });
        brain.RegisterModule(module);
        SetNeuronState(brain, B.TokenFromString("driv"), 0, 0.25f);
        SetNeuronState(brain, B.TokenFromString("decn"), 0, 0.50f);
        module.Update(brain);

        PlasticityBrainModuleSnapshot first = module.LatestSnapshot;
        first.Weights[0] = 99.0f;
        first.SourceStates[0] = 99.0f;

        PlasticityBrainModuleSnapshot second = module.LatestSnapshot;
        Assert.Equal(0.0625f, second.Weights[0], precision: 5);
        Assert.Equal(0.25f, second.SourceStates[0], precision: 5);

        BrainModuleSnapshot exported = Assert.Single(
            brain.CreateSnapshot().ModuleSnapshots,
            snapshot => snapshot.ModuleName == nameof(PlasticityBrainModule));
        Assert.True(exported.Enabled);
        Assert.Single(exported.Weights);
        Assert.Contains(exported.StateValues, value => value.Name == "source:0" && value.Value == 0.25f);
        Assert.Equal(0.0625f, exported.Weights[0].Value, precision: 5);
    }

    [Fact]
    public void PlasticityBrainModule_RejectsUnboundedStateSizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PlasticityBrainModule(new PlasticityBrainModuleOptions
            {
                Enabled = true,
                SourceNeuronCount = 65,
                TargetNeuronCount = 1
            }));
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

    private static void SetNeuronState(B brain, int lobeToken, int neuronIndex, float value)
    {
        Lobe? lobe = brain.GetLobeByToken(lobeToken);
        Assert.NotNull(lobe);
        Assert.True(lobe.GetNoOfNeurons() > neuronIndex);
        lobe.GetNeuron(neuronIndex).States[NeuronVar.State] = value;
    }
}
