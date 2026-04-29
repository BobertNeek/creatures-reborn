using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Save;
using CreaturesReborn.Sim.Util;
using CreaturesReborn.Sim.World;
using CreatureSim = CreaturesReborn.Sim.Creature.Creature;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Lab;

public sealed record LabCreatureSeed(
    string GenomePath,
    string Moniker,
    int Sex = GeneConstants.MALE,
    byte Age = 0,
    int Variant = 0,
    byte[]? GenomeBytes = null);

public sealed record LabWorldPreset(
    string Name,
    float Temperature = 0.5f,
    float Light = 0.5f,
    float Radiation = 0.0f,
    float AirQuality = 1.0f)
{
    public static LabWorldPreset Neutral { get; } = new("neutral");

    public CreatureEnvironmentContext ToEnvironmentContext()
        => new(
            Math.Clamp(Temperature, 0.0f, 1.0f),
            Math.Clamp(Light, 0.0f, 1.0f),
            Math.Clamp(Radiation, 0.0f, 1.0f));

    public float ClampedAirQuality => Math.Clamp(AirQuality, 0.0f, 1.0f);
}

public sealed record LabRunConfig(
    int Seed,
    int Ticks,
    IReadOnlyList<LabCreatureSeed> Population,
    LabWorldPreset? WorldPreset = null,
    bool BreedFirstPairOnStart = false,
    byte MumChanceOfMutation = 4,
    byte MumDegreeOfMutation = 4,
    byte DadChanceOfMutation = 4,
    byte DadDegreeOfMutation = 4);

public enum LineageBirthOutcome
{
    Living = 0,
    Stillborn = 1,
    Quarantined = 2
}

public sealed record LineageRecord(
    string Moniker,
    string? MotherMoniker,
    string? FatherMoniker,
    int BirthTick,
    int Generation,
    string GenomeMoniker,
    LineageBirthOutcome Outcome = LineageBirthOutcome.Living);

public sealed record LabChemicalValue(
    int ChemicalId,
    string Token,
    string DisplayName,
    float Value);

public sealed record LabChemicalSummary(IReadOnlyList<LabChemicalValue> Values)
{
    public float Get(int chemicalId)
        => Values.FirstOrDefault(value => value.ChemicalId == chemicalId)?.Value ?? 0.0f;
}

public sealed record LabBrainMetrics(
    int LobeCount,
    int TractCount,
    int ModuleCount,
    int InstinctsRemaining);

public sealed record LabBehaviorMetrics(
    int TicksObserved,
    int FinalVerb,
    int FinalNoun,
    int UniqueVerbCount,
    int UniqueNounCount);

public sealed record LabEnvironmentMetrics(
    float Temperature,
    float Light,
    float Radiation,
    float Hotness,
    float Coldness,
    float ComfortNeed,
    float Stress,
    float AirQuality,
    float Suffocation);

public sealed record LabCreatureMetrics(
    string Moniker,
    int Sex,
    byte Age,
    bool Dead,
    LabChemicalSummary Chemicals,
    LabBrainMetrics Brain,
    LabBehaviorMetrics Behavior,
    LabEnvironmentMetrics Environment);

public sealed record LabCreatureGenomeRecord(
    string Moniker,
    int Sex,
    byte Age,
    int Variant,
    bool Dead,
    byte[] GenomeBytes);

public sealed record LabRunMetrics(
    int Seed,
    int TicksRequested,
    int TicksRun,
    string WorldPresetName,
    int InitialPopulation,
    int FinalPopulation,
    int Births,
    int Deaths,
    IReadOnlyList<LineageRecord> Lineage,
    IReadOnlyList<StillbornRecord> Stillborns,
    IReadOnlyList<LabCreatureMetrics> Creatures,
    IReadOnlyList<CrossoverReport> CrossoverReports,
    IReadOnlyList<MutationReport> MutationReports,
    WorldEvolutionJournal EvolutionJournal,
    IReadOnlyList<LabCreatureGenomeRecord> PopulationGenomes);

public sealed class LabRunner
{
    private static readonly int[] WatchedChemicals =
    {
        ChemID.ATP,
        ChemID.Glycogen,
        ChemID.Adipose,
        ChemID.Muscle,
        ChemID.HungerForCarb,
        ChemID.HungerForProtein,
        ChemID.HungerForFat,
        ChemID.Oxygen,
        ChemID.Hotness,
        ChemID.Coldness,
        ChemID.Pain,
        ChemID.Injury,
        ChemID.Tiredness,
        ChemID.Sleepiness,
        ChemID.Fear,
        ChemID.Reward,
        ChemID.Punishment
    };

