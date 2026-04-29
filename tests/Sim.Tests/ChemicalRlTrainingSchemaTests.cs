using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Lab;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class ChemicalRlTrainingSchemaTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void DataModel_HoldsObservationActionAndRewardVector()
    {
        var observation = new ChemicalRlObservation(
            Tick: 12,
            Chemicals: [0.1f, 0.2f],
            ChemicalDeltas: [0.05f],
            CurrentVerb: 1,
            CurrentNoun: 2,
            AgeStage: 0,
            EnvironmentLabel: "neutral");
        var action = new ChemicalRlAction([
            new GenomeExtensionGene("reinforcement.hunger", GenomeExtensionGeneKind.ChemicalReinforcementWeight, 1.25f)
        ]);
        var reward = new ChemicalRlRewardVector(10, 1, 1, 0.5f, 0, 0, 0, 2, 0, 0, 0.25f);

        var episode = new ChemicalRlEpisode(0, observation, action, reward, GenomeExtensionDocument.Empty);

        Assert.Equal(12, episode.Observation.Tick);
        Assert.Single(episode.Action.ExtensionGeneChanges);
        Assert.True(episode.Reward.ScalarForRanking() > 0.0f);
    }

    [Fact]
    public void NoOpTrainer_RecordsEpisodesDeterministically()
    {
        ChemicalRlTrainingConfig config = CreateConfig(seed: 700, episodes: 2);
        var trainer = new ChemicalRlNoOpTrainer();

        ChemicalRlTrainingResult first = trainer.Run(config);
        ChemicalRlTrainingResult second = trainer.Run(config);

        Assert.Equal(2, first.Episodes.Count);
        Assert.Equal(
            first.Episodes.Select(e => e.Reward),
            second.Episodes.Select(e => e.Reward));
        Assert.Equal(first.BestReward, second.BestReward);
    }

    [Fact]
    public void EvolutionaryTrainer_ExportsInspectableExtensionGenes()
    {
        ChemicalRlTrainingConfig config = CreateConfig(seed: 701, episodes: 3);

        ChemicalRlTrainingResult result = new ChemicalRlEvolutionaryTrainer().Run(config);

        Assert.Equal(3, result.Episodes.Count);
        Assert.NotEmpty(result.BestExtensions.Genes);
        Assert.Empty(GenomeExtensionValidator.Validate(result.BestExtensions));
        Assert.All(result.BestExtensions.Genes, gene => Assert.True(float.IsFinite(gene.Value)));
    }

    [Fact]
    public void GenomeExporter_AppliesTrainingActionWithoutOpaquePolicy()
    {
        GenomeExtensionDocument baseDocument = CreateExtensions();
        var action = new ChemicalRlAction([
            new GenomeExtensionGene("plasticity.rate", GenomeExtensionGeneKind.PlasticityLearningRate, 99.0f),
            new GenomeExtensionGene("module.plasticity", GenomeExtensionGeneKind.ModuleEnablement, 1.0f)
        ]);

        GenomeExtensionDocument exported = ChemicalRlGenomeExporter.Export(action, baseDocument);

        Assert.Contains(exported.Genes, gene => gene.Key == "plasticity.rate" && gene.Value <= 4.0f);
        Assert.Contains(exported.Genes, gene => gene.Key == "module.plasticity" && gene.Value == 1.0f);
        Assert.Empty(GenomeExtensionValidator.Validate(exported));
    }

    private static ChemicalRlTrainingConfig CreateConfig(int seed, int episodes)
        => new(
            Seed: seed,
            Episodes: episodes,
            EcologyConfig: new EcologyRunConfig(
                Seed: seed,
                Generations: 1,
                TicksPerGeneration: 2,
                Founders:
                [
                    new LabCreatureSeed(Path.GetFullPath(StarterGenomePath), "rl-mum", GeneConstants.FEMALE),
                    new LabCreatureSeed(Path.GetFullPath(StarterGenomePath), "rl-dad", GeneConstants.MALE)
                ]),
            InitialExtensions: CreateExtensions());

    private static GenomeExtensionDocument CreateExtensions()
        => new(GenomeExtensionDocument.CurrentSchemaVersion, [
            new("brain.profile", GenomeExtensionGeneKind.BrainProfileSelection, 1.0f),
            new("reinforcement.hunger", GenomeExtensionGeneKind.ChemicalReinforcementWeight, 1.0f),
            new("plasticity.rate", GenomeExtensionGeneKind.PlasticityLearningRate, 0.2f)
        ]);
}
