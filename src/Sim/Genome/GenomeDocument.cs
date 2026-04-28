using System;
using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Genome;

/// <summary>
/// Editable genome document with immutable source bytes and a separate working copy.
/// The raw genome remains the compatibility floor; edits rebuild the working genome
/// from byte records rather than mutating a live creature genome in place.
/// </summary>
public sealed class GenomeDocument
{
    private readonly IRng _rng;
    private readonly int _sex;
    private readonly byte _age;
    private readonly int _variant;
    private readonly string _moniker;

    private GenomeDocument(
        byte[] sourceFileBytes,
        G sourceGenome,
        G workingGenome,
        IRng rng,
        int sex,
        byte age,
        int variant,
        string moniker)
    {
        SourceFileBytes = sourceFileBytes.ToArray();
        SourceGenome = sourceGenome;
        WorkingGenome = workingGenome;
        _rng = rng;
        _sex = sex;
        _age = age;
        _variant = variant;
        _moniker = moniker;
        RefreshRecords();
    }

    public G SourceGenome { get; }

    public G WorkingGenome { get; private set; }

    public byte[] SourceFileBytes { get; }

    public byte[] SourceRawBytes => SourceGenome.AsSpan().ToArray();

    public byte[] WorkingRawBytes => WorkingGenome.AsSpan().ToArray();

    public IReadOnlyList<GeneRecord> SourceRecords { get; private set; } = Array.Empty<GeneRecord>();

    public IReadOnlyList<GeneRecord> WorkingRecords { get; private set; } = Array.Empty<GeneRecord>();

    public static GenomeDocument FromGenome(G genome, IRng rng)
        => FromFileBytes(
            GenomeWriter.Serialize(genome),
            rng,
            genome.Sex,
            genome.Age,
            genome.Variant,
            genome.Moniker);

    public static GenomeDocument FromFileBytes(
        byte[] fileBytes,
        IRng rng,
        int sex = GeneConstants.MALE,
        byte age = 0,
        int variant = 0,
        string moniker = "")
    {
        G source = LoadGenome(fileBytes, rng, sex, age, variant, moniker);
        G working = LoadGenome(fileBytes, rng, sex, age, variant, moniker);
        return new GenomeDocument(fileBytes, source, working, rng, sex, age, variant, moniker);
    }

    internal void ReplaceWorkingRaw(byte[] rawBytes)
    {
        var fileBytes = GenomeRawEditor.ToFileBytes(rawBytes);
        WorkingGenome = LoadGenome(fileBytes, _rng, _sex, _age, _variant, _moniker);
        RefreshRecords();
    }

    private void RefreshRecords()
    {
        SourceRecords = GeneDecoder.Decode(SourceGenome);
        WorkingRecords = GeneDecoder.Decode(WorkingGenome);
    }

    private static G LoadGenome(byte[] fileBytes, IRng rng, int sex, byte age, int variant, string moniker)
    {
        var genome = new G(rng);
        GenomeReader.Load(genome, fileBytes.ToArray(), sex, age, variant, moniker);
        return genome;
    }
}

internal static class GenomeRawEditor
{
    private static readonly byte[] EndMarker =
    [
        (byte)'g',
        (byte)'e',
        (byte)'n',
        (byte)'d'
    ];

    public static byte[] ToFileBytes(byte[] rawBytes)
    {
        byte[] fileBytes = new byte[4 + rawBytes.Length];
        int token = GeneConstants.DNA3TOKEN;
        fileBytes[0] = (byte)(token & 0xFF);
        fileBytes[1] = (byte)((token >> 8) & 0xFF);
        fileBytes[2] = (byte)((token >> 16) & 0xFF);
        fileBytes[3] = (byte)((token >> 24) & 0xFF);
        rawBytes.CopyTo(fileBytes.AsSpan(4));
        return fileBytes;
    }

    public static byte[] ReplaceHeader(byte[] rawBytes, int geneIndex, GeneHeaderPatch patch)
        => Rewrite(rawBytes, genes =>
        {
            byte[] gene = CheckedGene(genes, geneIndex).ToArray();
            patch.Apply(gene);
            genes[geneIndex] = gene;
        });

    public static byte[] ReplacePayload(byte[] rawBytes, int geneIndex, byte[] payload)
        => Rewrite(rawBytes, genes =>
        {
            byte[] gene = CheckedGene(genes, geneIndex);
            var edited = new byte[GeneHeaderOffsets.GH_LENGTH + payload.Length];
            Array.Copy(gene, edited, GeneHeaderOffsets.GH_LENGTH);
            payload.CopyTo(edited.AsSpan(GeneHeaderOffsets.GH_LENGTH));
            genes[geneIndex] = edited;
        });

    public static byte[] DuplicateGene(byte[] rawBytes, int geneIndex)
        => Rewrite(rawBytes, genes =>
        {
            byte[] gene = CheckedGene(genes, geneIndex);
            genes.Add(gene.ToArray());
        });

    public static byte[] DeleteGene(byte[] rawBytes, int geneIndex)
        => Rewrite(rawBytes, genes =>
        {
            CheckedGene(genes, geneIndex);
            genes.RemoveAt(geneIndex);
        });

    public static byte[] MoveGene(byte[] rawBytes, int fromIndex, int toIndex)
        => Rewrite(rawBytes, genes =>
        {
            byte[] gene = CheckedGene(genes, fromIndex);
            genes.RemoveAt(fromIndex);
            int insertAt = Math.Clamp(toIndex, 0, genes.Count);
            genes.Insert(insertAt, gene);
        });

    private static byte[] Rewrite(byte[] rawBytes, Action<List<byte[]>> edit)
    {
        List<byte[]> genes = GeneDecoder.DecodeRaw(rawBytes)
            .Select(record => record.RawBytes.ToArray())
            .ToList();

        edit(genes);

        int length = genes.Sum(gene => gene.Length) + EndMarker.Length;
        var result = new byte[length];
        int offset = 0;
        foreach (byte[] gene in genes)
        {
            gene.CopyTo(result.AsSpan(offset));
            offset += gene.Length;
        }

        EndMarker.CopyTo(result.AsSpan(offset));
        return result;
    }

    private static byte[] CheckedGene(IReadOnlyList<byte[]> genes, int geneIndex)
    {
        if ((uint)geneIndex >= (uint)genes.Count)
            throw new ArgumentOutOfRangeException(nameof(geneIndex), $"Gene index {geneIndex} is outside 0..{genes.Count - 1}.");
        return genes[geneIndex];
    }
}
