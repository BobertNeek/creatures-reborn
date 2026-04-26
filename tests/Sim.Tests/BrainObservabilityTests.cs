using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using Xunit;
using B = CreaturesReborn.Sim.Brain.Brain;

namespace CreaturesReborn.Sim.Tests;

public class BrainObservabilityTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void BrainSnapshot_CopiesLobesTractsAndNeuronState()
    {
        var brain = LoadBrain();
        var lobe = brain.GetLobe(0)!;
        lobe.GetNeuron(0).States[NeuronVar.State] = 0.25f;

        BrainSnapshot snapshot = brain.CreateSnapshot();
        lobe.GetNeuron(0).States[NeuronVar.State] = 0.75f;

        Assert.Equal(brain.LobeCount, snapshot.Lobes.Count);
        Assert.Equal(brain.TractCount, snapshot.Tracts.Count);
        Assert.Equal(0.25f, snapshot.Lobes[0].Neurons[0].States[NeuronVar.State], precision: 6);
        Assert.Equal(B.TokenToString(lobe.Token), snapshot.Lobes[0].TokenText);
    }

    [Fact]
    public void BrainSnapshotOptions_CanLimitNeuronAndDendriteSamples()
    {
        var brain = LoadBrain();
        BrainSnapshot snapshot = brain.CreateSnapshot(new BrainSnapshotOptions(MaxNeuronsPerLobe: 2, MaxDendritesPerTract: 3));

        Assert.All(snapshot.Lobes, lobe => Assert.True(lobe.Neurons.Count <= 2));
        Assert.All(snapshot.Tracts, tract => Assert.True(tract.Dendrites.Count <= 3));
    }

    [Fact]
    public void SVRuleDisassembler_DescribesOpcodeEntries()
    {
        var rule = new SVRule();

        var entries = SVRuleDisassembler.Disassemble(rule);

        Assert.Equal(BrainConst.SVRuleLength, entries.Count);
        Assert.Equal(0, entries[0].Index);
        Assert.Equal(SVRule.Op.StopImmediately, entries[0].Operation);
        Assert.Equal(SVRule.Operand.Accumulator, entries[0].Operand);
    }

    [Fact]
    public void BrainPortRegistry_ContainsClassicDriveMotorAndChemicalPorts()
    {
        var registry = BrainPortRegistry.CreateDefault();

        Assert.Contains(registry.Ports, port => port.Name == "drive:0" && port.Kind == BrainPortKind.Drive);
        Assert.Contains(registry.Ports, port => port.Name == "motor:verb" && port.Kind == BrainPortKind.Motor);
        Assert.Contains(registry.Ports, port => port.Name == "chemical:reward" && port.Kind == BrainPortKind.Chemical);
    }

    [Fact]
    public void BrainModuleDescriptor_CanDescribeRegisteredModulesWithoutChangingInterface()
    {
        var brain = LoadBrain();
        var module = new PassiveModule();
        brain.RegisterModule(module);

        var descriptors = brain.GetModuleDescriptors();

        Assert.Contains(descriptors, descriptor => descriptor.Name == nameof(PassiveModule));
        Assert.Contains(descriptors, descriptor => descriptor.ShadowedLobeToken == null);
    }

    [Fact]
    public void LearningTrace_CapturesReinforcementAndInstinctRecords()
    {
        var trace = new LearningTrace();

        trace.RecordReinforcement(new ReinforcementTrace(
            Tick: 12,
            TractIndex: 3,
            ChemicalId: 32,
            Level: 0.8f,
            BeforeWeight: 0.1f,
            AfterWeight: 0.2f,
            Kind: ReinforcementKind.Reward));
        trace.RecordInstinct(new InstinctTrace(Tick: 13, RemainingInstincts: 2, Fired: true));

        Assert.Single(trace.Reinforcements);
        Assert.Single(trace.Instincts);
        Assert.Equal(ReinforcementKind.Reward, trace.Reinforcements[0].Kind);
    }

    private static B LoadBrain()
    {
        var genome = GenomeReader.LoadNew(new Rng(1), Path.GetFullPath(StarterGenomePath));
        var brain = new B();
        brain.ReadFromGenome(genome, new Rng(1));
        brain.RegisterBiochemistry(new float[256]);
        return brain;
    }

    private sealed class PassiveModule : IBrainModule
    {
        public int? ShadowedLobeToken => null;
        public void Initialise(B brain) { }
        public void Update(B brain) { }
    }
}
