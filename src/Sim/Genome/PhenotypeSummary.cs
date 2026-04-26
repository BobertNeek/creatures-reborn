using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Genome;

public sealed record PhenotypeSection(string Name, IReadOnlyList<string> Lines);

public sealed class PhenotypeSummary
{
    public PhenotypeSummary(IReadOnlyDictionary<string, PhenotypeSection> sections)
    {
        Sections = sections;
    }

    public IReadOnlyDictionary<string, PhenotypeSection> Sections { get; }
}

public static class PhenotypeSummarizer
{
    public static PhenotypeSummary Summarize(Genome genome)
        => Summarize(GeneDecoder.Decode(genome));

    public static PhenotypeSummary Summarize(IReadOnlyList<GeneRecord> genes)
    {
        var sections = new Dictionary<string, PhenotypeSection>();

        AddSection(
            sections,
            "brain structure",
            genes,
            gene => gene.Type == (int)GeneType.BRAINGENE,
            "Brain genes configure lobes, brain organs, tracts, dendrites, and classic SVRule substrate.");

        AddSection(
            sections,
            "organ chemistry",
            genes,
            gene => gene.Type == (int)GeneType.BIOCHEMISTRYGENE || gene.Type == (int)GeneType.ORGANGENE,
            "Biochemistry and organ genes configure receptors, emitters, reactions, half-lives, injections, neuroemitters, and organ health.");

        AddSection(
            sections,
            "stimulus learning",
            genes,
            gene => gene.Type == (int)GeneType.CREATUREGENE &&
                    (gene.Subtype == (int)CreatureSubtype.G_STIMULUS || gene.Subtype == (int)CreatureSubtype.G_INSTINCT),
            "Stimulus and instinct genes shape chemical reinforcement and early learned behavior.");

        AddSection(
            sections,
            "appearance",
            genes,
            gene => gene.Type == (int)GeneType.CREATUREGENE &&
                    (gene.Subtype == (int)CreatureSubtype.G_APPEARANCE ||
                     gene.Subtype == (int)CreatureSubtype.G_PIGMENT ||
                     gene.Subtype == (int)CreatureSubtype.G_PIGMENTBLEED ||
                     gene.Subtype == (int)CreatureSubtype.G_EXPRESSION ||
                     gene.Subtype == (int)CreatureSubtype.G_POSE ||
                     gene.Subtype == (int)CreatureSubtype.G_GAIT),
            "Appearance, pigment, expression, pose, and gait genes affect visible creature phenotype.");

        AddSection(
            sections,
            "reproduction",
            genes,
            gene => gene.Type == (int)GeneType.CREATUREGENE && gene.Subtype == (int)CreatureSubtype.G_GENUS,
            "Genus and header-style creature genes identify reproductive family and lineage-relevant creature metadata.");

        return new PhenotypeSummary(sections);
    }

    private static void AddSection(
        Dictionary<string, PhenotypeSection> sections,
        string name,
        IReadOnlyList<GeneRecord> genes,
        System.Func<GeneRecord, bool> predicate,
        string description)
    {
        var matching = genes.Where(predicate).ToList();
        if (matching.Count == 0)
            return;

        var lines = new List<string> { description };
        lines.AddRange(matching
            .GroupBy(gene => gene.DisplayName)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}: {group.Count()} gene(s)"));

        sections[name] = new PhenotypeSection(name, lines);
    }
}
