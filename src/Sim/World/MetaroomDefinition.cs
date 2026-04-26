using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreaturesReborn.Sim.World;

public enum MetaroomPathKind
{
    Floor,
    Ramp,
    Stair,
}

public enum MetaroomObjectKind
{
    Door,
    Elevator,
    Home,
    FoodSpawn,
    FoodDispenser,
    Toy,
    Incubator,
    NornSpawn,
}

public enum DoorTransitionMode
{
    Portal,
    AdjacentEdge,
}

public sealed class MetaroomPoint
{
    public MetaroomPoint() { }

    public MetaroomPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; set; }
    public float Y { get; set; }
}

public sealed class MetaroomDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string BackgroundPath { get; set; } = "";
    public int BackgroundWidth { get; set; }
    public int BackgroundHeight { get; set; }
    public float WorldX { get; set; }
    public float WorldY { get; set; }
    public float WorldWidth { get; set; } = 40;
    public float WorldHeight { get; set; } = 13.5f;
    public float BackdropCenterX { get; set; }
    public float BackdropCenterY { get; set; }
    public List<MetaroomPathDefinition> Paths { get; set; } = new();
    public List<MetaroomObjectDefinition> Objects { get; set; } = new();
}

public sealed class MetaroomPathDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public MetaroomPathKind Kind { get; set; } = MetaroomPathKind.Floor;
    public RoomType RoomType { get; set; } = RoomType.IndoorWood;
    public float CeilingOffset { get; set; } = 3;
    public bool Enabled { get; set; } = true;
    public List<MetaroomPoint> Points { get; set; } = new();
}

public sealed class MetaroomObjectDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public MetaroomObjectKind Kind { get; set; }
    public MetaroomPoint Position { get; set; } = new();
    public float Scale { get; set; } = 1;
    public float RotationDegrees { get; set; }
    public bool Enabled { get; set; } = true;
    public DoorDefinition? Door { get; set; }
    public ElevatorDefinition? Elevator { get; set; }
    public HomeDefinition? Home { get; set; }
    public FoodDefinition? Food { get; set; }
    public GadgetDefinition? Gadget { get; set; }
}

public sealed class DoorDefinition
{
    public string TargetMetaroomId { get; set; } = "";
    public string TargetDoorId { get; set; } = "";
    public DoorTransitionMode TransitionMode { get; set; } = DoorTransitionMode.Portal;
    public bool Bidirectional { get; set; } = true;
    public float CaptureRadius { get; set; } = 0.8f;
    public MetaroomPoint ExitOffset { get; set; } = new();
    public int Permeability { get; set; } = 100;
}

public sealed class ElevatorDefinition
{
    public float YLow { get; set; }
    public float YHigh { get; set; }
    public float CaptureRadius { get; set; } = 0.9f;
    public float TravelSpeed { get; set; } = 3;
    public int StartFloor { get; set; } = 1;
}

public sealed class HomeDefinition
{
    public float Radius { get; set; } = 3;
    public float LonelinessSuppress { get; set; } = 0.25f;
    public float BoredomSuppress { get; set; } = 0.15f;
}

public sealed class FoodDefinition
{
    public string FoodKind { get; set; } = "Fruit";
    public float GlycogenAmount { get; set; } = 0.3f;
    public float ATPAmount { get; set; } = -1;
    public float RespawnDelay { get; set; } = 25;
}

public sealed class GadgetDefinition
{
    public string GadgetType { get; set; } = "RobotToy";
    public float ScanRadius { get; set; } = 6;
    public float TimerInterval { get; set; } = 10;
}

public static class MetaroomDefinitionJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(MetaroomDefinition definition)
        => JsonSerializer.Serialize(definition, Options);

    public static MetaroomDefinition Deserialize(string json)
        => JsonSerializer.Deserialize<MetaroomDefinition>(json, Options)
           ?? throw new InvalidDataException("Metaroom JSON did not contain a definition.");

    public static MetaroomDefinition Load(string path)
        => Deserialize(File.ReadAllText(path));

    public static void Save(string path, MetaroomDefinition definition)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, Serialize(definition));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
