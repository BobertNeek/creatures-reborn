namespace CreaturesReborn.Sim.Genome;

public sealed record CrossoverReport(
    string ChildMoniker,
    string MumMoniker,
    string DadMoniker,
    int CrossCount,
    int MutationCount,
    GenomeSummary ChildSummary,
    GenomeDiff MumDiff,
    GenomeDiff DadDiff)
{
    public static CrossoverReport Create(string childMoniker, Genome mum, Genome dad, Genome child)
        => new(
            childMoniker,
            mum.Moniker,
            dad.Moniker,
            child.CrossoverCrossCount,
            child.CrossoverMutationCount,
            GenomeSummary.Create(child),
            GenomeDiff.Compare(mum, child),
            GenomeDiff.Compare(dad, child));
}

public sealed record MutationReport(
    string ParentMoniker,
    string ChildMoniker,
    int ChangedGeneCount,
    GenomeDiff Diff)
{
    public static MutationReport FromParentAndChild(Genome parent, Genome child)
    {
        var diff = GenomeDiff.Compare(parent, child);
        return new MutationReport(parent.Moniker, child.Moniker, diff.Records.Count, diff);
    }
}
