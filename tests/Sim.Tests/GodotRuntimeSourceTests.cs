using System;
using System.IO;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class GodotRuntimeSourceTests
{
    private static string RepoPath(params string[] parts)
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            Path.Combine(parts)));

    [Fact]
    public void NornRuntime_DefaultsToProceduralTrue3DModelAndActionPoses()
    {
        string spriteSource = File.ReadAllText(RepoPath("src", "Godot", "NornBillboardSprite.cs"));

        Assert.Contains("UseProceduralModel = true", spriteSource);
        Assert.Contains("NornModelFactory.Create()", spriteSource);
        Assert.Contains("SetActionPose", spriteSource);
        Assert.Contains("NornActionPose", spriteSource);
    }

    [Fact]
    public void CreatureNode_DelegatesContextDriveAndPoseWorkToFocusedHelpers()
    {
        string creatureNode = File.ReadAllText(RepoPath("src", "Godot", "CreatureNode.cs"));

        Assert.Contains("CreatureContextDrives.Apply", creatureNode);
        Assert.Contains("SetActionPose", creatureNode);
        Assert.True(File.Exists(RepoPath("src", "Godot", "CreatureContextDrives.cs")));
        Assert.True(File.Exists(RepoPath("src", "Godot", "NornModelFactory.cs")));
    }
}
