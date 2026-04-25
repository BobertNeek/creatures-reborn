using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

    [Fact]
    public void TreehouseScene_AssignsFruitSeedAndFoodKinds()
    {
        string scene = File.ReadAllText(Path.GetFullPath(TreehouseScenePath));
        HashSet<int> foodKinds = Regex.Matches(scene, @"FoodKind = (\d+)")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToHashSet();

        Assert.Contains(0, foodKinds);
        Assert.Contains(1, foodKinds);
        Assert.Contains(2, foodKinds);
    }

    [Fact]
    public void TreehouseScene_StartsWithMaleAndFemaleNorns()
    {
        string scene = File.ReadAllText(Path.GetFullPath(TreehouseScenePath));
        HashSet<int> sexes = Regex.Matches(scene, @"Sex = (\d+)")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToHashSet();

        Assert.Contains(1, sexes);
        Assert.Contains(2, sexes);
    }
}
