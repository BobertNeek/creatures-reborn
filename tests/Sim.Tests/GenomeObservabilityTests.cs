using System;
using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Xunit;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Tests;

public class GenomeObservabilityTests
{
    [Fact]
    public void GeneDecoder_DecodesHeadersPayloadsAndDisplayNames()
    {
        var genome = GenomeFromRaw(
            Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id: 7, payload: [10, 20]),
            Gene((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_REACTION, id: 9, payload: [1, 2, 3, 4]));

        var records = GeneDecoder.Decode(genome);

        Assert.Equal(2, records.Count);
        Assert.Equal(0, records[0].Offset);
        Assert.Equal((int)GeneType.BRAINGENE, records[0].Type);
        Assert.Equal((int)BrainSubtype.G_LOBE, records[0].Subtype);
        Assert.Equal(7, records[0].Id);
        Assert.Equal(0, records[0].Generation);
        Assert.Equal(0, records[0].SwitchOnAge);
        Assert.Equal((byte)MutFlags.MUT, records[0].Flags);
        Assert.Equal(42, records[0].Mutability);
        Assert.Equal(0, records[0].Variant);
        Assert.Equal(new byte[] { 10, 20 }, records[0].Payload.Bytes);
        Assert.Equal("Brain/Lobe", records[0].DisplayName);
        Assert.Equal("Biochemistry/Reaction", records[1].DisplayName);
    }

    [Fact]
    public void GeneValidator_ReportsStructuralIssuesWithoutRejectingValidGenomes()
    {
        var valid = GenomeFromRaw(Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_STIMULUS, id: 1, payload: [3, 4]));
        Assert.Empty(GeneValidator.Validate(valid));

        byte[] truncatedHeader = [(byte)'g', (byte)'e', (byte)'n', (byte)'e', 2, 0];
        var truncatedIssues = GeneValidator.ValidateRaw(truncatedHeader);
        Assert.Contains(truncatedIssues, i => i.Code == GeneValidationCode.TruncatedHeader);
        Assert.Contains(truncatedIssues, i => i.Code == GeneValidationCode.MissingEndMarker);

