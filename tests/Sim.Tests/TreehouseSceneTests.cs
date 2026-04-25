using System;
using System.IO;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class TreehouseSceneTests
{
    private static readonly string TreehouseScenePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "scenes", "Treehouse.tscn");

    [Fact]
    public void TreehouseScene_DoesNotCommitFloorDebugBarsEnabled()
    {
        string scene = File.ReadAllText(Path.GetFullPath(TreehouseScenePath));

        Assert.Contains("res://src/Godot/FloorPlateNode.cs", scene);
        Assert.DoesNotContain("ShowDebug = true", scene);
    }
}
