using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class C3DsParityReportTests
{
    [Fact]
    public void FixtureSet_DiscoverFindsGenomesRecursivelyAndSortsThem()
    {
        string root = Path.Combine(Path.GetTempPath(), $"c3ds-fixtures-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "nested"));
            File.WriteAllBytes(Path.Combine(root, "zeta.gen"), C3DsBiologyParityTests.RawGenome(C3DsBiologyParityTests.Organ()));
            File.WriteAllBytes(Path.Combine(root, "nested", "alpha.gen"), C3DsBiologyParityTests.RawGenome(C3DsBiologyParityTests.Organ()));
            File.WriteAllText(Path.Combine(root, "ignored.txt"), "not a genome");

            C3DsFixtureSet fixtureSet = C3DsFixtureSet.Discover(root);

            Assert.Equal(root, fixtureSet.RootPath);
            string[] names = fixtureSet.Genomes.Select(genome => Path.GetFileName(genome.Path)).ToArray();
            Assert.Collection(
                names,
                name => Assert.Equal("alpha.gen", name),
                name => Assert.Equal("zeta.gen", name));
            Assert.All(fixtureSet.Genomes, genome => Assert.True(genome.Bytes.Length > 0));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ParityReport_RecordsImportBootBiochemistryBrainAndTraceSummary()
    {
        byte[] rawGenome = C3DsBiologyParityTests.RawGenome(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP),
            C3DsBiologyParityTests.Receptor(ChemID.Injury, 3, 0));
        C3DsParityReport report = C3DsParityReport.Create("synthetic.gen", rawGenome);

        Assert.Equal("synthetic.gen", report.GenomeName);
        Assert.True(report.ImportSucceeded);
        Assert.True(report.BootSucceeded);
        Assert.True(report.GeneCount >= 6);
        Assert.True(report.OrganCount > 0);
        Assert.True(report.LobeCount > 0);
        Assert.True(report.TractCount > 0);
        Assert.Equal(BiochemistryCompatibilityMode.C3DS, report.BiochemistryMode);
        Assert.True(report.FirstTickChemicalDeltaCount >= 0);
        Assert.DoesNotContain(report.Issues, issue => issue.Kind == C3DsParityIssueKind.Unsupported && issue.Message.Contains("standard", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReportWriter_ExportsJsonAndMarkdown()
    {
        C3DsParityReport report = C3DsParityReport.Create(
            "synthetic.gen",
            C3DsBiologyParityTests.RawGenome(
                C3DsBiologyParityTests.Lobe("driv", 4),
                C3DsBiologyParityTests.Lobe("decn", 4),
                C3DsBiologyParityTests.Tract("driv", "decn"),
                C3DsBiologyParityTests.Organ()));

        string json = C3DsParityReportWriter.ToJson([report]);
        string markdown = C3DsParityReportWriter.ToMarkdown([report]);

        Assert.Contains("\"genomeName\"", json);
        Assert.Contains("synthetic.gen", json);
        Assert.Contains("# C3/DS Parity Report", markdown);
        Assert.Contains("synthetic.gen", markdown);
    }
}
