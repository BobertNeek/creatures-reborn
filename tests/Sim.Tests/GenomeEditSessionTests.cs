using System;
using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Xunit;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Tests;

public sealed class GenomeEditSessionTests
{
    [Fact]
    public void GenomeDocument_ClonesSourceAndWorkingCopies()
    {
        G genome = GenomeFromRaw(
            Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id: 7, payload: [1, 2, 3]));
        byte[] original = GenomeWriter.Serialize(genome);

        GenomeDocument document = GenomeDocument.FromGenome(genome, new Rng(12));
        var session = new GenomeEditSession(document, new Rng(13));

        session.Apply(GenomeEditOperation.ReplacePayload(0, [9, 8, 7, 6]));

        Assert.Equal(original, GenomeWriter.Serialize(genome));
        Assert.Equal(original, document.SourceFileBytes);
        Assert.NotEqual(document.SourceRawBytes, document.WorkingRawBytes);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, document.WorkingRecords[0].Payload.Bytes);
    }

    [Fact]
    public void GenomeEditSession_AppliesUndoAndRedoForHeaderAndPayloadEdits()
    {
        G genome = GenomeFromRaw(
            Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id: 7, switchOn: 0, payload: [1, 2, 3]));
        var session = new GenomeEditSession(GenomeDocument.FromGenome(genome, new Rng(20)), new Rng(21));

        session.Apply(GenomeEditOperation.EditHeader(0, GeneHeaderPatch.Create(switchOnAge: 16, mutability: 99)));
        session.Apply(GenomeEditOperation.ReplacePayload(0, [4, 5, 6]));

        Assert.Equal(16, session.Document.WorkingRecords[0].SwitchOnAge);
        Assert.Equal(99, session.Document.WorkingRecords[0].Mutability);
        Assert.Equal(new byte[] { 4, 5, 6 }, session.Document.WorkingRecords[0].Payload.Bytes);
        Assert.True(session.CanUndo);
        Assert.False(session.CanRedo);

        session.Undo();
        Assert.Equal(new byte[] { 1, 2, 3 }, session.Document.WorkingRecords[0].Payload.Bytes);
        Assert.True(session.CanRedo);

        session.Undo();
        Assert.Equal(0, session.Document.WorkingRecords[0].SwitchOnAge);
        Assert.Equal(42, session.Document.WorkingRecords[0].Mutability);

        session.Redo();
        session.Redo();
        Assert.Equal(16, session.Document.WorkingRecords[0].SwitchOnAge);
        Assert.Equal(new byte[] { 4, 5, 6 }, session.Document.WorkingRecords[0].Payload.Bytes);
    }

    [Fact]
    public void GenomeEditSession_CanDuplicateDeleteMoveAndRoundTrip()
    {
        G genome = GenomeFromRaw(
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_STIMULUS, id: 1, payload: [1]),
            Gene((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_REACTION, id: 2, payload: [2]),
            Gene((int)GeneType.ORGANGENE, (int)OrganSubtype.G_ORGAN, id: 3, payload: [3]));
        var session = new GenomeEditSession(GenomeDocument.FromGenome(genome, new Rng(30)), new Rng(31));

        session.Apply(GenomeEditOperation.DuplicateGene(1));
        session.Apply(GenomeEditOperation.MoveGene(fromIndex: 3, toIndex: 0));
        session.Apply(GenomeEditOperation.DeleteGene(2));

        Assert.Equal(3, session.Document.WorkingRecords.Count);
        Assert.Equal((int)GeneType.BIOCHEMISTRYGENE, session.Document.WorkingRecords[0].Type);
        Assert.DoesNotContain(GeneValidator.ValidateRaw(session.Document.WorkingRawBytes), issue => issue.Severity == GeneValidationSeverity.Error);

        byte[] exported = session.ExportFileBytes();
        var reloaded = new G(new Rng(32));
        GenomeReader.Load(reloaded, exported);
        Assert.Equal(session.Document.WorkingRawBytes, reloaded.AsSpan().ToArray());
    }

    [Fact]
    public void GenomeEditSession_ReportsDiffAndPhenotypeForWorkingGenome()
    {
        G genome = GenomeFromRaw(
            Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id: 1, payload: [1]),
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_INSTINCT, id: 2, payload: [2]));
        var session = new GenomeEditSession(GenomeDocument.FromGenome(genome, new Rng(40)), new Rng(41));

        session.Apply(GenomeEditOperation.ReplacePayload(0, [8, 8]));

        Assert.Contains(session.CreateDiff().Records, record => record.Kind == GeneDiffKind.Changed);
        Assert.Contains("brain structure", session.CreatePhenotypeSummary().Sections.Keys);
        Assert.Contains("stimulus learning", session.CreatePhenotypeSummary().Sections.Keys);
    }

    [Fact]
    public void GenePayloadCodec_DecodesTypedCoreFieldsAndEncodesPatches()
    {
        byte[] lobePayload =
        [
            (byte)'d', (byte)'r', (byte)'i', (byte)'v',
            0, 64,
            0, 10,
            0, 20,
            3,
            4,
            80,
            90,
            100,
            1,
            5,
            1,
            0,
            (byte)'n', (byte)'u', (byte)'l', (byte)'l'
        ];
        G genome = GenomeFromRaw(Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id: 1, payload: lobePayload));
        GeneRecord record = GeneDecoder.Decode(genome)[0];

        EditableGenePayload payload = GenePayloadCodec.Decode(record);

        Assert.Equal(GenePayloadKind.BrainLobe, payload.Kind);
        Assert.Equal("driv", payload.GetString("token"));
        Assert.Equal(64, payload.GetInt("update"));
        Assert.Equal(10, payload.GetInt("x"));
        Assert.Equal(20, payload.GetInt("y"));
        Assert.Equal(3, payload.GetInt("width"));
        Assert.Equal(4, payload.GetInt("height"));
        Assert.Equal(5, payload.GetInt("tissue"));

        byte[] encoded = GenePayloadCodec.Encode(record, new[]
        {
            GeneFieldEdit.Int("update", 32),
            GeneFieldEdit.Int("width", 8),
            GeneFieldEdit.String("token", "decn"),
        });

        EditableGenePayload edited = GenePayloadCodec.Decode(record with { Payload = record.Payload with { Bytes = encoded } });
        Assert.Equal("decn", edited.GetString("token"));
        Assert.Equal(32, edited.GetInt("update"));
        Assert.Equal(8, edited.GetInt("width"));
    }

    [Fact]
    public void GenePayloadCodec_UsesRawFallbackForUnknownOrShortPayloads()
    {
        G genome = GenomeFromRaw(Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_TRACT, id: 1, payload: [1, 2]));
        GeneRecord record = GeneDecoder.Decode(genome)[0];

        EditableGenePayload payload = GenePayloadCodec.Decode(record);

        Assert.True(payload.IsRawFallback);
        Assert.Equal(new byte[] { 1, 2 }, payload.RawBytes);
        Assert.Equal(new byte[] { 9, 9 }, GenePayloadCodec.Encode(record, [GeneFieldEdit.Raw([9, 9])]));
    }

    [Fact]
    public void GenomeEditSession_CanCreateMutationAndCrossoverReports()
    {
        G mum = GenomeFromRaw(Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_GENUS, id: 1, payload: [1]), moniker: "mum");
        G dad = GenomeFromRaw(Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_GENUS, id: 1, payload: [2]), moniker: "dad");

        GenomeEditSession session = GenomeEditSession.FromCrossover(
            "child",
            mum,
            dad,
            new Rng(50),
            mumChanceOfMutation: 4,
            mumDegreeOfMutation: 4,
            dadChanceOfMutation: 4,
            dadDegreeOfMutation: 4);

        Assert.Equal("child", session.CrossoverReport?.ChildMoniker);
        Assert.Equal("mum", session.CrossoverReport?.MumMoniker);
        Assert.Equal("dad", session.CrossoverReport?.DadMoniker);
        Assert.NotNull(session.MutationReport);
        Assert.NotEqual(GenomeWriter.Serialize(mum), session.ExportFileBytes());
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
