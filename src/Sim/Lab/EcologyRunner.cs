using System;
using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Lab;

public sealed record EcologyWorldPreset(
    string Name,
    float Temperature = 0.5f,
    float Light = 0.5f,
    float Radiation = 0.0f,
    float AirQuality = 1.0f)
{
    public static EcologyWorldPreset Neutral { get; } = new("neutral");

    public LabWorldPreset ToLabPreset()
        => new(Name, Temperature, Light, Radiation, AirQuality);
}

public sealed record EcologyPopulationPolicy(
    int PopulationCap = 16,
    bool BreedFirstPairEachGeneration = true,
    bool StopOnExtinction = true);

public sealed record EcologyRunConfig(
    int Seed,
    int Generations,
    int TicksPerGeneration,
    IReadOnlyList<LabCreatureSeed> Founders,
    EcologyWorldPreset? WorldPreset = null,
    EcologyPopulationPolicy? PopulationPolicy = null);

public sealed record EcologyRunSummary(
    int GenerationsRun,
    int LivingPopulation,
    int StillbornCount,
    int DeathCount,
    int ReproductionCount,
    bool Extinct);

public sealed record EcologyRunResult(
    EcologyRunConfig Config,
    IReadOnlyList<LabRunMetrics> Generations,
    EcologyRunSummary Summary,
    WorldEvolutionJournal Journal);

public sealed class EcologyRunner
{
    public EcologyRunResult Run(EcologyRunConfig config)
    {
        Validate(config);

        EcologyPopulationPolicy policy = config.PopulationPolicy ?? new EcologyPopulationPolicy();
        var generationResults = new List<LabRunMetrics>();
        var journal = new WorldEvolutionJournal();
        if (policy.PopulationCap <= 0)
        {
            return new EcologyRunResult(
                config,
                generationResults,
                new EcologyRunSummary(0, 0, 0, 0, 0, Extinct: true),
                journal);
        }

        IReadOnlyList<LabCreatureSeed> currentPopulation = config.Founders
            .Take(policy.PopulationCap)
            .ToArray();
        for (int generation = 0; generation < config.Generations; generation++)
        {
            if (currentPopulation.Count == 0)
            {
                if (policy.StopOnExtinction)
                    break;
                continue;
            }

            LabRunMetrics metrics = new LabRunner().Run(new LabRunConfig(
                Seed: config.Seed + generation,
                Ticks: config.TicksPerGeneration,
                Population: currentPopulation,
                WorldPreset: (config.WorldPreset ?? EcologyWorldPreset.Neutral).ToLabPreset(),
                BreedFirstPairOnStart: policy.BreedFirstPairEachGeneration));
            generationResults.Add(metrics);
            MergeJournal(journal, metrics.EvolutionJournal, generation);

            currentPopulation = currentPopulation
                .Take(Math.Min(policy.PopulationCap, Math.Max(0, metrics.FinalPopulation)))
                .ToArray();
        }

        int living = generationResults.LastOrDefault()?.FinalPopulation ?? currentPopulation.Count;
        int stillborns = generationResults.Sum(result => result.Stillborns.Count);
        int deaths = generationResults.Sum(result => result.Deaths);
        int reproductions = generationResults.Sum(result => result.EvolutionJournal.Events.Count(e => e.Kind == NaturalSelectionEventKind.Reproduction));

        return new EcologyRunResult(
            config,
            generationResults,
            new EcologyRunSummary(
                generationResults.Count,
                living,
                stillborns,
                deaths,
                reproductions,
                Extinct: living == 0),
            journal);
    }

    private static void MergeJournal(WorldEvolutionJournal target, WorldEvolutionJournal source, int generation)
    {
        int tickOffset = generation * 1_000_000;
        foreach (NaturalSelectionEvent selectionEvent in source.Events)
        {
            target.Record(selectionEvent with
            {
                Tick = selectionEvent.Tick + tickOffset,
                Detail = string.IsNullOrWhiteSpace(selectionEvent.Detail)
                    ? $"generation:{generation}"
                    : $"{selectionEvent.Detail};generation:{generation}"
            });
        }

        foreach (SurvivalMetricFrame frame in source.SurvivalFrames)
            target.RecordSurvivalFrame(frame with { Tick = frame.Tick + tickOffset });
        foreach (ReproductionMetricFrame frame in source.ReproductionFrames)
            target.RecordReproductionFrame(frame with { Tick = frame.Tick + tickOffset });
    }

    private static void Validate(EcologyRunConfig config)
    {
        if (config.Generations < 0)
            throw new ArgumentException("Ecology generation count cannot be negative.", nameof(config));
        if (config.TicksPerGeneration < 0)
            throw new ArgumentException("Ecology ticks per generation cannot be negative.", nameof(config));
        if (config.Founders == null)
            throw new ArgumentException("Ecology founders cannot be null.", nameof(config));
    }
}
