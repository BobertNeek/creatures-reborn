using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using CreatureSim = CreaturesReborn.Sim.Creature.Creature;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class StockGenomeParityFixtureTests
{
    [Fact]
    public void ConfiguredC3DsFixtureGenomes_ImportValidateAndBootInC3DsMode()
    {
        C3DsFixtureSet fixtureSet = FixtureSet();
        if (fixtureSet.Genomes.Count == 0)
            return;

        foreach (C3DsFixtureGenome fixture in fixtureSet.Genomes)
        {
            C3DsGenomeImportResult import = C3DsGenomeImporter.ImportRaw(
                fixture.Bytes,
                new C3DsGenomeImportOptions(Moniker: Path.GetFileNameWithoutExtension(fixture.Path)));
            GenomeSimulationSafetyReport safety = GenomeSimulationSafetyValidator.Validate(
                import.Genome,
                new GenomeSimulationSafetyOptions(
                    Sex: import.Genome.Sex,
                    Age: import.Genome.Age,
                    Variant: import.Genome.Variant,
                    Biology: new MinimumBiologyInterfaceSpec(
                        RequireOrgan: false,
                        RequireEnergyReaction: false,
                        RequireDeathOrInjuryRoute: false)));

            Assert.Equal(BiochemistryCompatibilityMode.C3DS, import.CompatibilityProfile.BiochemistryMode);
            Assert.NotEmpty(import.Records);
            Assert.False(import.Report.HasErrors);
            Assert.DoesNotContain(safety.Issues, issue => issue.Code == GenomeSimulationSafetyCode.MalformedGenome);

            CreatureSim creature = CreatureSim.CreateFromGenome(
                import.Genome,
                new Rng(900),
                new CreatureImportOptions(
                    import.Genome.Sex,
                    import.Genome.Age,
                    import.Genome.Variant,
                    Path.GetFileNameWithoutExtension(fixture.Path),
                    BiochemistryCompatibilityMode.C3DS));
            CreatureTickTrace tickTrace = creature.Tick(new CreatureTraceOptions(IncludeBiochemistryTrace: true));
            Assert.Equal(BiochemistryCompatibilityMode.C3DS, creature.Biochemistry.CompatibilityMode);
            Assert.Empty(creature.Brain.GetModuleDescriptors());
            Assert.DoesNotContain(
                tickTrace.Biochemistry?.Deltas ?? [],
                delta => delta.Source is ChemicalDeltaSource.Metabolism
                    or ChemicalDeltaSource.Fatigue
                    or ChemicalDeltaSource.InjuryRecovery
                    or ChemicalDeltaSource.Environment
                    or ChemicalDeltaSource.Respiration
                    or ChemicalDeltaSource.Immune
                    or ChemicalDeltaSource.Toxin);
        }
    }

    [Fact]
    public void ConfiguredC3DsFixtureGenomes_HaveDocumentedStandardChemicalCatalog()
    {
        C3DsFixtureSet fixtureSet = FixtureSet();
        if (fixtureSet.Genomes.Count == 0)
            return;

        foreach (int id in Enumerable.Range(0, BiochemConst.NUMCHEM))
        {
            StandardChemicalDefinition definition = StandardChemicalCatalog.Get(id);
            Assert.Equal(id, definition.Id);
            Assert.False(string.IsNullOrWhiteSpace(definition.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(definition.Source));
        }
    }

    [Fact]
    public void ConfiguredC3DsFixtureGenomes_WriteParityReports()
    {
        C3DsFixtureSet fixtureSet = FixtureSet();
        if (fixtureSet.Genomes.Count == 0)
            return;

        C3DsParityReport[] reports = fixtureSet.Genomes
            .Select(fixture => C3DsParityReport.Create(fixture.Name, fixture.Bytes))
            .ToArray();
        string reportDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "artifacts", "c3ds-parity"));

        C3DsParityReportWriter.WriteAll(reportDir, reports);

        Assert.All(reports, report =>
        {
            Assert.True(report.ImportSucceeded, report.GenomeName);
            Assert.True(report.BootSucceeded, report.GenomeName);
            Assert.Equal(BiochemistryCompatibilityMode.C3DS, report.BiochemistryMode);
            Assert.DoesNotContain(report.Issues, issue => issue.Kind == C3DsParityIssueKind.BehaviorMismatch);
        });
        Assert.True(File.Exists(Path.Combine(reportDir, "c3ds-parity.json")));
        Assert.True(File.Exists(Path.Combine(reportDir, "c3ds-parity.md")));
    }

    private static C3DsFixtureSet FixtureSet()
    {
        string? root = Environment.GetEnvironmentVariable("CREATURES_C3DS_FIXTURES");
        return C3DsFixtureSet.Discover(root);
    }
}
