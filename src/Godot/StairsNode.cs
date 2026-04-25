using System;
using Godot;

namespace CreaturesReborn.Godot;

/// <summary>
/// A walkable staircase connecting two floor heights. Unlike ElevatorNode
/// (which is a platform the player has to activate), stairs are passive —
/// any CreatureNode that wanders into the stair's X range is gently lerped
/// toward the ramp's surface. So a norn walking horizontally across the
/// metaroom automatically ascends/descends when it crosses a stair zone.
///
/// Why this design (and not real navmesh pathfinding): the vertical slice's
/// norn AI only decides "walk left" or "walk right" each tick. Stairs that
/// act on raw horizontal motion let existing AI reach every floor for free —
/// stair-seeking logic can come later. The StairsNode just reshapes the
/// terrain the norn walks on.
///
/// Floor connectivity: each stair covers one floor transition. Place two
/// stairs end-to-end (e.g. bot→mid abutting mid→top on the same side of
/// the room) so a single continuous walk carries a norn across two floors.
///
/// Convention: the stair's surface is defined by (XLeft, YLeft) → (XRight,
/// YRight). Either endpoint can be higher — it just flips the slope.
/// </summary>
[GlobalClass]
public partial class StairsNode : Node3D
{
    [Export] public float XLeft  = -10.0f;
    [Export] public float XRight = -7.0f;
    [Export] public float YLeft  = 5.0f;   // y at XLeft
    [Export] public float YRight = 2.0f;   // y at XRight
    [Export] public int   StepCount = 8;   // visual step resolution
    [Export] public float ClimbSpeed = 4.0f; // world-units / sec a norn tracks the ramp at
    [Export] public bool  ShowVisual = false; // off by default — painted backdrop shows the descent
    [Export] public bool  Enabled = true;

    public override void _Ready()
    {
        if (Enabled && ShowVisual) BuildVisual();
    }

    public override void _Process(double delta)
    {
        if (!Enabled) return;
        float dt = (float)delta;
        var parent = GetParent();
        if (parent == null) return;

        float lo  = XLeft < XRight ? XLeft  : XRight;
        float hi  = XLeft < XRight ? XRight : XLeft;

        foreach (Node n in parent.GetChildren())
        {
            if (n is not CreatureNode cn) continue;
            float cx = cn.Position.X;
            if (cx < lo || cx > hi) continue;

            // Linear interpolation along the ramp — using the originally
            // defined endpoints so direction (up-left vs up-right) is
            // preserved regardless of which X is numerically greater.
            float t = (cx - XLeft) / (XRight - XLeft);
            float expectedY = Mathf.Lerp(YLeft, YRight, Mathf.Clamp(t, 0f, 1f));

            // Capture rule: "standing on or falling onto the ramp".
            //   • If the norn is BELOW the ramp surface by more than a small
            //     tolerance, they're on a lower floor — ignore.
            //   • If they're at or above the ramp, pull them onto it. This
            //     means a norn walking along a higher floor and crossing
            //     the stair's X range smoothly descends onto it (instead of
            //     walking off into empty space).
            // Using an "above-only" rule prevents norns on a lower floor
            // that shares X range with the stair from being yanked upward.
            const float BelowTol = 0.3f;
            if (cn.Position.Y < expectedY - BelowTol) continue;

            float newY = Mathf.MoveToward(cn.Position.Y, expectedY, ClimbSpeed * dt);
            cn.Position = new Vector3(cn.Position.X, newY, cn.Position.Z);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Visual: row of small wooden step blocks laid along the ramp. Purposefully
    // chunky so the player can see the path at a glance against the backdrop.
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildVisual()
    {
        if (StepCount < 1) StepCount = 1;
        float xSpan = XRight - XLeft;
        float stepW = MathF.Abs(xSpan) / StepCount;

        var mat = new StandardMaterial3D
        {
            AlbedoColor  = new Color(0.55f, 0.38f, 0.22f),
            Emission     = new Color(0.20f, 0.12f, 0.05f),
            EmissionEnergyMultiplier = 0.25f,
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        for (int i = 0; i < StepCount; i++)
        {
            float t = (i + 0.5f) / StepCount;
            float xCenter = Mathf.Lerp(XLeft, XRight, t);
            float yCenter = Mathf.Lerp(YLeft, YRight, t);

            var slab = new MeshInstance3D
            {
                Name = $"Step{i}",
                Mesh = new BoxMesh { Size = new Vector3(stepW * 1.05f, 0.18f, 1.25f) },
                // Sit the slab's top surface exactly at yCenter so norn feet land on it.
                Position = new Vector3(xCenter, yCenter - 0.09f, 0f),
                MaterialOverride = mat,
            };
            AddChild(slab);
        }

        // End caps — small brighter nubs at the top & bottom of the run so
        // the stair's floor-entry points read clearly.
        AddChild(BuildCap(XLeft,  YLeft));
        AddChild(BuildCap(XRight, YRight));
    }

    private static MeshInstance3D BuildCap(float x, float y)
    {
        return new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 0.22f, BottomRadius = 0.22f,
                Height = 0.08f, RadialSegments = 10,
            },
            Position = new Vector3(x, y + 0.04f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.85f, 0.70f, 0.40f),
                Emission    = new Color(0.80f, 0.55f, 0.25f),
                EmissionEnergyMultiplier = 0.5f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
    }
}
