using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Sim.Save;

public sealed class GameSaveData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int Slot { get; set; } = 1;
    public string SlotName { get; set; } = "Save Slot";
    public string WorldLabel { get; set; } = "World";
    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<string> MetaroomPaths { get; set; } = new();
    public int WorldTick { get; set; }
    public int Day { get; set; }
    public int Season { get; set; }
    public int Year { get; set; }
    public float TimeOfDay { get; set; }
    public int BreedingLimit { get; set; } = 6;
    public int TotalPopulationMax { get; set; } = 16;
    public float TickRate { get; set; } = 20.0f;
    public float GravityAcceleration { get; set; } = 18.0f;
    public List<SavedCreatureState> Creatures { get; set; } = new();
    public List<SavedFoodState> Foods { get; set; } = new();
    public List<SavedEggState> Eggs { get; set; } = new();
}

public sealed class SaveSlotSummary
{
    public int Slot { get; set; }
    public string SlotName { get; set; } = "";
    public string WorldLabel { get; set; } = "";
    public DateTimeOffset? SavedAtUtc { get; set; }
    public int CreatureCount { get; set; }
    public string Path { get; set; } = "";
    public bool IsValid { get; set; }
    public string? Error { get; set; }
}

public sealed class SavedCreatureState
{
    public string Moniker { get; set; } = "";
    public string GenomePath { get; set; } = "";
    public byte[] GenomeBytes { get; set; } = Array.Empty<byte>();
    public int Sex { get; set; }
    public byte Age { get; set; }
    public int Variant { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float WalkSpeed { get; set; } = 1.0f;
    public float VerticalVelocity { get; set; }
    public int AgeTickAccumulator { get; set; }
    public StatefulRngState? RngState { get; set; }
    public SavedBiochemistryState Biochemistry { get; set; } = new();
    public SavedBrainState Brain { get; set; } = new();
    public SavedMotorState Motor { get; set; } = new();
}

public sealed class SavedBiochemistryState
{
    public float[] Chemicals { get; set; } = Array.Empty<float>();
}

public sealed class SavedBrainState
{
    public List<SavedLobeState> Lobes { get; set; } = new();
    public List<SavedTractState> Tracts { get; set; } = new();
    public int InstinctsRemaining { get; set; }
    public bool IsProcessingInstincts { get; set; }
}

public sealed class SavedLobeState
{
    public int Index { get; set; }
    public int Token { get; set; }
    public int WinningNeuronId { get; set; }
    public List<SavedNeuronState> Neurons { get; set; } = new();
}

public sealed class SavedNeuronState
{
    public int Index { get; set; }
    public float[] States { get; set; } = Array.Empty<float>();
}

public sealed class SavedTractState
{
    public int Index { get; set; }
    public float STtoLTRate { get; set; }
    public List<SavedDendriteState> Dendrites { get; set; } = new();
}

public sealed class SavedDendriteState
{
    public int Index { get; set; }
    public float[] Weights { get; set; } = Array.Empty<float>();
}

public sealed class SavedMotorState
{
    public int CurrentVerb { get; set; }
    public int CurrentNoun { get; set; }
    public int CurrentGait { get; set; }
    public int CurrentPose { get; set; }
}

public sealed class SavedFoodState
{
    public string FoodKind { get; set; } = "Fruit";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float GlycogenAmount { get; set; }
    public float ATPAmount { get; set; }
}

public sealed class SavedEggState
{
    public string GenomePath { get; set; } = "";
    public byte[] GenomeBytes { get; set; } = Array.Empty<byte>();
    public int Sex { get; set; }
    public float Age { get; set; }
    public float HatchTime { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public sealed class GameSaveService
{
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly string _saveDirectory;

    public GameSaveService(string saveDirectory)
    {
        _saveDirectory = saveDirectory;
    }

    public string Save(GameSaveData data)
    {
        Directory.CreateDirectory(_saveDirectory);
        data.SchemaVersion = GameSaveData.CurrentSchemaVersion;
        data.SavedAtUtc = data.SavedAtUtc == default ? DateTimeOffset.UtcNow : data.SavedAtUtc;
        string path = SlotPath(data.Slot);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
        return path;
    }

    public GameSaveData Load(string path)
    {
        GameSaveData? data = JsonSerializer.Deserialize<GameSaveData>(File.ReadAllText(path), JsonOptions);
        if (data == null)
            throw new InvalidDataException($"Save file '{path}' did not contain save data.");
        if (data.SchemaVersion != GameSaveData.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported save schema {data.SchemaVersion}.");
        return data;
    }

    public IReadOnlyList<SaveSlotSummary> ListSlots()
    {
        Directory.CreateDirectory(_saveDirectory);
        return Directory.EnumerateFiles(_saveDirectory, "slot-*.creatures-save.json")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(ReadSummary)
            .ToArray();
    }

    public string SlotPath(int slot)
        => Path.Combine(_saveDirectory, $"slot-{Math.Clamp(slot, 1, 99)}.creatures-save.json");

    private SaveSlotSummary ReadSummary(string path)
    {
        try
        {
            GameSaveData data = Load(path);
            return new SaveSlotSummary
            {
                Slot = data.Slot,
                SlotName = data.SlotName,
                WorldLabel = data.WorldLabel,
                SavedAtUtc = data.SavedAtUtc,
                CreatureCount = data.Creatures.Count,
                Path = path,
                IsValid = true,
            };
        }
        catch (Exception ex)
        {
            return new SaveSlotSummary
            {
                Path = path,
                SlotName = Path.GetFileNameWithoutExtension(path),
                IsValid = false,
                Error = ex.Message,
            };
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
