using System;
using System.IO;
using CreaturesReborn.Sim.World;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class MetaroomEditorSceneTests
{
    [Fact]
    public void Project_StartsAtMainMenuScene()
    {
        string project = File.ReadAllText(RepoPath("project.godot"));

        Assert.Contains("run/main_scene=\"res://scenes/MainMenu.tscn\"", project);
    }

    [Fact]
    public void MainMenuScene_OffersPlayEditorLoadAndQuit()
    {
        string scene = File.ReadAllText(RepoPath("scenes", "MainMenu.tscn"));
        string source = File.ReadAllText(RepoPath("src", "Godot", "UI", "MainMenu.cs"));

        Assert.Contains("res://src/Godot/UI/MainMenu.cs", scene);
        Assert.Contains("Play Treehouse", source);
        Assert.Contains("Metaroom Editor", source);
        Assert.Contains("Load Metaroom", source);
        Assert.Contains("Quit", source);
        Assert.Contains("ChangeSceneToFile", source);
    }

    [Fact]
    public void MetaroomEditorScene_ExposesLineToolsObjectPaletteAndPersistence()
    {
        string scene = File.ReadAllText(RepoPath("scenes", "MetaroomEditor.tscn"));
        string source = File.ReadAllText(RepoPath("src", "Godot", "Editor", "MetaroomEditorNode.cs"));

        Assert.Contains("res://src/Godot/Editor/MetaroomEditorNode.cs", scene);
        Assert.Contains("FileDialog", source);
        Assert.Contains("Line2D", source);
        Assert.Contains("BeginPath", source);
        Assert.Contains("AddObject", source);
        Assert.Contains("MetaroomObjectKind.Door", source);
        Assert.Contains("MetaroomDefinitionJson.Save", source);
        Assert.Contains("MetaroomDefinitionJson.Load", source);
        Assert.Contains("TestPlay", source);
    }

    [Fact]
    public void MetaroomEditorScene_ExposesDoorLinkInspectorAndValidation()
    {
        string source = File.ReadAllText(RepoPath("src", "Godot", "Editor", "MetaroomEditorNode.cs"));

        Assert.Contains("Target Metaroom", source);
        Assert.Contains("Target Door", source);
        Assert.Contains("Bidirectional", source);
        Assert.Contains("ValidateDoorLink", source);
        Assert.Contains("UpdateSelectedDoorTargetMetaroom", source);
        Assert.Contains("UpdateSelectedDoorTargetDoor", source);
    }

    [Fact]
    public void TreehouseMetaroomJson_LoadsAndReferencesRuntimeBackground()
    {
        string path = RepoPath("data", "metarooms", "treehouse.json");

        MetaroomDefinition definition = MetaroomDefinitionJson.Load(path);

        Assert.Equal("treehouse", definition.Id);
        Assert.Equal("res://art/metaroom/metaroom-right-connector-v2.png", definition.BackgroundPath);
        Assert.True(definition.Paths.Count >= 8);
        Assert.Contains(definition.Objects, obj => obj.Kind == MetaroomObjectKind.Door);
        Assert.Contains(definition.Objects, obj => obj.Kind == MetaroomObjectKind.FoodSpawn);
        Assert.Contains(definition.Objects, obj => obj.Kind == MetaroomObjectKind.NornSpawn);
    }

    private static string RepoPath(params string[] parts)
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            Path.Combine(parts)));
}
