using System;
using System.IO;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using G     = CreaturesReborn.Sim.Genome.Genome;
using B     = CreaturesReborn.Sim.Brain.Brain;
using Lobe  = CreaturesReborn.Sim.Brain.Lobe;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

/// <summary>
/// Phase-A validation of the Brain subsystem:
/// - Lobes are created from genome (count > 0)
/// - Tracts are created from genome (count > 0)
/// - No NaN or exception after 100 ticks
/// - WinningNeuronId stays in valid range
/// </summary>
public class BrainBootTests
{
    private static readonly string GenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "resources for gpt",
            "Creature Norn Augment Project",
            "Agents AND Breeds",
            "C3DS Compilation Mall-Breed Pack egg agents and decompiled files with sprites",
            "C3DS Compilation Mall-Breed Pack",
            "norn.bondi.48.gen");

    private static readonly IRng Rng = new Rng(42);

    private static bool ShouldSkip() => !File.Exists(GenomePath);

    private static (G genome, B brain) LoadBrainFromGenome()
    {
        var genome = GenomeReader.LoadNew(new Rng(0), GenomePath);
        var brain  = new B();
        brain.ReadFromGenome(genome, new Rng(1));
        return (genome, brain);
    }

    // -------------------------------------------------------------------------

    [Fact]
    public void BrainHasAtLeastOneLobeAfterGenomeLoad()
    {
        if (ShouldSkip()) return;
        var (_, brain) = LoadBrainFromGenome();
        Assert.True(brain.LobeCount > 0, "Expected at least one lobe from genome.");
    }

    [Fact]
    public void BrainHasAtLeastOneTractAfterGenomeLoad()
    {
        if (ShouldSkip()) return;
        var (_, brain) = LoadBrainFromGenome();
        Assert.True(brain.TractCount > 0, "Expected at least one tract from genome.");
    }

    [Fact]
    public void BrainLobeCountMatchesExpected()
    {
        // C3/DS standard norns have exactly 8 lobes: srep, driv, stim, verb, noun, attn, decn, visn (plus smel, resp in some genomes)
        if (ShouldSkip()) return;
        var (_, brain) = LoadBrainFromGenome();
        Assert.True(brain.LobeCount >= 8,
            $"Expected ≥8 lobes, got {brain.LobeCount}.");
    }

    [Fact]
    public void UpdateDoesNotThrowFor100Ticks()
    {
        if (ShouldSkip()) return;
        var chemicals = new float[256];
        chemicals[35] = 1.0f; // ATP = full

        var (genome, brain) = LoadBrainFromGenome();
        brain.RegisterBiochemistry(chemicals);

        for (int i = 0; i < 100; i++)
            brain.Update();
    }

    [Fact]
    public void NeuronStatesHaveNoNaNAfter100Ticks()
    {
        if (ShouldSkip()) return;
        var chemicals = new float[256];
        chemicals[35] = 1.0f;

        var (genome, brain) = LoadBrainFromGenome();
        brain.RegisterBiochemistry(chemicals);

        for (int i = 0; i < 100; i++)
            brain.Update();

        for (int l = 0; l < brain.LobeCount; l++)
        {
            Lobe? lobe = brain.GetLobe(l);
            Assert.NotNull(lobe);
            for (int n = 0; n < lobe!.GetNoOfNeurons(); n++)
            {
                for (int v = 0; v < 8; v++)
                {
                    float val = lobe.GetNeuronState(n, v);
                    Assert.False(float.IsNaN(val),
                        $"NaN at lobe {l}, neuron {n}, var {v}.");
                }
            }
        }
    }

    [Fact]
    public void WinningNeuronIdInValidRangeAfterTicks()
    {
        if (ShouldSkip()) return;
        var chemicals = new float[256];
        chemicals[35] = 1.0f;

        var (genome, brain) = LoadBrainFromGenome();
        brain.RegisterBiochemistry(chemicals);

        for (int i = 0; i < 50; i++)
            brain.Update();

        for (int l = 0; l < brain.LobeCount; l++)
        {
            Lobe? lobe = brain.GetLobe(l);
            Assert.NotNull(lobe);
            int winner = lobe!.GetWhichNeuronWon();
            Assert.True(winner >= 0 && winner < lobe.GetNoOfNeurons(),
                $"Winning neuron {winner} out of range [0, {lobe.GetNoOfNeurons()}) in lobe {l}.");
        }
    }

    [Fact]
    public void BrainLocusProviderReturnsNonNullLocus()
    {
        if (ShouldSkip()) return;
        var chemicals = new float[256];
        var (_, brain) = LoadBrainFromGenome();

        // Lobe with tissueId 0 should exist in any standard norn genome
        for (int t = 0; t < 16; t++)
        {
            var locus = brain.GetBrainLocus(t, 0);
            Assert.NotNull(locus);
        }
    }

    [Fact]
    public void BrainLocusBackingIsLive()
    {
        // Verify backed FloatLocus reads and writes go through to neuron.States
        if (ShouldSkip()) return;
        var (_, brain) = LoadBrainFromGenome();

        if (brain.LobeCount == 0) return;

        Lobe lobe0 = brain.GetLobe(0)!;
        int  tissueId = lobe0.GetTissueId();

        var locus = brain.GetBrainLocus(tissueId, 0); // neuron 0, state var 0
        float original = lobe0.GetNeuronState(0, 0);

        // Write via locus, read via lobe
        locus.Value = 0.5f;
        Assert.Equal(0.5f, lobe0.GetNeuronState(0, 0));

        // Restore
        locus.Value = original;
    }
}
