using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class GenomeExtensionTests
{
    [Fact]
    public void ExtensionDocument_RoundTripsWithoutChangingRawGenBytes()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP),
            C3DsBiologyParityTests.Receptor(ChemID.Injury, 3, 0));
        byte[] before = GenomeWriter.Serialize(genome);
        var document = new GenomeExtensionDocument(
            GenomeExtensionDocument.CurrentSchemaVersion,
            [
                new("brain.profile", GenomeExtensionGeneKind.BrainProfileSelection, 1.0f),
                new("reinforcement.hunger", GenomeExtensionGeneKind.ChemicalReinforcementWeight, 1.5f)
            ]);

        GenomeExtensionDocument restored = GenomeExtensionDocument.FromJson(document.ToJson());
        byte[] after = GenomeWriter.Serialize(genome);

        Assert.Equal(before, after);
        Assert.Equal(document.Genes.Count, restored.Genes.Count);
        Assert.Equal(1.5f, restored.Genes.Single(gene => gene.Key == "reinforcement.hunger").Value);
    }

    [Fact]
    public void ExtensionCrossover_InheritsDeterministicallyFromBothParents()
    {
        var mother = new GenomeExtensionDocument(1, [
            new("brain.profile", GenomeExtensionGeneKind.BrainProfileSelection, 1.0f),
            new("memory.capacity", GenomeExtensionGeneKind.MemoryCapacity, 2.0f)
        ]);
        var father = new GenomeExtensionDocument(1, [
            new("brain.profile", GenomeExtensionGeneKind.BrainProfileSelection, 0.0f),
            new("danger.sensitivity", GenomeExtensionGeneKind.DangerSensitivity, 3.0f)
        ]);

        (GenomeExtensionDocument childA, GenomeExtensionCrossoverReport reportA) =
            GenomeExtensionCrossover.Cross(mother, father, new Rng(10));
        (GenomeExtensionDocument childB, GenomeExtensionCrossoverReport reportB) =
            GenomeExtensionCrossover.Cross(mother, father, new Rng(10));

        Assert.Equal(childA.Genes, childB.Genes);
        Assert.Equal(reportA.InheritedFromMother, reportB.InheritedFromMother);
        Assert.Contains(childA.Genes, gene => gene.Key == "memory.capacity");
        Assert.Contains(childA.Genes, gene => gene.Key == "danger.sensitivity");
    }

    [Fact]
    public void ExtensionMutation_UsesInjectedRngAndClampsValues()
    {
        var document = new GenomeExtensionDocument(1, [
            new("lobe.scale", GenomeExtensionGeneKind.LobeSizeMultiplier, 1.0f),
            new("reinforcement.reward", GenomeExtensionGeneKind.ChemicalReinforcementWeight, 15.95f)
        ]);

        (GenomeExtensionDocument mutated, GenomeExtensionMutationReport report) =
            GenomeExtensionMutation.Mutate(document, new Rng(4), mutationChance: 1.0f, mutationDegree: 4.0f);

        Assert.Equal(2, report.MutatedGenes.Count);
        Assert.All(mutated.Genes, gene => Assert.True(float.IsFinite(gene.Value)));
        Assert.InRange(mutated.Genes.Single(gene => gene.Key == "lobe.scale").Value, 0.25f, 4.0f);
        Assert.InRange(mutated.Genes.Single(gene => gene.Key == "reinforcement.reward").Value, 0.0f, 16.0f);
    }

    [Fact]
    public void ExtensionValidator_RejectsInvalidRanges()
    {
        var document = new GenomeExtensionDocument(1, [
            new("bad.scale", GenomeExtensionGeneKind.LobeSizeMultiplier, 99.0f),
            new("bad.nan", GenomeExtensionGeneKind.MemoryCapacity, float.NaN)
        ]);

        var issues = GenomeExtensionValidator.Validate(document);

        Assert.Contains(issues, issue => issue.Key == "bad.scale");
        Assert.Contains(issues, issue => issue.Key == "bad.nan");
    }

    [Fact]
    public void ExtensionPhenotypeSummary_DescribesModernTraits()
    {
        var document = new GenomeExtensionDocument(1, [
            new("plasticity.rate", GenomeExtensionGeneKind.PlasticityLearningRate, 0.2f),
            new("module.plasticity", GenomeExtensionGeneKind.ModuleEnablement, 1.0f)
        ]);

        PhenotypeSection section = GenomeExtensionPhenotypeSummarizer.Summarize(document);

        Assert.Equal("modern extensions", section.Name);
        Assert.Contains(section.Lines, line => line.Contains("PlasticityLearningRate"));
        Assert.Contains(section.Lines, line => line.Contains("ModuleEnablement"));
    }
}