        byte[] unknownType = RawGenome(
            Gene(type: 99, subtype: 0, id: 1, payload: [1]));
        var unknownIssues = GeneValidator.ValidateRaw(unknownType);
        Assert.Contains(unknownIssues, i => i.Code == GeneValidationCode.UnknownGeneType);
    }

    [Fact]
    public void GenomeSummary_GroupsFamiliesAndSubtypesConsistentlyWithLegacyCounts()
    {
        var genome = GenomeFromRaw(
            Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id: 1, payload: [1]),
            Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_TRACT, id: 2, payload: [2]),
            Gene((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_RECEPTOR, id: 3, payload: [3]),
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_INSTINCT, id: 4, payload: [4]),
            Gene((int)GeneType.ORGANGENE, (int)OrganSubtype.G_ORGAN, id: 5, payload: [5]));

        var summary = GenomeSummary.Create(genome);

        Assert.Equal(2, summary.Count(GeneType.BRAINGENE));
        Assert.Equal(1, summary.Count(GeneType.BIOCHEMISTRYGENE));
        Assert.Equal(1, summary.Count(GeneType.CREATUREGENE));
        Assert.Equal(1, summary.Count(GeneType.ORGANGENE));
        Assert.Equal(genome.CountGeneType((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, BrainSubtypeInfo.NUMBRAINSUBTYPES),
            summary.Count(GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE));
        Assert.Equal(genome.CountGeneType((int)GeneType.BRAINGENE, (int)BrainSubtype.G_TRACT, BrainSubtypeInfo.NUMBRAINSUBTYPES),
            summary.Count(GeneType.BRAINGENE, (int)BrainSubtype.G_TRACT));
    }

    [Fact]
    public void GenomeDiff_ReportsChangedAddedRemovedDuplicatedAndReorderedGenes()
    {
        var original = GenomeFromRaw(
            Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id: 1, payload: [1]),
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_STIMULUS, id: 2, payload: [2]),
            Gene((int)GeneType.ORGANGENE, (int)OrganSubtype.G_ORGAN, id: 6, payload: [6]));
        var changed = GenomeFromRaw(
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_STIMULUS, id: 2, payload: [3]),
            Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id: 1, payload: [1]),
            Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id: 1, generation: 1, payload: [1]),
            Gene((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_EMITTER, id: 8, payload: [8]));

        var identical = GenomeDiff.Compare(original, original);
        Assert.Empty(identical.Records);

        var diff = GenomeDiff.Compare(original, changed);
        Assert.Contains(diff.Records, r => r.Kind == GeneDiffKind.Changed);
        Assert.Contains(diff.Records, r => r.Kind == GeneDiffKind.Added);
        Assert.Contains(diff.Records, r => r.Kind == GeneDiffKind.Removed);
        Assert.Contains(diff.Records, r => r.Kind == GeneDiffKind.Duplicated);
        Assert.Contains(diff.Records, r => r.Kind == GeneDiffKind.Reordered);
    }

    [Fact]
    public void PhenotypeSummarizer_ProducesDesignerReadableSections()
    {
        var genome = GenomeFromRaw(
            Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id: 1, payload: [1]),
            Gene((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_REACTION, id: 2, payload: [2]),
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_STIMULUS, id: 3, payload: [3]),
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_APPEARANCE, id: 4, payload: [4]),
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_GENUS, id: 5, payload: [5]),
            Gene((int)GeneType.ORGANGENE, (int)OrganSubtype.G_ORGAN, id: 6, payload: [6]));

        var phenotype = PhenotypeSummarizer.Summarize(genome);

        Assert.Contains("brain structure", phenotype.Sections.Keys);
        Assert.Contains("organ chemistry", phenotype.Sections.Keys);
        Assert.Contains("stimulus learning", phenotype.Sections.Keys);
        Assert.Contains("appearance", phenotype.Sections.Keys);
        Assert.Contains("reproduction", phenotype.Sections.Keys);
    }

    [Fact]
    public void CrossoverAndMutationReportsSummarizeParentChildDifferencesWithoutChangingGenomes()
    {
        var mum = GenomeFromRaw(
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_GENUS, id: 1, payload: [1]),
            moniker: "mum");
        var dad = GenomeFromRaw(
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_GENUS, id: 1, payload: [2]),
            moniker: "dad");
        var child = GenomeFromRaw(
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_GENUS, id: 1, payload: [3]),
            moniker: "child");
        var before = child.AsSpan().ToArray();

        var crossover = CrossoverReport.Create("child", mum, dad, child);
        var mutation = MutationReport.FromParentAndChild(mum, child);

        Assert.Equal("child", crossover.ChildMoniker);
        Assert.Equal("mum", crossover.MumMoniker);
        Assert.Equal("dad", crossover.DadMoniker);
        Assert.True(crossover.MumDiff.Records.Count > 0);
        Assert.True(crossover.DadDiff.Records.Count > 0);
        Assert.True(mutation.Diff.Records.Count > 0);
        Assert.Equal(before, child.AsSpan().ToArray());
    }

    private static G GenomeFromRaw(params byte[][] genes)
        => GenomeFromRaw(genes, moniker: "test");

    private static G GenomeFromRaw(byte[] gene, string moniker)
        => GenomeFromRaw(new[] { gene }, moniker);

    private static G GenomeFromRaw(IEnumerable<byte[]> genes, string moniker)
    {
        var genome = new G(new Rng(123));
        genome.AttachBytes(RawGenome(genes.ToArray()), GeneConstants.MALE, age: 0, variant: 0, moniker);
        return genome;
    }

    private static byte[] RawGenome(params byte[][] genes)
    {
        var bytes = new List<byte>();
        foreach (var gene in genes)
            bytes.AddRange(gene);
        bytes.AddRange([(byte)'g', (byte)'e', (byte)'n', (byte)'d']);
        return bytes.ToArray();
    }

    private static byte[] Gene(
        int type,
        int subtype,
        int id,
        int generation = 0,
        int switchOn = 0,
        byte flags = (byte)MutFlags.MUT,
        byte mutability = 42,
        int variant = 0,
        params byte[] payload)
    {
        var bytes = new List<byte>
        {
            (byte)'g', (byte)'e', (byte)'n', (byte)'e',
            (byte)type,
            (byte)subtype,
            (byte)id,
            (byte)generation,
            (byte)switchOn,
            flags,
            mutability,
            (byte)variant
        };
        bytes.AddRange(payload);
        return bytes.ToArray();
    }
}
