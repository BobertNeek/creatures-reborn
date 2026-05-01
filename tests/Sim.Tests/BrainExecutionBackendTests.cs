using System;
using System.IO;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using Xunit;
using B = CreaturesReborn.Sim.Brain.Brain;

namespace CreaturesReborn.Sim.Tests;

public sealed class BrainExecutionBackendTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void Brain_DefaultExecutionMode_UsesClassicCpuBackend()
    {
        B brain = LoadBrain(seed: 200);

        brain.UpdateComponents();

        Assert.Equal(BrainExecutionMode.CpuOnly, brain.ExecutionMode);
        Assert.Equal(BrainExecutionBackendKind.Cpu, brain.LastExecutionStatus.Kind);
        Assert.False(brain.LastExecutionStatus.UsedGpu);
        Assert.Null(brain.LastExecutionStatus.FallbackReason);
    }

    [Fact]
    public void Brain_GpuPreferred_FallsBackBeforeMutationWhenBackendUnavailable()
    {
        B baseline = LoadBrain(seed: 201);
        B accelerated = LoadBrain(seed: 201);
        var backend = new FakeGpuBackend(isAvailable: false, isSupported: true);
        accelerated.ConfigureExecutionBackend(backend, BrainExecutionMode.GpuPreferred);

        baseline.UpdateComponents();
        accelerated.UpdateComponents();

        Assert.Equal(0, backend.UpdateCalls);
        Assert.Equal(BrainExecutionBackendKind.Cpu, accelerated.LastExecutionStatus.Kind);
        Assert.False(accelerated.LastExecutionStatus.UsedGpu);
        Assert.Contains("unavailable", accelerated.LastExecutionStatus.FallbackReason, StringComparison.OrdinalIgnoreCase);
        AssertBrainSampleEqual(baseline, accelerated);
    }

    [Fact]
    public void Brain_GpuPreferred_FallsBackBeforeMutationWhenBackendDoesNotSupportBrain()
    {
        B baseline = LoadBrain(seed: 202);
        B accelerated = LoadBrain(seed: 202);
        var backend = new FakeGpuBackend(isAvailable: true, isSupported: false);
        accelerated.ConfigureExecutionBackend(backend, BrainExecutionMode.GpuPreferred);

        baseline.UpdateComponents();
        accelerated.UpdateComponents();

        Assert.Equal(0, backend.UpdateCalls);
        Assert.Equal(BrainExecutionBackendKind.Cpu, accelerated.LastExecutionStatus.Kind);
        Assert.False(accelerated.LastExecutionStatus.UsedGpu);
        Assert.Contains("unsupported", accelerated.LastExecutionStatus.FallbackReason, StringComparison.OrdinalIgnoreCase);
        AssertBrainSampleEqual(baseline, accelerated);
    }

    [Fact]
    public void Brain_GpuPreferred_UsesSupportedBackendThroughExecutionContext()
    {
        B brain = LoadBrain(seed: 203);
        var backend = new FakeGpuBackend(isAvailable: true, isSupported: true);
        brain.ConfigureExecutionBackend(backend, BrainExecutionMode.GpuPreferred);

        brain.UpdateComponents();

        Assert.Equal(1, backend.UpdateCalls);
        Assert.True(backend.LastContextHadComponents);
        Assert.Equal("fake gpu", brain.LastExecutionStatus.BackendName);
        Assert.Equal(BrainExecutionBackendKind.Gpu, brain.LastExecutionStatus.Kind);
        Assert.True(brain.LastExecutionStatus.UsedGpu);
        Assert.Null(brain.LastExecutionStatus.FallbackReason);
    }

    [Fact]
    public void Brain_GpuShadowValidate_KeepsCpuStateWhenGpuOutputDiffers()
    {
        B baseline = LoadBrain(seed: 204);
        B shadowed = LoadBrain(seed: 204);
        var backend = new CorruptingGpuBackend();
        shadowed.ConfigureExecutionBackend(backend, BrainExecutionMode.GpuShadowValidate);

        baseline.UpdateComponents();
        shadowed.UpdateComponents();

        Assert.Equal(1, backend.UpdateCalls);
        Assert.Equal(BrainExecutionBackendKind.Cpu, shadowed.LastExecutionStatus.Kind);
        Assert.True(shadowed.LastExecutionStatus.UsedGpu);
        Assert.Contains("shadow", shadowed.LastExecutionStatus.FallbackReason, StringComparison.OrdinalIgnoreCase);
        AssertBrainSampleEqual(baseline, shadowed);
    }

    private static B LoadBrain(int seed)
    {
        var genome = GenomeReader.LoadNew(new Rng(seed), Path.GetFullPath(StarterGenomePath));
        var brain = new B();
        brain.ReadFromGenome(genome, new Rng(seed));
        brain.RegisterBiochemistry(new float[256]);
        return brain;
    }

    private static void AssertBrainSampleEqual(B expected, B actual)
    {
        BrainSnapshot expectedSnapshot = expected.CreateSnapshot(new BrainSnapshotOptions(
            MaxNeuronsPerLobe: 8,
            MaxDendritesPerTract: 8));
        BrainSnapshot actualSnapshot = actual.CreateSnapshot(new BrainSnapshotOptions(
            MaxNeuronsPerLobe: 8,
            MaxDendritesPerTract: 8));

        Assert.Equal(expectedSnapshot.Lobes.Count, actualSnapshot.Lobes.Count);
        Assert.Equal(expectedSnapshot.Tracts.Count, actualSnapshot.Tracts.Count);
        for (int i = 0; i < expectedSnapshot.Lobes.Count; i++)
        {
            Assert.Equal(expectedSnapshot.Lobes[i].WinningNeuronId, actualSnapshot.Lobes[i].WinningNeuronId);
            for (int n = 0; n < expectedSnapshot.Lobes[i].Neurons.Count; n++)
                Assert.Equal(expectedSnapshot.Lobes[i].Neurons[n].States, actualSnapshot.Lobes[i].Neurons[n].States);
        }

        for (int i = 0; i < expectedSnapshot.Tracts.Count; i++)
        {
            Assert.Equal(expectedSnapshot.Tracts[i].STtoLTRate, actualSnapshot.Tracts[i].STtoLTRate, precision: 6);
            for (int d = 0; d < expectedSnapshot.Tracts[i].Dendrites.Count; d++)
                Assert.Equal(expectedSnapshot.Tracts[i].Dendrites[d].Weights, actualSnapshot.Tracts[i].Dendrites[d].Weights);
        }
    }

    private sealed class FakeGpuBackend : IBrainExecutionBackend
    {
        private readonly bool _isSupported;

        public FakeGpuBackend(bool isAvailable, bool isSupported)
        {
            IsAvailable = isAvailable;
            _isSupported = isSupported;
        }

        public string Name => "fake gpu";
        public BrainExecutionBackendKind Kind => BrainExecutionBackendKind.Gpu;
        public bool IsAvailable { get; }
        public int UpdateCalls { get; private set; }
        public bool LastContextHadComponents { get; private set; }

        public bool Supports(BrainExecutionContext context, out string? reason)
        {
            reason = _isSupported ? null : "unsupported brain topology";
            return _isSupported;
        }

        public void Update(BrainExecutionContext context)
        {
            UpdateCalls++;
            LastContextHadComponents = context.Components.Count > 0;
            context.RunClassicCpuUpdate();
        }
    }

    private sealed class CorruptingGpuBackend : IBrainExecutionBackend
    {
        public string Name => "corrupting gpu";
        public BrainExecutionBackendKind Kind => BrainExecutionBackendKind.Gpu;
        public bool IsAvailable => true;
        public int UpdateCalls { get; private set; }

        public bool Supports(BrainExecutionContext context, out string? reason)
        {
            reason = null;
            return true;
        }

        public void Update(BrainExecutionContext context)
        {
            UpdateCalls++;
            Lobe? lobe = context.Brain.GetLobe(0);
            if (lobe != null)
                lobe.GetNeuron(0).States[NeuronVar.State] = 0.99f;
        }
    }
}
