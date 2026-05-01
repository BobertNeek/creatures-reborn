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
    public void MainMenuScene_OffersRequestedMenuFlowSettingsAndSaveSlots()
    {
        string scene = File.ReadAllText(RepoPath("scenes", "MainMenu.tscn"));
        string source = File.ReadAllText(RepoPath("src", "Godot", "UI", "MainMenu.cs"));
        string launchSource = File.ReadAllText(RepoPath("src", "Godot", "GameLaunchSession.cs"));

        Assert.Contains("res://src/Godot/UI/MainMenu.cs", scene);
        Assert.Contains("New Game", source);
        Assert.Contains("Load Game", source);
        Assert.Contains("Edit MetaRoom", source);
        Assert.Contains("Load Metaroom", source);
        Assert.Contains("Settings", source);
        Assert.Contains("Exit Game", source);
        Assert.Contains("StartBundledWorld", source);
        Assert.Contains("ShowSaveSlotBrowser", source);
        Assert.Contains("ShowSharedSettingsOverlay", source);
        Assert.Contains("res://data/metarooms/treehouse.json", launchSource);
        Assert.Contains("res://data/metarooms/forest.json", launchSource);
        Assert.DoesNotContain("Play Treehouse", source);
    }

    [Fact]
    public void MainMenuSettings_UsesGodotConfigDisplayAndAudioApis()
    {
        string settingsSource = File.ReadAllText(RepoPath("src", "Godot", "UI", "GameSettingsStore.cs"));
        string applierSource = File.ReadAllText(RepoPath("src", "Godot", "UI", "GameSettingsApplier.cs"));
        string worldSource = File.ReadAllText(RepoPath("src", "Godot", "WorldNode.cs"));
        string guiSource = File.ReadAllText(RepoPath("src", "Godot", "UI", "GameGui.cs"));
        string overlaySource = File.ReadAllText(RepoPath("src", "Godot", "UI", "SettingsOverlay.cs"));

        Assert.Contains("ConfigFile", settingsSource);
        Assert.Contains("DisplayServer.WindowSetMode", applierSource);
        Assert.Contains("DisplayServer.WindowSetVsyncMode", applierSource);
        Assert.Contains("AudioServer.SetBusVolumeLinear", applierSource);
        Assert.Contains("AudioServer.SetBusMute", applierSource);
        Assert.Contains("ApplySettings(GameSettings", worldSource);
        Assert.Contains("RestoreFromSave(GameSaveData", worldSource);
        Assert.Contains("CreateSaveData", worldSource);
        Assert.Contains("GameSaveService", guiSource);
        Assert.Contains("Save Slot", guiSource);
        Assert.Contains("\"Save\"", overlaySource);
        Assert.Contains("\"Reset\"", overlaySource);
        Assert.Contains("\"OK\"", overlaySource);
        Assert.Contains("\"Cancel\"", overlaySource);
        Assert.DoesNotContain("Save Settings", overlaySource);
        Assert.DoesNotContain("Reset Settings", overlaySource);
    }

    [Fact]
    public void InGameGui_EscapePausesAndShowsSaveLoadSettingsMenu()
    {
        string guiSource = File.ReadAllText(RepoPath("src", "Godot", "UI", "GameGui.cs"));

        Assert.Contains("ProcessModeEnum.Always", guiSource);
        Assert.Contains("ShowPauseOverlay", guiSource);
        Assert.Contains("ClosePauseOverlay", guiSource);
        Assert.Contains("GetTree().Paused = true", guiSource);
        Assert.Contains("GetTree().Paused = false", guiSource);
        Assert.Contains("\"Resume\"", guiSource);
        Assert.Contains("\"Save Game\"", guiSource);
        Assert.Contains("\"Load Game\"", guiSource);
        Assert.Contains("\"Settings\"", guiSource);
        Assert.Contains("ShowSaveOverlay", guiSource);
        Assert.Contains("ShowLoadOverlay", guiSource);
        Assert.Contains("SettingsOverlay", guiSource);
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
        Assert.Equal("Treehouse", definition.Name);
        Assert.Equal("res://art/metaroom/imported/ig_08c5f1f9f59ddea70169ed69df04348199aeebee1a267cc1df.png", definition.BackgroundPath);
        Assert.True(definition.Paths.Count >= 20);
        Assert.Contains(definition.Objects, obj => obj.Kind == MetaroomObjectKind.Incubator);
        Assert.Contains(definition.Objects, obj => obj.Kind == MetaroomObjectKind.FoodDispenser);
        Assert.Contains(definition.Objects, obj => obj.Kind == MetaroomObjectKind.Toy);
        Assert.Contains(definition.Objects, obj => obj.Kind == MetaroomObjectKind.NornSpawn);
    }

    private static string RepoPath(params string[] parts)
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            Path.Combine(parts)));
}
