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
        Assert.Contains("ShouldUseProceduralModel", spriteSource);
        Assert.Contains("DisplayServer.GetName()", spriteSource);
        Assert.Contains("\"headless\"", spriteSource);
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
    public void NornRuntime_UsesWorldNavigationForJsonMetaroomWalkingAndGravity()
    {
        string creatureSource = File.ReadAllText(RepoPath("src", "Godot", "CreatureNode.cs"));
        string pointerSource = File.ReadAllText(RepoPath("src", "Godot", "PointerAgent.cs"));
        string worldSource = File.ReadAllText(RepoPath("src", "Godot", "WorldNode.cs"));

        Assert.Contains("GetParent() is WorldNode worldNode && _sprite != null", creatureSource);
        Assert.DoesNotContain("worldNode && treehouse != null", creatureSource);
        Assert.Contains("ApplyGravity", creatureSource);
        Assert.Contains("ApplyGravityStep", worldSource);
        Assert.Contains("SetHeldByHand", creatureSource);
        Assert.Contains("SetHeldByHand(true)", pointerSource);
        Assert.Contains("SetHeldByHand(false)", pointerSource);
        Assert.Contains("FindWalkableSurfaceBelow", worldSource);
        Assert.Contains("WalkSurfaceStickDistance", worldSource);
    }

    [Fact]
    public void StairsNode_OnlyCapturesCreaturesNearRampSurface()
    {
        string stairsSource = File.ReadAllText(RepoPath("src", "Godot", "StairsNode.cs"));

        Assert.Contains("MathF.Abs(cn.Position.Y - expectedY)", stairsSource);
        Assert.DoesNotContain("if (cn.Position.Y < expectedY - BelowTol) continue;", stairsSource);
    }

    [Fact]
    public void DebugHud_ReadsCoreV2SnapshotsAndCatalogsWithoutOwningSimulation()
    {
        string debugHud = File.ReadAllText(RepoPath("src", "Godot", "UI", "DebugHud.cs"));

        Assert.Contains("SetWorld", debugHud);
        Assert.Contains("SetAffordanceTarget", debugHud);
        Assert.Contains("GenomeSummary.Create", debugHud);
        Assert.Contains("ChemicalCatalog.Get", debugHud);
        Assert.Contains("CreateSnapshot", debugHud);
        Assert.Contains("CreateCaSnapshot", debugHud);
        Assert.Contains("AgentAffordanceCatalog.ForAgent", debugHud);
        Assert.DoesNotContain("new GameWorld", debugHud);
    }

    [Fact]
    public void GameGui_UsesOffsetsForHorizontallyStretchedActionBar()
    {
        string gameGui = File.ReadAllText(RepoPath("src", "Godot", "UI", "GameGui.cs"));

        Assert.Contains("_actionBar.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide)", gameGui);
        Assert.Contains("_actionBar.OffsetLeft = 0", gameGui);
        Assert.Contains("_actionBar.OffsetRight = 0", gameGui);
        Assert.Contains("_actionBar.OffsetBottom = -5", gameGui);
        Assert.DoesNotContain("_actionBar.Size = new Vector2(0, 50)", gameGui);
    }

    [Fact]
    public void DebugScreenshot_UsesFallbackBeforeReadingViewportTextureInHeadlessMode()
    {
        string screenshotSource = File.ReadAllText(RepoPath("src", "Godot", "DebugScreenshot.cs"));

        Assert.Contains("DisplayServer.GetName()", screenshotSource);
        Assert.Contains("\"headless\"", screenshotSource);
        Assert.Contains("CreateFallbackImage", screenshotSource);
    }

    [Fact]
    public void LegacyNornGlbExternalTextureSources_ArePresentBesideModel()
    {
        string[] textureNames =
        {
            "norn_Body_F.png",
            "norn_Ear_F.png",
            "norn_Feet_F.png",
            "norn_Head_F.png",
            "norn_Humerus_F.png",
            "norn_Radius_F.png",
            "norn_Shin_F.png",
            "norn_Thigh_F.png",
            "norn_Tail_Base_F.png",
            "norn_Tail_Tip_F.png",
        };

        foreach (string textureName in textureNames)
            Assert.True(File.Exists(RepoPath("assets", "models", textureName)), $"{textureName} is required by norn.glb.");

        foreach (string textureName in textureNames)
        {
            string uidPath = RepoPath("assets", "models", $"{textureName}.uid");
            Assert.True(File.Exists(uidPath), $"{textureName}.uid keeps norn.glb external resource UIDs valid.");
            Assert.StartsWith("uid://", File.ReadAllText(uidPath).Trim());

            string importPath = RepoPath("assets", "models", $"{textureName}.import");
            Assert.True(File.Exists(importPath), $"{textureName}.import registers the imported texture UID with Godot.");
            Assert.Contains(File.ReadAllText(uidPath).Trim(), File.ReadAllText(importPath));
        }

        string glbImportPath = RepoPath("assets", "models", "norn.glb.import");
        Assert.True(File.Exists(glbImportPath), "norn.glb.import lets clean checkouts load norn.glb as a PackedScene.");
        Assert.Contains("type=\"PackedScene\"", File.ReadAllText(glbImportPath));
    }

    [Fact]
    public void LegacyNornGlbImportMetadata_IsExplicitlyTrackedDespiteGlobalImportIgnore()
    {
        string gitignore = File.ReadAllText(RepoPath(".gitignore"));

        Assert.Contains("*.import", gitignore);
        Assert.Contains("!assets/models/norn.glb.import", gitignore);
        string[] textureNames =
        {
            "norn_Body_F.png",
            "norn_Ear_F.png",
            "norn_Feet_F.png",
            "norn_Head_F.png",
            "norn_Humerus_F.png",
            "norn_Radius_F.png",
            "norn_Shin_F.png",
            "norn_Thigh_F.png",
            "norn_Tail_Base_F.png",
            "norn_Tail_Tip_F.png",
        };

        foreach (string textureName in textureNames)
            Assert.Contains($"!assets/models/{textureName}.import", gitignore);
    }
}
