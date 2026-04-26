using System;
using System.IO;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class GodotMetaroomRuntimeSourceTests
{
    [Fact]
    public void RuntimeLoader_LoadsMetaroomDefinitionsIntoWorldMap()
    {
        string source = File.ReadAllText(RepoPath("src", "Godot", "MetaroomRuntimeLoader.cs"));

        Assert.Contains("MetaroomDefinitionJson.Load", source);
        Assert.Contains("MetaroomWorldBuilder.Build", source);
        Assert.Contains("BuildBackground", source);
        Assert.Contains("BuildEditableObject", source);
        Assert.Contains("DoorNode", source);
    }

    [Fact]
    public void DoorNode_TeleportsCreaturesAndMovesCameraFocus()
    {
        string source = File.ReadAllText(RepoPath("src", "Godot", "DoorNode.cs"));

        Assert.Contains("TargetWorldPosition", source);
        Assert.Contains("CreatureNode", source);
        Assert.Contains("CaptureRadius", source);
        Assert.Contains("TeleportCreature", source);
        Assert.Contains("Camera3D", source);
        Assert.Contains("PointerAgent", source);
    }

    [Fact]
    public void WorldNode_PrefersMetaroomRuntimeLoaderWhenPresent()
    {
        string source = File.ReadAllText(RepoPath("src", "Godot", "WorldNode.cs"));

        Assert.Contains("MetaroomRuntimeLoader", source);
        Assert.Contains("LoadIntoWorld", source);
    }

    [Fact]
    public void WorldNode_UsesLoadedMapBoundsForJsonMetarooms()
    {
        string source = File.ReadAllText(RepoPath("src", "Godot", "WorldNode.cs"));

        Assert.Contains("World.Map.AllRooms.Count", source);
        Assert.Contains("Min(room => room.XLeft)", source);
        Assert.Contains("Max(room => room.XRight)", source);
    }

    private static string RepoPath(params string[] parts)
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            Path.Combine(parts)));
}
