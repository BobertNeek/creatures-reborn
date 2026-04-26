using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Lab;
using CreaturesReborn.Sim.Util;
using Xunit;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Tests;

public class EvolutionHookTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void GenomeEvolutionHookSet_ExtractsBrainAnchorsFromGenome()
    {
        var genome = LoadGenome(seed: 130);

        GenomeEvolutionHookSet hooks = GenomeEvolutionHookSet.Create(genome);

        Assert.True(hooks.OfflineOnly);
        Assert.NotEmpty(hooks.Lobes);
        Assert.NotEmpty(hooks.Tracts);
        Assert.NotEmpty(hooks.ModuleCandidates);
        Assert.All(hooks.Lobes, lobe =>
        {
            Assert.NotEqual(0, lobe.Token);
            Assert.True(lobe.NeuronCount > 0);
            Assert.False(string.IsNullOrWhiteSpace(lobe.TokenText));
        });
        Assert.All(hooks.Tracts, tract =>
        {
            Assert.NotEqual(0, tract.SourceToken);
            Assert.NotEqual(0, tract.TargetToken);
        });
    }

    [Fact]
    public void EvolutionModuleCandidates_AreOfflineAndDisabledByDefault()
    {
        GenomeEvolutionHookSet hooks = GenomeEvolutionHookSet.Create(LoadGenome(seed: 131));

        EvolutionModuleCandidate candidate = hooks.ModuleCandidates[0];
        PlasticityBrainModuleOptions defaultOptions = candidate.ToPlasticityOptions();

        Assert.All(hooks.ModuleCandidates, module =>
        {
            Assert.True(module.OfflineOnly);
            Assert.False(module.EnabledByDefault);
            Assert.Equal(EvolutionModuleCandidateKind.Plasticity, module.Kind);
        });
        Assert.False(defaultOptions.Enabled);
        Assert.Null(defaultOptions.ShadowedLobeToken);
    }

    [Fact]
    public void EvolutionModuleCandidate_CreatesExplicitPlasticityOptions()
    {
        GenomeEvolutionHookSet hooks = GenomeEvolutionHookSet.Create(
            LoadGenome(seed: 132),
            new EvolutionHookBuildOptions(MaxSourceNeurons: 3, MaxTargetNeurons: 4, LearningRate: 0.125f));
        EvolutionModuleCandidate candidate = hooks.ModuleCandidates[0];

        PlasticityBrainModuleOptions options = candidate.ToPlasticityOptions(
            enabled: true,
            shadowTargetLobe: true,
            writeTargetInputs: true);

        Assert.True(options.Enabled);
        Assert.Equal(candidate.SourceLobeToken, options.SourceLobeToken);
        Assert.Equal(candidate.TargetLobeToken, options.TargetLobeToken);
        Assert.Equal(candidate.TargetLobeToken, options.ShadowedLobeToken);
        Assert.InRange(options.SourceNeuronCount, 1, 3);
        Assert.InRange(options.TargetNeuronCount, 1, 4);
        Assert.Equal(0.125f, options.LearningRate);
        Assert.True(options.WriteTargetInputs);
    }

    [Fact]
    public void GenomeEvolutionHookSet_IsDeterministicForSameGenome()
    {
        GenomeEvolutionHookSet first = GenomeEvolutionHookSet.Create(LoadGenome(seed: 133));
        GenomeEvolutionHookSet second = GenomeEvolutionHookSet.Create(LoadGenome(seed: 133));

        Assert.Equal(
            first.ModuleCandidates.Select(candidate => candidate.InnovationId),
            second.ModuleCandidates.Select(candidate => candidate.InnovationId));
        Assert.Equal(
            first.ModuleCandidates.Select(candidate => (candidate.SourceLobeToken, candidate.TargetLobeToken)),
            second.ModuleCandidates.Select(candidate => (candidate.SourceLobeToken, candidate.TargetLobeToken)));
    }

    [Fact]
    public void GenomeEvolutionHookSet_DoesNotMutateGenomeBytesOrCounts()
    {
        var genome = LoadGenome(seed: 134);
        byte[] beforeBytes = genome.AsSpan().ToArray();
        int beforeLobes = genome.CountGeneType((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, BrainSubtypeInfo.NUMBRAINSUBTYPES);

        _ = GenomeEvolutionHookSet.Create(genome);

        Assert.Equal(beforeBytes, genome.AsSpan().ToArray());
        Assert.Equal(
            beforeLobes,
            genome.CountGeneType((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, BrainSubtypeInfo.NUMBRAINSUBTYPES));
    }

    private static G LoadGenome(int seed)
        => GenomeReader.LoadNew(new Rng(seed), Path.GetFullPath(StarterGenomePath));
}