    public LabRunMetrics Run(LabRunConfig config)
    {
        Validate(config);

        LabWorldPreset worldPreset = config.WorldPreset ?? LabWorldPreset.Neutral;
        var world = new GameWorld();
        var states = new List<CreatureRunState>();
        var lineage = new List<LineageRecord>();
        var stillborns = new List<StillbornRecord>();
        var crossoverReports = new List<CrossoverReport>();
        var mutationReports = new List<MutationReport>();
        var evolutionJournal = new WorldEvolutionJournal();

        LoadFounders(config, states, lineage, evolutionJournal);
        int initialPopulation = states.Count;
        int births = 0;

        if (config.BreedFirstPairOnStart && TryBreedFirstPair(config, states, lineage, stillborns, crossoverReports, mutationReports, evolutionJournal))
            births++;

        int deaths = Tick(config, worldPreset, world, states, stillborns, evolutionJournal);
        IReadOnlyList<LabCreatureMetrics> creatureMetrics = states.Select(CreateMetrics).ToArray();
        IReadOnlyList<LabCreatureGenomeRecord> populationGenomes = states.Select(CreateGenomeRecord).ToArray();

        return new LabRunMetrics(
            Seed: config.Seed,
            TicksRequested: config.Ticks,
            TicksRun: config.Ticks,
            WorldPresetName: worldPreset.Name,
            InitialPopulation: initialPopulation,
            FinalPopulation: states.Count,
            Births: births,
            Deaths: deaths,
            Lineage: lineage,
            Stillborns: stillborns,
            Creatures: creatureMetrics,
            CrossoverReports: crossoverReports,
            MutationReports: mutationReports,
            EvolutionJournal: evolutionJournal,
            PopulationGenomes: populationGenomes);
    }

    private static void LoadFounders(
        LabRunConfig config,
        List<CreatureRunState> states,
        List<LineageRecord> lineage,
        WorldEvolutionJournal evolutionJournal)
    {
        for (int i = 0; i < config.Population.Count; i++)
        {
            LabCreatureSeed seed = config.Population[i];
            string moniker = string.IsNullOrWhiteSpace(seed.Moniker)
                ? $"founder-{i + 1:D4}"
                : seed.Moniker;
            IRng rng = new Rng(config.Seed + i + 1);
            CreatureSim creature;
            if (seed.GenomeBytes is { Length: > 0 } genomeBytes)
            {
                var genome = new G(rng);
                GenomeReader.Load(genome, genomeBytes, seed.Sex, age: 0, seed.Variant, moniker);
                creature = CreatureSim.CreateFromGenome(genome, rng);
                creature.Genome.Age = seed.Age;
            }
            else
            {
                if (!File.Exists(seed.GenomePath))
                    throw new FileNotFoundException($"Lab genome path does not exist: {seed.GenomePath}", seed.GenomePath);

                creature = CreatureSim.LoadFromFile(
                    seed.GenomePath,
                    rng,
                    seed.Sex,
                    seed.Age,
                    seed.Variant,
                    moniker);
            }

            states.Add(new CreatureRunState(creature));
            lineage.Add(new LineageRecord(
                moniker,
                MotherMoniker: null,
                FatherMoniker: null,
                BirthTick: 0,
                Generation: 0,
                GenomeMoniker: creature.Genome.Moniker));
            evolutionJournal.Record(new NaturalSelectionEvent(
                Tick: 0,
                NaturalSelectionEventKind.Birth,
                moniker,
                Detail: "founder"));
        }
    }

