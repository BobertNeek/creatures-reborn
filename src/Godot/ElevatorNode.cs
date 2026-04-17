using System;
using Godot;

namespace CreaturesReborn.Godot;

/// <summary>
/// A vertical elevator/lift that carries creatures between the Treehouse's
/// three floors along a tree-trunk shaft.
///
/// Behaviour:
///   - Sits at a fixed X. Draws a translucent column spanning the full
///     floor-to-ceiling height so it's visible in the scene.
///   - Maintains a "current floor" index 0/1/2 (top/mid/bottom).
///   - Any CreatureNode within CaptureRadius horizontally gets its Y lerped
///     toward the elevator's current floor Y. So stepping onto the pad
///     rides the lift automatically.
///   - Every DwellTime seconds (default 4s) the lift advances to the next
///     floor, cycling 0 → 1 → 2 → 1 → 0. So a norn can hop on and get
///     carried up or down without player input.
///   - Can be clicked/activated by the pointer to jump straight to a chosen
///     floor (call GoToFloor(i)).
/// </summary>
[GlobalClass]
public partial class ElevatorNode : Node3D
{
    [Export] public float CaptureRadius = 0.9f;   // horizontal capture zone
    [Export] public float TravelSpeed   = 3.0f;   // world units per second
    [Export] public int   StartFloor    = 2;      // 2 = bottom

    // Internal state — call-driven, not auto-cycling. Stays at its last
    // commanded floor until the player clicks it again via PointerAgent.
    private int   _currentFloor;
    private int   _targetFloor;
    private float _currentY;

    // Visuals
    private MeshInstance3D? _shaftMesh;
    private MeshInstance3D? _platformMesh;
    private OmniLight3D?    _light;

    public override void _Ready()
    {
        _currentFloor = _targetFloor = Mathf.Clamp(StartFloor, 0, 2);
        _currentY     = TreehouseMetaroomNode.GetFloorY(_currentFloor);

        BuildVisual();
        UpdatePlatformPosition();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float targetY = TreehouseMetaroomNode.GetFloorY(_targetFloor);

        // Move toward target if we're not there yet
        if (!Mathf.IsEqualApprox(_currentY, targetY))
        {
            _currentY = Mathf.MoveToward(_currentY, targetY, TravelSpeed * dt);
            UpdatePlatformPosition();
        }
        else
        {
            _currentFloor = _targetFloor;
        }

        CarryCreatures(dt);

        if (_light != null)
        {
            float pulse = 0.7f + MathF.Sin((float)Time.GetTicksMsec() * 0.002f) * 0.3f;
            _light.LightEnergy = 0.8f * pulse;
        }
    }

    /// <summary>Command the lift to head to a specific floor (0=top, 1=mid, 2=bot).</summary>
    public void GoToFloor(int floor)
    {
        _targetFloor = Mathf.Clamp(floor, 0, 2);
    }

    // ── Carry creatures ──────────────────────────────────────────────────────
    // Only adjust Y, never X — clamping X to the lift trapped norns into
    // vertical totem-poles because they could never walk off the platform.
    // And only carry while the lift is actually in transit (Y still moving
    // toward target). Once it arrives, leave the rider alone so they can
    // wander off normally.
    private void CarryCreatures(float dt)
    {
        var parent = GetParent();
        if (parent == null) return;

        float targetY = TreehouseMetaroomNode.GetFloorY(_targetFloor);
        bool  inTransit = !Mathf.IsEqualApprox(_currentY, targetY);
        if (!inTransit) return;

        foreach (Node n in parent.GetChildren())
        {
            if (n is not CreatureNode cn) continue;

            // Capture only if standing on (or just above) the platform: must be
            // close in X AND already near the platform Y, so a norn on a
            // different floor isn't yanked through the floor above.
            float dx = MathF.Abs(cn.Position.X - Position.X);
            float dy = MathF.Abs(cn.Position.Y - _currentY);
            if (dx > CaptureRadius || dy > 0.6f) continue;

            float newY = Mathf.MoveToward(cn.Position.Y, _currentY, TravelSpeed * dt);
            cn.Position = new Vector3(cn.Position.X, newY, cn.Position.Z);
        }
    }

    // ── Visuals ──────────────────────────────────────────────────────────────
    private void BuildVisual()
    {
        // Shaft — thin translucent column spanning full floor range
        const float topY = TreehouseMetaroomNode.TopFloorY + 1.2f;
        const float botY = TreehouseMetaroomNode.BottomFloorY - 0.2f;
        float shaftHeight = topY - botY;

        _shaftMesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 0.35f, BottomRadius = 0.35f,
                Height = shaftHeight, RadialSegments = 10,
            },
            Position = new Vector3(0, (topY + botY) * 0.5f, -0.15f),
        };
        _shaftMesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor         = new Color(0.55f, 0.85f, 1.0f, 0.18f),
            Emission            = new Color(0.40f, 0.75f, 1.0f),
            EmissionEnergyMultiplier = 0.8f,
            Transparency        = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode         = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        AddChild(_shaftMesh);

        // Platform — a disk that moves with the lift
        _platformMesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 0.6f, BottomRadius = 0.6f,
                Height = 0.15f, RadialSegments = 14,
            },
        };
        _platformMesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.70f, 0.40f),
            Emission    = new Color(0.95f, 0.85f, 0.55f),
            EmissionEnergyMultiplier = 0.5f,
            Metallic = 0.4f, Roughness = 0.4f,
        };
        AddChild(_platformMesh);

        // Glow light
        _light = new OmniLight3D
        {
            LightColor     = new Color(0.60f, 0.85f, 1.0f),
            LightEnergy    = 0.8f,
            OmniRange      = 3.5f,
        };
        AddChild(_light);
    }

    private void UpdatePlatformPosition()
    {
        if (_platformMesh != null)
            _platformMesh.Position = new Vector3(0, _currentY, 0);
        if (_light != null)
            _light.Position = new Vector3(0, _currentY + 0.5f, 0);
    }
}
