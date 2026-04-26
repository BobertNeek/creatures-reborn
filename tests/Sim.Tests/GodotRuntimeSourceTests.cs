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
    public void NornRuntime_DefaultsToLegacyGlbWithProceduralFallbackAndActionPoses()
    {
        string spriteSource = File.ReadAllText(RepoPath("src", "Godot", "NornBillboardSprite.cs"));

        Assert.Contains("UseProceduralModel = false", spriteSource);
        Assert.Contains("LoadLegacyGlbModel", spriteSource);
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

    [Fact]
    public void PointerAgent_PreservesGrabOffsetWhileCarryingCreatures()
    {
        string pointerSource = File.ReadAllText(RepoPath("src", "Godot", "PointerAgent.cs"));

        Assert.Contains("_carriedCreatureOffset", pointerSource);
        Assert.Contains("creature.Position - Position", pointerSource);
        Assert.DoesNotContain("_carriedCreature.Position = Position + new Vector3(0, 1.2f, 0)", pointerSource);
    }

    [Fact]
    public void NornRuntime_ProjectsWalkStepsOntoWalkableTreehouseSurfaces()
    {
        string spriteSource = File.ReadAllText(RepoPath("src", "Godot", "NornBillboardSprite.cs"));
        string creatureSource = File.ReadAllText(RepoPath("src", "Godot", "CreatureNode.cs"));
        string worldSource = File.ReadAllText(RepoPath("src", "Godot", "WorldNode.cs"));

        Assert.Contains("SetWalkSurface", spriteSource);
        Assert.Contains("_walkSurface(parent.Position, newX)", spriteSource);
        Assert.Contains("SetWalkSurface", creatureSource);
        Assert.Contains("ProjectWalkStep", worldSource);
    }

    [Fact]
    public void StairsNode_OnlyCapturesCreaturesNearRampSurface()
    {
        string stairsSource = File.ReadAllText(RepoPath("src", "Godot", "StairsNode.cs"));

        Assert.Contains("MathF.Abs(cn.Position.Y - expectedY)", stairsSource);
        Assert.DoesNotContain("if (cn.Position.Y < expectedY - BelowTol) continue;", stairsSource);
    }
}
