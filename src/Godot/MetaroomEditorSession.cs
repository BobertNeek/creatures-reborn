using System;
using System.Collections.Generic;

namespace CreaturesReborn.Godot;

public static class MetaroomEditorSession
{
    private static readonly List<string> PendingDefinitionPaths = new();

    public static IReadOnlyList<string> DefinitionPaths => PendingDefinitionPaths;

    public static void SetDefinitionPaths(params string[] paths)
    {
        PendingDefinitionPaths.Clear();
        foreach (string path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path))
                PendingDefinitionPaths.Add(path);
        }
    }

    public static void Clear()
    {
        PendingDefinitionPaths.Clear();
    }
}