    private static bool TryBreedFirstPair(
        LabRunConfig config,
        List<CreatureRunState> states,
        List<LineageRecord> lineage,
        List<StillbornRecord> stillborns,
        List<CrossoverReport> crossoverReports,
        List<MutationReport> mutationReports,
        WorldEvolutionJournal evolutionJournal)
    {
        if (states.Count < 2)
            return false;

        CreatureSim? mother = states.Select(state => state.Creature)
            .FirstOrDefault(creature => creature.Genome.Sex == GeneConstants.FEMALE);
        CreatureSim? father = states.Select(state => state.Creature)
            .FirstOrDefault(creature => creature.Genome.Sex == GeneConstants.MALE);
        if (mother == null || father == null)
            return false;

        string childMoniker = "lab-child-0001";
        var childGenome = new G(new Rng(config.Seed + 10_000));
        childGenome.Cross(
            childMoniker,
            mother.Genome,
            father.Genome,
            config.MumChanceOfMutation,
            config.MumDegreeOfMutation,
            config.DadChanceOfMutation,
            config.DadDegreeOfMutation);

        crossoverReports.Add(CrossoverReport.Create(childMoniker, mother.Genome, father.Genome, childGenome));
        mutationReports.Add(MutationReport.FromParentAndChild(mother.Genome, childGenome));
        evolutionJournal.Record(new NaturalSelectionEvent(
            Tick: 0,
            NaturalSelectionEventKind.Reproduction,
            mother.Genome.Moniker,
            father.Genome.Moniker,
            $"child:{childMoniker}"));
        evolutionJournal.Record(new NaturalSelectionEvent(
            Tick: 0,
            NaturalSelectionEventKind.Crossover,
            childMoniker,
            Detail: "first-pair breeding"));
        evolutionJournal.Record(new NaturalSelectionEvent(
            Tick: 0,
            NaturalSelectionEventKind.Mutation,
            childMoniker,
            Detail: "first-pair breeding"));

        byte[] childGenomeBytes = childGenome.AsSpan().ToArray();
        HatchResult hatch = CreatureHatchService.AttemptHatch(
            new EggGenomePayload(
                childGenomeBytes,
                childGenome.Sex,
                childGenome.Variant,
                childMoniker),
            new HatchAttemptContext(
                childMoniker,
                mother.Genome.Moniker,
                father.Genome.Moniker,
                BirthTick: 0,
                Generation: 1),
            new Rng(config.Seed + 10_001));

        if (hatch.Creature != null)
        {
            CreatureSim child = hatch.Creature;
            states.Add(new CreatureRunState(child));
            lineage.Add(new LineageRecord(
                childMoniker,
                mother.Genome.Moniker,
                father.Genome.Moniker,
                BirthTick: 0,
                Generation: 1,
                GenomeMoniker: child.Genome.Moniker,
                Outcome: LineageBirthOutcome.Living));
            evolutionJournal.Record(new NaturalSelectionEvent(
                Tick: 0,
                NaturalSelectionEventKind.Birth,
                childMoniker,
                Detail: "offspring"));
            evolutionJournal.Record(new NaturalSelectionEvent(
                Tick: 0,
                NaturalSelectionEventKind.EggHatched,
                childMoniker,
                Detail: "living child"));
            evolutionJournal.RecordReproductionFrame(new ReproductionMetricFrame(
                Tick: 0,
                Births: 1,
                Stillbirths: 0,
                LivingChildren: 1));
            return true;
        }

        if (hatch.Stillborn != null)
        {
            stillborns.Add(hatch.Stillborn);
            lineage.Add(new LineageRecord(
                childMoniker,
                mother.Genome.Moniker,
                father.Genome.Moniker,
                BirthTick: 0,
                Generation: 1,
                GenomeMoniker: childMoniker,
                Outcome: hatch.Outcome == HatchOutcome.Quarantined
                    ? LineageBirthOutcome.Quarantined
                    : LineageBirthOutcome.Stillborn));
            evolutionJournal.Record(new NaturalSelectionEvent(
                Tick: 0,
                NaturalSelectionEventKind.Stillbirth,
                childMoniker,
                Detail: hatch.Stillborn.Reason.ToString()));
            evolutionJournal.RecordReproductionFrame(new ReproductionMetricFrame(
                Tick: 0,
                Births: 1,
                Stillbirths: 1,
                LivingChildren: 0));
            return true;
        }

        return false;
    }

