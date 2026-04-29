using System;
using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Sim.Lab;

public sealed record ChemicalRlTrainingConfig(
    int Seed,
    int Episodes,
    EcologyRunConfig EcologyConfig,
    GenomeExtensionDocument InitialExtensions);

public sealed record ChemicalRlObservation(
    int Tick,
    IReadOnlyList<float> Chemicals,
    IReadOnlyList<float> ChemicalDeltas,
    int CurrentVerb,
    int CurrentNoun,
    int AgeStage,
    string EnvironmentLabel);

public sealed record ChemicalRlAction(
    IReadOnlyList<GenomeExtensionGene> ExtensionGeneChanges);

public sealed record ChemicalRlRewardVector(
    float SurvivalTime,
    float ReproductionSuccess,
    float ChildSurvival,
    float HungerReduction,
    float PainAvoidance,
    float ToxinAvoidance,
    float RestQuality,
    float ExplorationDiversity,
    float SocialSuccess,
    float StillbornPenalty,
    float DiversityBonus)
{
    public float ScalarForRanking()
        => SurvivalTime
           + ReproductionSuccess
           + ChildSurvival
           + HungerReduction
           + PainAvoidance
           + ToxinAvoidance
           + RestQuality
           + ExplorationDiversity
           + SocialSuccess
           + DiversityBonus
           - StillbornPenalty;
}

public sealed record ChemicalRlEpisode(
    int Index,
    ChemicalRlObservation Observation,
    ChemicalRlAction Action,
    ChemicalRlRewardVector Reward,
    GenomeExtensionDocument CandidateExtensions);

public sealed record ChemicalRlTrainingResult(
    IReadOnlyList<ChemicalRlEpisode> Episodes,
    GenomeExtensionDocument BestExtensions,
    ChemicalRlRewardVector BestReward);

public static class ChemicalRlPolicyAdapter
{
    public static ChemicalRlObservation ObservationFromEcologyResult(EcologyRunResult result)
    {
        LabCreatureMetrics? creature = result.Generations.LastOrDefault()?.Creatures.FirstOrDefault();
        IReadOnlyList<float> chemicals = creature?.Chemicals.Values.Select(value => value.Value).ToArray()
                                         ?? Array.Empty<float>();
        return new ChemicalRlObservation(
            Tick: result.Generations.Sum(generation => generation.TicksRun),
            Chemicals: chemicals,
            ChemicalDeltas: Array.Empty<float>(),
            CurrentVerb: creature?.Behavior.FinalVerb ?? 0,
            CurrentNoun: creature?.Behavior.FinalNoun ?? 0,
            AgeStage: creature?.Age ?? 0,
            EnvironmentLabel: result.Config.WorldPreset?.Name ?? EcologyWorldPreset.Neutral.Name);
    }

    public static ChemicalRlRewardVector RewardFromEcologyResult(EcologyRunResult result)
    {
        float ticks = result.Generations.Sum(generation => generation.TicksRun);
        float births = result.Summary.ReproductionCount;
        float stillborns = result.Summary.StillbornCount;
        float diversity = result.Journal.Events.Select(e => e.Moniker).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return new ChemicalRlRewardVector(
            SurvivalTime: ticks,
            ReproductionSuccess: births,
            ChildSurvival: Math.Max(0, result.Summary.LivingPopulation - result.Config.Founders.Count),
            HungerReduction: 0,
            PainAvoidance: 0,
            ToxinAvoidance: 0,
            RestQuality: 0,
            ExplorationDiversity: diversity,
            SocialSuccess: 0,
            StillbornPenalty: stillborns,
            DiversityBonus: diversity * 0.1f);
    }
}

public static class ChemicalRlGenomeExporter
{
    public static GenomeExtensionDocument Export(ChemicalRlAction action, GenomeExtensionDocument baseDocument)
    {
        var byKey = baseDocument.Genes.ToDictionary(gene => gene.Key, StringComparer.OrdinalIgnoreCase);
        foreach (GenomeExtensionGene change in action.ExtensionGeneChanges)
            byKey[change.Key] = change with { Value = GenomeExtensionMutation.ClampValue(change.Kind, change.Value) };

        return new GenomeExtensionDocument(
            GenomeExtensionDocument.CurrentSchemaVersion,
            byKey.Values.OrderBy(gene => gene.Key, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}

public sealed class ChemicalRlNoOpTrainer
{
    public ChemicalRlTrainingResult Run(ChemicalRlTrainingConfig config)
    {
        Validate(config);
        var episodes = new List<ChemicalRlEpisode>();
        GenomeExtensionDocument bestExtensions = config.InitialExtensions;
        ChemicalRlRewardVector bestReward = EmptyReward();

        for (int i = 0; i < config.Episodes; i++)
        {
            EcologyRunResult ecology = new EcologyRunner().Run(config.EcologyConfig with { Seed = config.Seed + i });
            ChemicalRlRewardVector reward = ChemicalRlPolicyAdapter.RewardFromEcologyResult(ecology);
            var action = new ChemicalRlAction(Array.Empty<GenomeExtensionGene>());
            var episode = new ChemicalRlEpisode(
                i,
                ChemicalRlPolicyAdapter.ObservationFromEcologyResult(ecology),
                action,
                reward,
                config.InitialExtensions);
            episodes.Add(episode);

            if (i == 0 || reward.ScalarForRanking() > bestReward.ScalarForRanking())
            {
                bestReward = reward;
                bestExtensions = config.InitialExtensions;
            }
        }

        return new ChemicalRlTrainingResult(episodes, bestExtensions, bestReward);
    }

    private static ChemicalRlRewardVector EmptyReward()
        => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static void Validate(ChemicalRlTrainingConfig config)
    {
        if (config.Episodes < 0)
            throw new ArgumentException("Training episode count cannot be negative.", nameof(config));
    }
}

public sealed class ChemicalRlEvolutionaryTrainer
{
    public ChemicalRlTrainingResult Run(ChemicalRlTrainingConfig config)
    {
        Validate(config);
        var rng = new Rng(config.Seed);
        var episodes = new List<ChemicalRlEpisode>();
        GenomeExtensionDocument bestExtensions = config.InitialExtensions;
        ChemicalRlRewardVector bestReward = new(0, 0, 0, 0, 0, 0, 0, 0, 0, float.MaxValue, 0);

        for (int i = 0; i < config.Episodes; i++)
        {
            (GenomeExtensionDocument candidate, _) = GenomeExtensionMutation.Mutate(
                bestExtensions,
                rng,
                mutationChance: 0.5f,
                mutationDegree: 0.15f);
            EcologyRunResult ecology = new EcologyRunner().Run(config.EcologyConfig with { Seed = config.Seed + i });
            ChemicalRlRewardVector reward = ChemicalRlPolicyAdapter.RewardFromEcologyResult(ecology);
            var action = new ChemicalRlAction(candidate.Genes);
            episodes.Add(new ChemicalRlEpisode(
                i,
                ChemicalRlPolicyAdapter.ObservationFromEcologyResult(ecology),
                action,
                reward,
                candidate));

            if (i == 0 || reward.ScalarForRanking() >= bestReward.ScalarForRanking())
            {
                bestReward = reward;
                bestExtensions = candidate;
            }
        }

        return new ChemicalRlTrainingResult(episodes, bestExtensions, bestReward);
    }

    private static void Validate(ChemicalRlTrainingConfig config)
    {
        if (config.Episodes < 0)
            throw new ArgumentException("Training episode count cannot be negative.", nameof(config));
    }
}
