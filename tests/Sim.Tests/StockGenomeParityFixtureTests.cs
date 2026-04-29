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
        string[] genomePaths = FixtureGenomePaths();
        if (genomePaths.Length == 0)
            return;

        foreach (string path in genomePaths.Take(12))
        {
            C3DsGenomeImportResult import = C3DsGenomeImporter.ImportFile(path);
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
                    Path.GetFileNameWithoutExtension(path),
                    BiochemistryCompatibilityMode.C3DS));
            creature.Tick(new CreatureTraceOptions(IncludeBiochemistryTrace: true));
            Assert.Equal(BiochemistryCompatibilityMode.C3DS, creature.Biochemistry.CompatibilityMode);
        }
    }

    [Fact]
    public void ConfiguredC3DsFixtureGenomes_HaveDocumentedStandardChemicalCatalog()
    {
        string[] genomePaths = FixtureGenomePaths();
        if (genomePaths.Length == 0)
            return;

        foreach (int id in Enumerable.Range(0, BiochemConst.NUMCHEM))
        {
            StandardChemicalDefinition definition = StandardChemicalCatalog.Get(id);
            Assert.Equal(id, definition.Id);
            Assert.False(string.IsNullOrWhiteSpace(definition.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(definition.Source));
        }
    }

    private static string[] FixtureGenomePaths()
    {
        string? root = Environment.GetEnvironmentVariable("CREATURES_C3DS_FIXTURES");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return [];

        return Directory.GetFiles(root, "*.gen", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