    private static int Tick(
        LabRunConfig config,
        LabWorldPreset worldPreset,
        GameWorld world,
        List<CreatureRunState> states,
        List<StillbornRecord> stillborns,
        WorldEvolutionJournal evolutionJournal)
    {
        int deaths = 0;
        CreatureEnvironmentContext environment = worldPreset.ToEnvironmentContext();
        float airQuality = worldPreset.ClampedAirQuality;

        for (int tick = 0; tick < config.Ticks; tick++)
        {
            world.Tick();
            foreach (CreatureRunState state in states)
            {
                if (state.Dead)
                    continue;

                CreatureEnvironmentResponse response = state.Creature.ApplyEnvironment(environment);
                float suffocation = state.Creature.ApplyAirQuality(airQuality);
                state.Environment = new LabEnvironmentMetrics(
                    response.Temperature,
                    response.Light,
                    response.Radiation,
                    response.Hotness,
                    response.Coldness,
                    response.ComfortNeed,
                    response.Stress,
                    airQuality,
                    suffocation);
                state.Creature.Tick();
                state.RecordTick();

                if (IsDead(state.Creature))
                {
                    state.Dead = true;
                    deaths++;
                    evolutionJournal.Record(new NaturalSelectionEvent(
                        Tick: tick + 1,
                        NaturalSelectionEventKind.Death,
                        state.Creature.Genome.Moniker,
                        Detail: "dead locus set"));
                }
            }

            evolutionJournal.RecordSurvivalFrame(new SurvivalMetricFrame(
                Tick: tick + 1,
                LivingPopulation: states.Count(state => !state.Dead),
                DeadCount: deaths,
                StillbornCount: stillborns.Count));
        }

        return deaths;
    }

    private static LabCreatureMetrics CreateMetrics(CreatureRunState state)
    {
        BrainSnapshot snapshot = state.Creature.Brain.CreateSnapshot(new BrainSnapshotOptions(
            MaxNeuronsPerLobe: 0,
            MaxDendritesPerTract: 0));

        return new LabCreatureMetrics(
            state.Creature.Genome.Moniker,
            state.Creature.Genome.Sex,
            state.Creature.Genome.Age,
            state.Dead,
            CreateChemicalSummary(state.Creature),
            new LabBrainMetrics(
                snapshot.Lobes.Count,
                snapshot.Tracts.Count,
                snapshot.Modules.Count,
                snapshot.InstinctsRemaining),
            new LabBehaviorMetrics(
                state.TicksObserved,
                state.Creature.Motor.CurrentVerb,
                state.Creature.Motor.CurrentNoun,
                state.UniqueVerbs.Count,
                state.UniqueNouns.Count),
            state.Environment);
    }

    private static LabCreatureGenomeRecord CreateGenomeRecord(CreatureRunState state)
    {
        SavedCreatureState snapshot = state.Creature.CreateSnapshot();
        return new LabCreatureGenomeRecord(
            state.Creature.Genome.Moniker,
            state.Creature.Genome.Sex,
            state.Creature.Genome.Age,
            state.Creature.Genome.Variant,
            state.Dead,
            snapshot.GenomeBytes);
    }

    private static LabChemicalSummary CreateChemicalSummary(CreatureSim creature)
    {
        var values = new List<LabChemicalValue>(WatchedChemicals.Length);
        foreach (int chemicalId in WatchedChemicals)
        {
            ChemicalDefinition definition = ChemicalCatalog.Get(chemicalId);
            values.Add(new LabChemicalValue(
                chemicalId,
                definition.Token,
                definition.DisplayName,
                creature.GetChemical(chemicalId)));
        }

        return new LabChemicalSummary(values);
    }

    private static bool IsDead(CreatureSim creature)
        => creature.Biochemistry.GetCreatureLocus((int)CreatureTissue.Immune, ImmuneLocus.Dead).Value > 0.5f;

    private static void Validate(LabRunConfig config)
    {
        if (config.Ticks < 0)
            throw new ArgumentException("Lab run tick count cannot be negative.", nameof(config));
        if (config.Population == null || config.Population.Count == 0)
            throw new ArgumentException("Lab run requires at least one creature seed.", nameof(config));
    }

    private sealed class CreatureRunState
    {
        public CreatureRunState(CreatureSim creature)
        {
            Creature = creature;
        }

        public CreatureSim Creature { get; }
        public HashSet<int> UniqueVerbs { get; } = new();
        public HashSet<int> UniqueNouns { get; } = new();
        public int TicksObserved { get; private set; }
        public bool Dead { get; set; }
        public LabEnvironmentMetrics Environment { get; set; } = new(
            Temperature: 0.5f,
            Light: 0.5f,
            Radiation: 0.0f,
            Hotness: 0.0f,
            Coldness: 0.0f,
            ComfortNeed: 0.0f,
            Stress: 0.0f,
            AirQuality: 1.0f,
            Suffocation: 0.0f);

        public void RecordTick()
        {
            TicksObserved++;
            UniqueVerbs.Add(Creature.Motor.CurrentVerb);
            UniqueNouns.Add(Creature.Motor.CurrentNoun);
        }
    }
}
