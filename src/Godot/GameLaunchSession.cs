using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using CreaturesReborn.Sim.Save;

namespace CreaturesReborn.Godot;

public static class GameLaunchSession
{
    public static readonly string[] BundledMetaroomPaths =
    {
        "res://data/metarooms/treehouse.json",
        "res://data/metarooms/forest.json",
    };

    public static IReadOnlyList<string> ActiveMetaroomPaths { get; private set; } = Array.Empty<string>();
    public static GameSaveData? PendingSaveData { get; private set; }
    public static string? PendingSavePath { get; private set; }

    public static void StartBundledWorld(SceneTree tree)
    {
        PendingSaveData = null;
        PendingSavePath = null;
        ActiveMetaroomPaths = BundledMetaroomPaths;
        MetaroomEditorSession.SetDefinitionPaths(BundledMetaroomPaths);
        tree.ChangeSceneToFile("res://scenes/MetaroomWorld.tscn");
    }

    public static void StartCustomMetaroom(SceneTree tree, string path)
    {
        PendingSaveData = null;
        PendingSavePath = null;
        ActiveMetaroomPaths = new[] { path };
        MetaroomEditorSession.SetDefinitionPaths(path);
        tree.ChangeSceneToFile("res://scenes/MetaroomWorld.tscn");
    }

    public static void StartSavedGame(SceneTree tree, string savePath)
    {
        var service = new GameSaveService(ProjectSettings.GlobalizePath("user://saves"));
        PendingSaveData = service.Load(savePath);
        PendingSavePath = savePath;
        ActiveMetaroomPaths = PendingSaveData.MetaroomPaths.Count > 0
            ? PendingSaveData.MetaroomPaths
            : BundledMetaroomPaths;
        MetaroomEditorSession.SetDefinitionPaths(ActiveMetaroomPaths.ToArray());
        tree.ChangeSceneToFile("res://scenes/MetaroomWorld.tscn");
    }

    public static void ClearPendingSave()
    {
        PendingSaveData = null;
        PendingSavePath = null;
    }

    public static void Clear()
    {
        ActiveMetaroomPaths = Array.Empty<string>();
        PendingSaveData = null;
        PendingSavePath = null;
        MetaroomEditorSession.Clear();
    }
}
