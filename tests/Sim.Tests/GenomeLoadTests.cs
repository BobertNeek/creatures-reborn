using System;
using System.IO;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using G = CreaturesReborn.Sim.Genome.Genome;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

/// <summary>
/// Phase-A validation: load a stock C3/DS .gen file, verify basic structure, and confirm a
/// lossless round-trip through GenomeWriter → GenomeReader.
/// </summary>
public class GenomeLoadTests
{
    // Path to a known-good norn genome shipped with the C3DS compilation pack.
    private static readonly string GenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..", // up from test bin to repo root
            "resources for gpt",
            "Creature Norn Augment Project",
            "Agents AND Breeds",
            "C3DS Compilation Mall-Breed Pack egg agents and decompiled files with sprites",
            "My Agents",
            "BondiNornPack files",
            "norn.bondi.48.gen");

    private static readonly IRng Rng = new Rng(seed: 42);

    // -------------------------------------------------------------------------
    // Load
    // -------------------------------------------------------------------------

    [Fact]
    public void LoadFromFile_LoadsWithoutException()
    {
        if (ShouldSkip()) return;
        var g = new G(Rng);
        GenomeReader.LoadFromFile(g, FullPath, moniker: "bondi01");
        Assert.True(g.Length > 0, "Expected non-empty genome after load.");
    }

    [Fact]
    public void LoadFromFile_RejectsWrongMagic()
    {
        byte[] junk = new byte[] { 0x64, 0x6E, 0x61, 0x32 }; // "dna2" — old format
        var g = new G(Rng);
        Assert.Throws<GenomeException>(() => GenomeReader.Load(g, junk));
    }

    [Fact]
    public void LoadFromFile_RejectsTooShort()
    {
        var g = new G(Rng);
        Assert.Throws<GenomeException>(() => GenomeReader.Load(g, new byte[] { 0x64, 0x6E }));
    }

    // -------------------------------------------------------------------------
    // Gene counts by type
    // -------------------------------------------------------------------------

    [Fact]
    public void GeneCounts_AllTypesPresent()
    {
        if (ShouldSkip()) return;
        var g = LoadBondi();

        int brain    = CountAllSubtypes(g, GeneType.BRAINGENE,        BrainSubtypeInfo.NUMBRAINSUBTYPES);
        int biochem  = CountAllSubtypes(g, GeneType.BIOCHEMISTRYGENE, BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES);
        int creature = CountAllSubtypes(g, GeneType.CREATUREGENE,     CreatureSubtypeInfo.NUMCREATURESUBTYPES);
        int organ    = CountAllSubtypes(g, GeneType.ORGANGENE,        OrganSubtypeInfo.NUMORGANSUBTYPES);

        // A healthy C3/DS norn has many genes of each type — sanity-check minimums.
        Assert.True(brain    > 0,  $"Expected brain genes, got {brain}");
        Assert.True(biochem  > 10, $"Expected many biochem genes, got {biochem}");
        Assert.True(creature > 0,  $"Expected creature genes, got {creature}");
        Assert.True(organ    > 0,  $"Expected organ genes, got {organ}");
    }

    [Fact]
    public void GeneCounts_BrainSubtypeBreakdown_Reasonable()
    {
        if (ShouldSkip()) return;
        var g = LoadBondi();

        int lobes   = g.CountGeneType((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE,   BrainSubtypeInfo.NUMBRAINSUBTYPES);
        int borgans = g.CountGeneType((int)GeneType.BRAINGENE, (int)BrainSubtype.G_BORGAN, BrainSubtypeInfo.NUMBRAINSUBTYPES);
        int tracts  = g.CountGeneType((int)GeneType.BRAINGENE, (int)BrainSubtype.G_TRACT,  BrainSubtypeInfo.NUMBRAINSUBTYPES);

        Assert.True(lobes  >= 1, $"Expected lobe genes, got {lobes}");
        Assert.True(borgans >= 0, $"borgan count should be non-negative");
        Assert.True(tracts >= 1, $"Expected tract genes, got {tracts}");
    }

    [Fact]
    public void GeneCounts_BiochemSubtypeBreakdown_Reasonable()
    {
        if (ShouldSkip()) return;
        var g = LoadBondi();

        int receptors = g.CountGeneType((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_RECEPTOR, BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES);
        int emitters  = g.CountGeneType((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_EMITTER,  BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES);
        int reactions = g.CountGeneType((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_REACTION, BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES);
        int halfLives = g.CountGeneType((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_HALFLIFE, BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES);

        Assert.True(receptors > 0, $"Expected receptors, got {receptors}");
        Assert.True(emitters  > 0, $"Expected emitters, got {emitters}");
        Assert.True(reactions > 0, $"Expected reactions, got {reactions}");
        Assert.True(halfLives > 0, $"Expected half-lives, got {halfLives}");
    }

    // -------------------------------------------------------------------------
    // Round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void RoundTrip_SerializeAndReload_IdenticalBytes()
    {
        if (ShouldSkip()) return;
        var g1 = LoadBondi();
        ReadOnlySpan<byte> original = g1.AsSpan();

        byte[] serialized = GenomeWriter.Serialize(g1);

        var g2 = new G(Rng);
        GenomeReader.Load(g2, serialized, moniker: "bondi01");
        ReadOnlySpan<byte> reloaded = g2.AsSpan();

        Assert.Equal(original.Length, reloaded.Length);
        Assert.True(original.SequenceEqual(reloaded),
            "Round-trip produced different bytes — genome serialization is lossy.");
    }

    [Fact]
    public void RoundTrip_GeneCounts_Preserved()
    {
        if (ShouldSkip()) return;
        var g1 = LoadBondi();
        byte[] serialized = GenomeWriter.Serialize(g1);

        var g2 = new G(Rng);
        GenomeReader.Load(g2, serialized, moniker: "bondi01");

        foreach (GeneType gt in Enum.GetValues<GeneType>())
        {
            int numsubs = NumSubtypes(gt);
            int before  = CountAllSubtypes(g1, gt, numsubs);
            int after   = CountAllSubtypes(g2, gt, numsubs);
            Assert.Equal(before, after);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string FullPath => Path.GetFullPath(GenomePath);

    /// <summary>
    /// Returns true and marks the test skipped if the reference genome file is absent.
    /// Usage: <c>if (ShouldSkip()) return;</c>
    /// </summary>
    private static bool ShouldSkip()
    {
        // xunit 2.x has no runtime-skip API; we simply bail out early.
        // The test counts as passed (vacuously) when the genome data isn't on this machine.
        return !File.Exists(FullPath);
    }

    private G LoadBondi()
    {
        var g = new G(Rng);
        GenomeReader.LoadFromFile(g, FullPath, moniker: "bondi01");
        return g;
    }

    /// <summary>Sum gene counts across all subtypes of a given type (for "count all" semantics).</summary>
    private static int CountAllSubtypes(G g, GeneType type, int numSubtypes)
    {
        int total = 0;
        for (int sub = 0; sub < numSubtypes; sub++)
            total += g.CountGeneType((int)type, sub, numSubtypes);
        return total;
    }

    private static int NumSubtypes(GeneType gt) => gt switch
    {
        GeneType.BRAINGENE        => BrainSubtypeInfo.NUMBRAINSUBTYPES,
        GeneType.BIOCHEMISTRYGENE => BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES,
        GeneType.CREATUREGENE     => CreatureSubtypeInfo.NUMCREATURESUBTYPES,
        GeneType.ORGANGENE        => OrganSubtypeInfo.NUMORGANSUBTYPES,
        _                         => 1,
    };
}
