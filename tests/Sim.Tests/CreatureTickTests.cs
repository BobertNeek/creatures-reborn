using System;
using System.IO;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using G   = CreaturesReborn.Sim.Genome.Genome;
using C   = CreaturesReborn.Sim.Creature.Creature;
using VId = CreaturesReborn.Sim.Creature.VerbId;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

/// <summary>
/// Phase-A headless validation harness:
/// - Load a stock C3/DS .gen, construct Creature, tick 10,000×
/// - No NaNs in any neuron state or chemical concentration
/// - Chemical totals stay within [0, 1]
/// - Motor faculty resolves valid verb/noun IDs
/// </summary>
public class CreatureTickTests
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

    private static bool ShouldSkip() => !File.Exists(GenomePath);

    private static C LoadCreature()
        => C.LoadFromFile(GenomePath, new Rng(42));

    // -------------------------------------------------------------------------

    [Fact]
    public void Creature_LoadsWithoutException()
    {
        if (ShouldSkip()) return;
        var c = LoadCreature();
        Assert.NotNull(c);
        Assert.True(c.Brain.LobeCount > 0);
        Assert.True(c.Brain.TractCount > 0);
    }

    [Fact]
    public void Creature_10000Ticks_NoException()
    {
        if (ShouldSkip()) return;
        var c = LoadCreature();
        c.SetChemical(35, 1.0f); // ATP = full

        for (int i = 0; i < 10_000; i++)
            c.Tick();
    }

    [Fact]
    public void Creature_10000Ticks_NoNaNInChemicals()
    {
        if (ShouldSkip()) return;
        var c = LoadCreature();
        c.SetChemical(35, 1.0f);

        for (int i = 0; i < 10_000; i++)
            c.Tick();

        for (int chem = 0; chem < 256; chem++)
        {
            float v = c.GetChemical(chem);
            Assert.False(float.IsNaN(v), $"NaN at chemical {chem}");
            Assert.True(v >= 0.0f && v <= 1.0f,
                $"Chemical {chem} out of [0,1]: {v}");
        }
    }

    [Fact]
    public void Creature_10000Ticks_NoNaNInNeurons()
    {
        if (ShouldSkip()) return;
        var c = LoadCreature();
        c.SetChemical(35, 1.0f);

        for (int i = 0; i < 10_000; i++)
            c.Tick();

        for (int l = 0; l < c.Brain.LobeCount; l++)
        {
            var lobe = c.Brain.GetLobe(l)!;
            for (int n = 0; n < lobe.GetNoOfNeurons(); n++)
            {
                for (int v = 0; v < 8; v++)
                {
                    float val = lobe.GetNeuronState(n, v);
                    Assert.False(float.IsNaN(val),
                        $"NaN at lobe {l}, neuron {n}, var {v}");
                }
            }
        }
    }

    [Fact]
    public void Creature_MotorFacultyReturnsValidVerbAfterTicks()
    {
        if (ShouldSkip()) return;
        var c = LoadCreature();
        c.SetChemical(35, 1.0f);

        for (int i = 0; i < 100; i++)
            c.Tick();

        Assert.True(c.Motor.CurrentVerb >= 0 && c.Motor.CurrentVerb < VId.NumVerbs,
            $"Unexpected verb: {c.Motor.CurrentVerb}");
        Assert.True(c.Motor.CurrentNoun >= 0,
            $"Unexpected noun: {c.Motor.CurrentNoun}");
    }

    [Fact]
    public void Creature_InjectChemical_RaisesConcentration()
    {
        if (ShouldSkip()) return;
        var c = LoadCreature();

        float before = c.GetChemical(50);
        c.InjectChemical(50, 0.5f);
        float after  = c.GetChemical(50);

        Assert.True(after > before || after == 1.0f);
    }
}
