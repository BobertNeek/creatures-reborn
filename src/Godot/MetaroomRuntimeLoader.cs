using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using CreaturesReborn.Godot.Agents;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Godot;

[GlobalClass]
public partial class MetaroomRuntimeLoader : Node
{
    [Export] public string[] DefinitionPaths = Array.Empty<string>();

    private readonly List<MetaroomDefinition> _definitions = new();

    public IReadOnlyList<MetaroomDefinition> Definitions => _definitions;
    public MetaroomBuildResult? LastBuildResult { get; private set; }
    public bool HasDefinitions
        => DefinitionPaths.Length > 0
           || MetaroomEditorSession.DefinitionPaths.Count > 0
           || _definitions.Count > 0;

    public void SetDefinitions(IEnumerable<MetaroomDefinition> definitions)
    {
        _definitions.Clear();
        _definitions.AddRange(definitions);
    }

    public MetaroomBuildResult LoadIntoWorld(WorldNode world)
    {
        if (_definitions.Count == 0)
            LoadDefinitionFiles();

        LastBuildResult = MetaroomWorldBuilder.Build(world.World.Map, _definitions);
        BuildScene(world, LastBuildResult);
        return LastBuildResult;
    }

    private void LoadDefinitionFiles()
    {
        _definitions.Clear();
        IReadOnlyList<string> paths = MetaroomEditorSession.DefinitionPaths.Count > 0
            ? MetaroomEditorSession.DefinitionPaths
            : DefinitionPaths;

        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string localPath = ToLocalPath(path);
            _definitions.Add(MetaroomDefinitionJson.Load(localPath));
        }
    }

    private void BuildScene(WorldNode world, MetaroomBuildResult buildResult)
    {
        Node? old = world.GetNodeOrNull<Node>("GeneratedMetarooms");
        if (old != null)
        {
            world.RemoveChild(old);
            old.QueueFree();
        }

        var root = new Node3D { Name = "GeneratedMetarooms" };
        world.AddChild(root);

        foreach (MetaroomDefinition definition in _definitions)
        {
            var metaroomRoot = new Node3D { Name = $"Metaroom_{definition.Id}" };
            root.AddChild(metaroomRoot);
            BuildBackground(metaroomRoot, definition);
            BuildPathHelpers(metaroomRoot, definition);

            for (int i = 0; i < definition.Objects.Count; i++)
                BuildEditableObject(world, definition, definition.Objects[i], buildResult);
        }

        foreach (Node child in world.GetChildren())
            if (child is FoodRespawnManager respawnManager)
                respawnManager.RegisterExistingFoodSpawnPoints();
    }

    private static void BuildBackground(Node3D parent, MetaroomDefinition definition)
    {
        Texture2D? texture = LoadTexture(definition.BackgroundPath);
        if (texture == null)
            return;

        var mat = new StandardMaterial3D
        {
            AlbedoTexture = texture,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        float width = definition.WorldWidth > 0 ? definition.WorldWidth : 40;
        float height = definition.WorldHeight > 0 ? definition.WorldHeight : 13.5f;
        var mesh = new MeshInstance3D
        {
            Name = "Backdrop",
            Mesh = new QuadMesh { Size = new Vector2(width, height) },
            Position = new Vector3(
                definition.WorldX + definition.BackdropCenterX,
                definition.WorldY + definition.BackdropCenterY,
                -5.0f),
            MaterialOverride = mat,
        };
        parent.AddChild(mesh);
    }

    private static void BuildPathHelpers(Node3D parent, MetaroomDefinition definition)
    {
        foreach (MetaroomPathDefinition path in definition.Paths)
        {
            if (!path.Enabled || path.Points.Count < 2)
                continue;

            for (int i = 0; i < path.Points.Count - 1; i++)
            {
                MetaroomPoint a = path.Points[i];
                MetaroomPoint b = path.Points[i + 1];
                if (MathF.Abs(a.X - b.X) < 0.001f)
                    continue;

                if (path.Kind == MetaroomPathKind.Stair)
                {
                    parent.AddChild(new StairsNode
                    {
                        Name = $"{path.Id}_Stair_{i}",
                        XLeft = definition.WorldX + a.X,
                        XRight = definition.WorldX + b.X,
                        YLeft = definition.WorldY + a.Y,
                        YRight = definition.WorldY + b.Y,
                        ShowVisual = false,
                    });
                }
                else
                {
                    parent.AddChild(new FloorPlateNode
                    {
                        Name = $"{path.Id}_Floor_{i}",
                        XLeft = definition.WorldX + a.X,
                        XRight = definition.WorldX + b.X,
                        YLeft = definition.WorldY + a.Y,
                        YRight = definition.WorldY + b.Y,
                        ShowDebug = false,
                    });
                }
            }
        }
    }

    private static void BuildEditableObject(
        Node3D parent,
        MetaroomDefinition definition,
        MetaroomObjectDefinition obj,
        MetaroomBuildResult buildResult)
    {
        if (!obj.Enabled)
            return;

        Vector3 position = new(definition.WorldX + obj.Position.X, definition.WorldY + obj.Position.Y, 0);
        Node3D? node = obj.Kind switch
        {
            MetaroomObjectKind.Door => BuildDoor(definition, obj, position, buildResult),
            MetaroomObjectKind.Elevator => BuildElevator(definition, obj, position),
            MetaroomObjectKind.Home => BuildHome(obj, position),
            MetaroomObjectKind.FoodSpawn => BuildFood(obj, position),
            MetaroomObjectKind.FoodDispenser => BuildGadget(obj, position, GadgetNode.GadgetType.EmpathicVendor),
            MetaroomObjectKind.Toy => BuildGadget(obj, position, GadgetNode.GadgetType.RobotToy),
            MetaroomObjectKind.Incubator => BuildIncubator(position),
            MetaroomObjectKind.NornSpawn => BuildNorn(position),
            _ => null,
        };

        if (node == null)
            return;

        node.Name = string.IsNullOrWhiteSpace(obj.Name) ? obj.Id : obj.Name;
        node.Scale = new Vector3(obj.Scale, obj.Scale, obj.Scale);
        node.RotationDegrees = new Vector3(0, 0, obj.RotationDegrees);
        parent.AddChild(node);
    }

    private static DoorNode BuildDoor(
        MetaroomDefinition definition,
        MetaroomObjectDefinition obj,
        Vector3 position,
        MetaroomBuildResult buildResult)
    {
        DoorDefinition? door = obj.Door;
        var node = new DoorNode
        {
            Position = position,
            MetaroomId = definition.Id,
            DoorId = obj.Id,
            TargetMetaroomId = door?.TargetMetaroomId ?? "",
            TargetDoorId = door?.TargetDoorId ?? "",
            CaptureRadius = door?.CaptureRadius ?? 0.8f,
            Enabled = door != null,
        };

        if (door != null
            && buildResult.Doors.TryGetValue(MetaroomWorldBuilder.DoorKey(door.TargetMetaroomId, door.TargetDoorId), out DoorEndpoint? target))
        {
            node.TargetWorldPosition = new Vector3(
                target.WorldX + door.ExitOffset.X,
                target.WorldY + door.ExitOffset.Y,
                0);
        }

        return node;
    }

    private static ElevatorNode BuildElevator(MetaroomDefinition definition, MetaroomObjectDefinition obj, Vector3 position)
    {
        ElevatorDefinition elevator = obj.Elevator ?? new ElevatorDefinition
        {
            YLow = obj.Position.Y,
            YHigh = obj.Position.Y + 3,
        };

        return new ElevatorNode
        {
            Position = new Vector3(position.X, 0, position.Z),
            YLow = definition.WorldY + elevator.YLow,
            YHigh = definition.WorldY + elevator.YHigh,
            CaptureRadius = elevator.CaptureRadius,
            TravelSpeed = elevator.TravelSpeed,
            StartFloor = elevator.StartFloor,
        };
    }

    private static HomeNode BuildHome(MetaroomObjectDefinition obj, Vector3 position)
    {
        HomeDefinition home = obj.Home ?? new HomeDefinition();
        return new HomeNode
        {
            Position = position,
            Radius = home.Radius,
            LonelinessSuppress = home.LonelinessSuppress,
            BoredomSuppress = home.BoredomSuppress,
        };
    }

    private static FoodNode BuildFood(MetaroomObjectDefinition obj, Vector3 position)
    {
        FoodDefinition food = obj.Food ?? new FoodDefinition();
        return new FoodNode
        {
            Position = position,
            FoodKind = ParseEnum(food.FoodKind, FoodKind.Fruit),
            GlycogenAmount = food.GlycogenAmount,
            ATPAmount = food.ATPAmount,
        };
    }

    private static GadgetNode BuildGadget(
        MetaroomObjectDefinition obj,
        Vector3 position,
        GadgetNode.GadgetType fallback)
    {
        GadgetDefinition gadget = obj.Gadget ?? new GadgetDefinition();
        return new GadgetNode
        {
            Position = position,
            Type = ParseEnum(gadget.GadgetType, fallback),
            ScanRadius = gadget.ScanRadius,
            TimerInterval = gadget.TimerInterval,
        };
    }

    private static IncubatorNode BuildIncubator(Vector3 position)
        => new() { Position = position };

    private static Node3D? BuildNorn(Vector3 position)
    {
        var scene = ResourceLoader.Load<PackedScene>("res://scenes/Norn.tscn");
        if (scene == null)
            return null;

        var node = scene.Instantiate<Node3D>();
        node.Position = position;
        return node;
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : fallback;

    private static Texture2D? LoadTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string local = ToLocalPath(path);
        if (File.Exists(local))
        {
            Image image = Image.LoadFromFile(local);
            if (image != null)
                return ImageTexture.CreateFromImage(image);
        }

        return ResourceLoader.Exists(path)
            ? ResourceLoader.Load<Texture2D>(path)
            : null;
    }

    private static string ToLocalPath(string path)
        => path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
           || path.StartsWith("user://", StringComparison.OrdinalIgnoreCase)
            ? ProjectSettings.GlobalizePath(path)
            : path;
}
