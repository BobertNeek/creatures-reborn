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
    [Export] public float DwellTime     = 4.0f;   // seconds to wait at each floor
    [Export] public float TravelSpeed   = 3.0f;   // world units per second
    [Export] public int   StartFloor    = 2;      // 2 = bottom

    // Internal state
    private int   _currentFloor;
    private int   _targetFloor;
    private int   _direction = -1;   // -1 = going up (lower index), +1 = going down
    private float _dwell;
    private float _currentY;

    // Visuals
    private MeshInstance3D? _shaftMesh;
    private MeshInstance3D? _platformMesh;
    private OmniLight3D?    _light;

    public override void _Ready()
    {
        _currentFloor = _targetFloor = Mathf.Clamp(StartFloor, 0, 2);
        _currentY     = TreehouseMetaroomNode.GetFloorY(_currentFloor);
        _dwell        = DwellTime;

        BuildVisual();
        UpdatePlatformPosition();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float targetY = TreehouseMetaroomNode.GetFloorY(_targetFloor);

        // Move toward target
        if (!Mathf.IsEqualApprox(_currentY, targetY))
        {
            _currentY = Mathf.MoveToward(_currentY, targetY, TravelSpeed * dt);
            UpdatePlatformPosition();
        }
        else
        {
            // Dwell at floor, then advance
            _currentFloor = _targetFloor;
            _dwell -= dt;
            if (_dwell <= 0)
            {
                AdvanceFloor();
                _dwell = DwellTime;
            }
        }

        // Carry any creatures within capture radius
        CarryCreatures(dt);

        // Pulse the light
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
        _dwell       = DwellTime;
    }

    private void AdvanceFloor()
    {
        int next = _currentFloor + _direction;
        if (next < 0) { next = 1; _direction = +1; }
        if (next > 2) { next = 1; _direction = -1; }
        _targetFloor = next;
    }

    // ── Carry creatures ──────────────────────────────────────────────────────
    private void CarryCreatures(float dt)
    {
        var parent = GetParent();
        if (parent == null) return;

        foreach (Node n in parent.GetChildren())
        {
            if (n is not CreatureNode cn) continue;
            float dx = MathF.Abs(cn.Position.X - Position.X);
            if (dx > CaptureRadius) continue;

            // Snap creature X to lift X (so they don't drift off), lerp Y to platform
            float newY = Mathf.MoveToward(cn.Position.Y, _currentY, TravelSpeed * dt);
            cn.Position = new Vector3(Position.X, newY, cn.Position.Z);
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
