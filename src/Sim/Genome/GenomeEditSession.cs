using System;
using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Genome;

public enum GenomeEditOperationKind
{
    EditHeader,
    ReplacePayload,
    DuplicateGene,
    DeleteGene,
    MoveGene
}

public sealed record GeneHeaderPatch(
    int? Type = null,
    int? Subtype = null,
    int? Id = null,
    int? Generation = null,
    int? SwitchOnAge = null,
    byte? Flags = null,
    int? Mutability = null,
    int? Variant = null)
{
    public static GeneHeaderPatch Create(
        int? type = null,
        int? subtype = null,
        int? id = null,
        int? generation = null,
        int? switchOnAge = null,
        byte? flags = null,
        int? mutability = null,
        int? variant = null)
        => new(type, subtype, id, generation, switchOnAge, flags, mutability, variant);

    internal void Apply(byte[] rawGene)
    {
        if (rawGene.Length < GeneHeaderOffsets.GH_LENGTH)
            throw new ArgumentException("Gene is shorter than the fixed header length.", nameof(rawGene));

        WriteByte(rawGene, GeneHeaderOffsets.GH_TYPE, Type);
        WriteByte(rawGene, GeneHeaderOffsets.GH_SUB, Subtype);
        WriteByte(rawGene, GeneHeaderOffsets.GH_ID, Id);
        WriteByte(rawGene, GeneHeaderOffsets.GH_GEN, Generation);
        WriteByte(rawGene, GeneHeaderOffsets.GH_SWITCHON, SwitchOnAge);
        if (Flags.HasValue)
            rawGene[GeneHeaderOffsets.GH_FLAGS] = Flags.Value;
        WriteByte(rawGene, GeneHeaderOffsets.GH_MUTABILITY, Mutability);
        WriteByte(rawGene, GeneHeaderOffsets.GH_VARIANT, Variant);
    }

    private static void WriteByte(byte[] rawGene, int offset, int? value)
    {
        if (!value.HasValue) return;
        rawGene[offset] = (byte)Math.Clamp(value.Value, 0, 255);
    }
}

public sealed record GenomeEditOperation(
    GenomeEditOperationKind Kind,
    int GeneIndex,
    GeneHeaderPatch? HeaderPatch = null,
    byte[]? Payload = null,
    int TargetIndex = 0)
{
    public static GenomeEditOperation EditHeader(int geneIndex, GeneHeaderPatch patch)
        => new(GenomeEditOperationKind.EditHeader, geneIndex, HeaderPatch: patch);

    public static GenomeEditOperation ReplacePayload(int geneIndex, byte[] payload)
        => new(GenomeEditOperationKind.ReplacePayload, geneIndex, Payload: payload.ToArray());

    public static GenomeEditOperation DuplicateGene(int geneIndex)
        => new(GenomeEditOperationKind.DuplicateGene, geneIndex);

    public static GenomeEditOperation DeleteGene(int geneIndex)
        => new(GenomeEditOperationKind.DeleteGene, geneIndex);

    public static GenomeEditOperation MoveGene(int fromIndex, int toIndex)
        => new(GenomeEditOperationKind.MoveGene, fromIndex, TargetIndex: toIndex);
}

public sealed class GenomeEditSession
{
    private readonly Stack<byte[]> _undo = new();
    private readonly Stack<byte[]> _redo = new();

    public GenomeEditSession(GenomeDocument document, IRng rng)
    {
        Document = document;
    }

    public GenomeDocument Document { get; }

    public CrossoverReport? CrossoverReport { get; private init; }

    public MutationReport? MutationReport { get; private init; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Apply(GenomeEditOperation operation)
    {
        byte[] before = Document.WorkingRawBytes;
        byte[] after = ApplyOperation(before, operation);
        Document.ReplaceWorkingRaw(after);
        _undo.Push(before);
        _redo.Clear();
    }

    public void Undo()
    {
        if (!CanUndo)
            return;

        byte[] current = Document.WorkingRawBytes;
        byte[] previous = _undo.Pop();
        Document.ReplaceWorkingRaw(previous);
        _redo.Push(current);
    }

    public void Redo()
    {
        if (!CanRedo)
            return;

        byte[] current = Document.WorkingRawBytes;
        byte[] next = _redo.Pop();
        Document.ReplaceWorkingRaw(next);
        _undo.Push(current);
    }

    public byte[] ExportFileBytes()
        => GenomeWriter.Serialize(Document.WorkingGenome);

    public GenomeDiff CreateDiff()
        => GenomeDiff.Compare(Document.SourceRecords, Document.WorkingRecords);

    public PhenotypeSummary CreatePhenotypeSummary()
        => PhenotypeSummarizer.Summarize(Document.WorkingRecords);

    public IReadOnlyList<GeneValidationIssue> Validate()
        => GeneValidator.ValidateRaw(Document.WorkingRawBytes);

    public static GenomeEditSession FromCrossover(
        string childMoniker,
        G mum,
        G dad,
        IRng rng,
        byte mumChanceOfMutation,
        byte mumDegreeOfMutation,
        byte dadChanceOfMutation,
        byte dadDegreeOfMutation)
    {
        var child = new G(rng);
        child.Cross(
            childMoniker,
            mum,
            dad,
            mumChanceOfMutation,
            mumDegreeOfMutation,
            dadChanceOfMutation,
            dadDegreeOfMutation);

        GenomeDocument document = GenomeDocument.FromGenome(child, rng);
        return new GenomeEditSession(document, rng)
        {
            CrossoverReport = CrossoverReport.Create(childMoniker, mum, dad, child),
            MutationReport = MutationReport.FromParentAndChild(mum, child),
        };
    }

    private byte[] ApplyOperation(byte[] rawBytes, GenomeEditOperation operation)
        => operation.Kind switch
        {
            GenomeEditOperationKind.EditHeader => GenomeRawEditor.ReplaceHeader(
                rawBytes,
                operation.GeneIndex,
                operation.HeaderPatch ?? throw new ArgumentException("Header patch is required.")),
            GenomeEditOperationKind.ReplacePayload => GenomeRawEditor.ReplacePayload(
                rawBytes,
                operation.GeneIndex,
                operation.Payload ?? throw new ArgumentException("Payload is required.")),
            GenomeEditOperationKind.DuplicateGene => GenomeRawEditor.DuplicateGene(rawBytes, operation.GeneIndex),
            GenomeEditOperationKind.DeleteGene => GenomeRawEditor.DeleteGene(rawBytes, operation.GeneIndex),
            GenomeEditOperationKind.MoveGene => GenomeRawEditor.MoveGene(rawBytes, operation.GeneIndex, operation.TargetIndex),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation.Kind, "Unknown genome edit operation.")
        };
}
