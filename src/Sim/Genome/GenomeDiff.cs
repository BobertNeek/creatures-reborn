using System;
using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Genome;

public enum GeneDiffKind
{
    Added,
    Removed,
    Changed,
    Duplicated,
    Reordered
}

public sealed record GeneDiffRecord(
    GeneDiffKind Kind,
    GeneIdentity Identity,
    int? LeftOffset,
    int? RightOffset,
    string Description);

public sealed class GenomeDiff
{
    private GenomeDiff(IReadOnlyList<GeneDiffRecord> records)
    {
        Records = records;
    }

    public IReadOnlyList<GeneDiffRecord> Records { get; }

    public static GenomeDiff Compare(Genome left, Genome right)
        => Compare(GeneDecoder.Decode(left), GeneDecoder.Decode(right));

    public static GenomeDiff Compare(IReadOnlyList<GeneRecord> left, IReadOnlyList<GeneRecord> right)
    {
        var records = new List<GeneDiffRecord>();
        var leftByIdentity = left.GroupBy(g => g.Identity).ToDictionary(g => g.Key, g => g.ToList());
        var rightByIdentity = right.GroupBy(g => g.Identity).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var identity in leftByIdentity.Keys.Union(rightByIdentity.Keys).OrderBy(k => k.Type).ThenBy(k => k.Subtype).ThenBy(k => k.Id).ThenBy(k => k.Generation))
        {
            bool hasLeft = leftByIdentity.TryGetValue(identity, out var leftGenes);
            bool hasRight = rightByIdentity.TryGetValue(identity, out var rightGenes);

            if (!hasLeft && hasRight)
            {
                foreach (var gene in rightGenes!)
                    records.Add(new(GeneDiffKind.Added, identity, null, gene.Offset, $"Added {gene.DisplayName}."));
                continue;
            }

            if (hasLeft && !hasRight)
            {
                foreach (var gene in leftGenes!)
                    records.Add(new(GeneDiffKind.Removed, identity, gene.Offset, null, $"Removed {gene.DisplayName}."));
                continue;
            }

            int pairCount = Math.Min(leftGenes!.Count, rightGenes!.Count);
            for (int i = 0; i < pairCount; i++)
            {
                var leftGene = leftGenes[i];
                var rightGene = rightGenes[i];
                if (!leftGene.RawBytes.SequenceEqual(rightGene.RawBytes))
                {
                    records.Add(new(
                        GeneDiffKind.Changed,
                        identity,
                        leftGene.Offset,
                        rightGene.Offset,
                        $"Changed {leftGene.DisplayName}."));
                }
            }

            if (leftGenes.Count > rightGenes.Count)
            {
                foreach (var gene in leftGenes.Skip(rightGenes.Count))
                    records.Add(new(GeneDiffKind.Removed, identity, gene.Offset, null, $"Removed duplicate {gene.DisplayName}."));
            }
            else if (rightGenes.Count > leftGenes.Count)
            {
                foreach (var gene in rightGenes.Skip(leftGenes.Count))
                    records.Add(new(GeneDiffKind.Added, identity, null, gene.Offset, $"Added duplicate {gene.DisplayName}."));
            }
        }

        AddDuplicateRecords(left, "left", records);
        AddDuplicateRecords(right, "right", records);
        AddReorderedRecords(left, right, records);

        return new GenomeDiff(records);
    }

    private static void AddDuplicateRecords(
        IReadOnlyList<GeneRecord> genes,
        string side,
        List<GeneDiffRecord> records)
    {
        foreach (var group in genes.GroupBy(g => g.FamilyIdentity).Where(g => g.Count() > 1))
        {
            var first = group.First();
            records.Add(new(
                GeneDiffKind.Duplicated,
                first.Identity,
                side == "left" ? first.Offset : null,
                side == "right" ? first.Offset : null,
                $"{GeneNames.DisplayName(first.Type, first.Subtype)} appears {group.Count()} times in the {side} genome for ID {first.Id}."));
        }
    }

    private static void AddReorderedRecords(
        IReadOnlyList<GeneRecord> left,
        IReadOnlyList<GeneRecord> right,
        List<GeneDiffRecord> records)
    {
        var rightIndexByIdentity = right
            .Select((gene, index) => (gene.Identity, index))
            .GroupBy(item => item.Identity)
            .ToDictionary(group => group.Key, group => group.Select(item => item.index).ToList());

        var seen = new Dictionary<GeneIdentity, int>();
        for (int leftIndex = 0; leftIndex < left.Count; leftIndex++)
        {
            var gene = left[leftIndex];
            int occurrence = seen.TryGetValue(gene.Identity, out int count) ? count : 0;
            seen[gene.Identity] = occurrence + 1;

            if (!rightIndexByIdentity.TryGetValue(gene.Identity, out var rightIndexes) || occurrence >= rightIndexes.Count)
                continue;

            int rightIndex = rightIndexes[occurrence];
            if (rightIndex != leftIndex)
            {
                records.Add(new(
                    GeneDiffKind.Reordered,
                    gene.Identity,
                    gene.Offset,
                    right[rightIndex].Offset,
                    $"{gene.DisplayName} moved from gene index {leftIndex} to {rightIndex}."));
            }
        }
    }
}
