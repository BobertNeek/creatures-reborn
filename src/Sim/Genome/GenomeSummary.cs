using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Genome;

public sealed class GenomeSummary
{
    private readonly Dictionary<int, int> _typeCounts;
    private readonly Dictionary<(int Type, int Subtype), int> _subtypeCounts;

    private GenomeSummary(
        IReadOnlyList<GeneRecord> genes,
        Dictionary<int, int> typeCounts,
        Dictionary<(int Type, int Subtype), int> subtypeCounts)
    {
        Genes = genes;
        _typeCounts = typeCounts;
        _subtypeCounts = subtypeCounts;
    }

    public IReadOnlyList<GeneRecord> Genes { get; }

    public IReadOnlyDictionary<int, int> TypeCounts => _typeCounts;

    public IReadOnlyDictionary<(int Type, int Subtype), int> SubtypeCounts => _subtypeCounts;

    public int TotalGenes => Genes.Count;

    public int Count(GeneType type)
        => Count((int)type);

    public int Count(int type)
        => _typeCounts.TryGetValue(type, out int count) ? count : 0;

    public int Count(GeneType type, int subtype)
        => Count((int)type, subtype);

    public int Count(int type, int subtype)
        => _subtypeCounts.TryGetValue((type, subtype), out int count) ? count : 0;

    public IReadOnlyDictionary<string, int> FamilyCounts()
        => _typeCounts.ToDictionary(pair => GeneNames.TypeName(pair.Key), pair => pair.Value);

    public static GenomeSummary Create(Genome genome)
        => Create(GeneDecoder.Decode(genome));

    public static GenomeSummary Create(IReadOnlyList<GeneRecord> genes)
    {
        var typeCounts = new Dictionary<int, int>();
        var subtypeCounts = new Dictionary<(int Type, int Subtype), int>();

        foreach (var gene in genes)
        {
            typeCounts[gene.Type] = typeCounts.TryGetValue(gene.Type, out int typeCount)
                ? typeCount + 1
                : 1;

            var key = (gene.Type, gene.Subtype);
            subtypeCounts[key] = subtypeCounts.TryGetValue(key, out int subtypeCount)
                ? subtypeCount + 1
                : 1;
        }

        return new GenomeSummary(genes, typeCounts, subtypeCounts);
    }
}
