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

    [Fact]
    public void TreehouseScene_UsesBackdropAlignedFloorHeights()
    {
        string scene = File.ReadAllText(Path.GetFullPath(TreehouseScenePath));

        Assert.Contains("YLeft = 11.25", scene);
        Assert.Contains("YLeft = 7.3", scene);
        Assert.Contains("YLeft = 3.35", scene);
        Assert.DoesNotContain("YLeft = 10.32", scene);
        Assert.DoesNotContain("YLeft = 5.80", scene);
        Assert.DoesNotContain("YLeft = 2.55", scene);
    }

    [Fact]
    public void TreehouseScene_UsesBackdropAlignedRampEndpoints()
    {
        string scene = File.ReadAllText(Path.GetFullPath(TreehouseScenePath));

        AssertNodeSurface(scene, "FloorBottomLeftSlant", -19.8f, -13.3f, 4.25f, 7.3f);
        AssertNodeSurface(scene, "StairBotMidToHammock", -19.8f, -13.3f, 4.25f, 7.3f);
        AssertNodeSurface(scene, "FloorPond", 14.8f, 18.8f, 3.35f, 7.3f);
        AssertNodeSurface(scene, "StairPondToAlchemyR", 14.8f, 18.8f, 3.35f, 7.3f);
        AssertNodeValue(scene, "FloorBottomLeftLow", "XRight", 14.8f);
    }

    private static void AssertNodeSurface(
        string scene,
        string nodeName,
        float xLeft,
        float xRight,
        float yLeft,
        float yRight)
    {
        AssertNodeValue(scene, nodeName, "XLeft", xLeft);
        AssertNodeValue(scene, nodeName, "XRight", xRight);
        AssertNodeValue(scene, nodeName, "YLeft", yLeft);
        AssertNodeValue(scene, nodeName, "YRight", yRight);
    }

    private static void AssertNodeValue(string scene, string nodeName, string property, float expected)
    {
        var match = Regex.Match(
            scene,
            $@"\[node name=""{Regex.Escape(nodeName)}""[^\]]*\](?<body>.*?)(?=\n\[node |\z)",
            RegexOptions.Singleline);

        Assert.True(match.Success, $"Node {nodeName} was not found.");

        var valueMatch = Regex.Match(
            match.Groups["body"].Value,
            $@"^{Regex.Escape(property)} = (?<value>-?\d+(?:\.\d+)?)\r?$",
            RegexOptions.Multiline);

        Assert.True(valueMatch.Success, $"{nodeName}.{property} was not found.");
        float actual = float.Parse(
            valueMatch.Groups["value"].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, actual, precision: 2);
    }
}
