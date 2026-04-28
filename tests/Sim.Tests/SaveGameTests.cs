using System;
using System.IO;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Save;
using CreaturesReborn.Sim.Settings;
using CreaturesReborn.Sim.Util;
using Xunit;
using C = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Sim.Tests;

public class SaveGameTests
{
    private static readonly string StarterGenomePath =
        RepoPath("data", "genomes", "starter.gen");

    [Fact]
    public void StatefulRng_RoundTripsAndContinuesSequence()
    {
        var rng = new StatefulRng(12345);
        _ = rng.Rnd(1000);
        StatefulRngState state = rng.CreateState();
        int expectedNext = rng.Rnd(1000);

        var restored = StatefulRng.FromState(state);

        Assert.Equal(expectedNext, restored.Rnd(1000));
    }

    [Fact]
    public void CreatureSnapshot_RestoresExactBiologyState()
    {
        var rng = new StatefulRng(42);
        C creature = C.LoadFromFile(StarterGenomePath, rng, GeneConstants.FEMALE, age: 128, moniker: "Ada");
        creature.SetChemical(ChemID.ATP, 0.42f);
        creature.InjectChemical(ChemID.Pain, 0.21f);
        creature.Motor.SuggestVerb(VerbId.Eat);
        creature.Tick();

        SavedCreatureState saved = creature.CreateSnapshot();
        creature.SetChemical(ChemID.ATP, 1.0f);
        creature.SetChemical(ChemID.Pain, 1.0f);
        creature.Tick();

        C restored = C.RestoreSnapshot(saved);
        SavedCreatureState roundTrip = restored.CreateSnapshot();

        Assert.Equal(saved.Moniker, restored.Genome.Moniker);
        Assert.Equal(saved.Sex, restored.Genome.Sex);
        Assert.Equal(saved.Age, restored.Genome.Age);
        Assert.Equal(saved.Biochemistry.Chemicals, roundTrip.Biochemistry.Chemicals);
        Assert.Equal(saved.Brain.Lobes[0].Neurons[0].States, roundTrip.Brain.Lobes[0].Neurons[0].States);
        Assert.Equal(saved.Brain.Tracts[0].Dendrites[0].Weights, roundTrip.Brain.Tracts[0].Dendrites[0].Weights);
        Assert.Equal(saved.Motor.CurrentVerb, roundTrip.Motor.CurrentVerb);
    }

    [Fact]
    public void GameSaveService_RoundTripsSaveDataAndSummaries()
    {
        string root = Path.Combine(Path.GetTempPath(), "creatures-save-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var data = new GameSaveData
            {
                Slot = 2,
                SlotName = "Garden Lab",
                WorldLabel = "Bundled World",
                SavedAtUtc = new DateTimeOffset(2026, 4, 27, 12, 0, 0, TimeSpan.Zero),
                MetaroomPaths = ["res://data/metarooms/treehouse.json", "res://data/metarooms/forest.json"],
                WorldTick = 1234,
                BreedingLimit = 8,
                TotalPopulationMax = 24,
                TickRate = 30.0f,
                Creatures =
                [
                    C.LoadFromFile(StarterGenomePath, new StatefulRng(7), GeneConstants.MALE, age: 128, moniker: "Newton")
                        .CreateSnapshot()
                ],
            };

            var service = new GameSaveService(root);
            string path = service.Save(data);
            GameSaveData loaded = service.Load(path);
            SaveSlotSummary summary = Assert.Single(service.ListSlots());

            Assert.Equal(2, loaded.Slot);
            Assert.Equal("Garden Lab", loaded.SlotName);
            Assert.Equal(1234, loaded.WorldTick);
            Assert.Equal(8, loaded.BreedingLimit);
            Assert.Equal(24, loaded.TotalPopulationMax);
            Assert.Equal(30.0f, loaded.TickRate);
            Assert.Single(loaded.Creatures);
            Assert.True(summary.IsValid);
            Assert.Equal("Garden Lab", summary.SlotName);
            Assert.Equal("Bundled World", summary.WorldLabel);
            Assert.Equal(1, summary.CreatureCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GameSaveService_ListSlotsReportsCorruptSaveWithoutThrowing()
    {
        string root = Path.Combine(Path.GetTempPath(), "creatures-save-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(Path.Combine(root, "slot-3.creatures-save.json"), "{ broken json");
            var service = new GameSaveService(root);

            SaveSlotSummary summary = Assert.Single(service.ListSlots());

            Assert.False(summary.IsValid);
            Assert.Contains("slot-3", summary.SlotName);
            Assert.False(string.IsNullOrWhiteSpace(summary.Error));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GameSettings_NormalizeClampsMenuAndSimulationValues()
    {
        GameSettings settings = new GameSettings
        {
            FpsCap = 500,
            UiScale = 4.0f,
            TextScale = 0.1f,
            MasterVolume = 4.0f,
            MaxCreatures = 1000,
            BreedingLimit = 1000,
            SimulationSpeed = 0,
            GravityStrength = 120,
        }.Normalize();

        Assert.Equal(240, settings.FpsCap);
        Assert.Equal(2.0f, settings.UiScale);
        Assert.Equal(0.85f, settings.TextScale);
        Assert.Equal(1.0f, settings.MasterVolume);
        Assert.Equal(128, settings.MaxCreatures);
        Assert.Equal(64, settings.BreedingLimit);
        Assert.Equal(1.0f, settings.SimulationSpeed);
        Assert.Equal(80.0f, settings.GravityStrength);
    }

    private static string RepoPath(params string[] parts)
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            Path.Combine(parts)));
}
