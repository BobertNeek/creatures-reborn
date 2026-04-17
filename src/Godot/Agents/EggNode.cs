using System;
using Godot;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Godot.Agents;

/// <summary>
/// A laid egg sitting in the world.
///
/// Spawned by CreatureNode.LayEggWith when two norns mate. Holds a path to
/// the freshly-crossed child genome on disk (written to user://). Sits for
/// HatchTime seconds, animating cold-blue → warm-orange + wobble, then
/// instantiates a Norn.tscn at its position with that genome path. The egg
/// node then frees itself.
///
/// This is the agent that LayEgg actually produces — distinct from
/// IncubatorNode (which is a heatpan device) and from the adult creature
/// itself. Without it, "breeding" just spawned full-grown norns instantly.
/// </summary>
[GlobalClass]
public partial class EggNode : Node3D
{
    [Export] public float  HatchTime  = 12.0f;     // seconds before hatch
    [Export] public string GenomePath = "";        // path on disk; if blank uses starter

    private float _age;
    private bool  _hatched;
    private float _pulsePhase;

    private MeshInstance3D? _shellMesh;
    private OmniLight3D?    _glow;

    public override void _Ready()
    {
        BuildVisual();
    }

    public override void _Process(double delta)
    {
        if (_hatched) return;

        float dt = (float)delta;
        _age        += dt;
        _pulsePhase += dt;

        float t = Math.Min(_age / HatchTime, 1.0f);

        // Cold blue → warm orange shell
        if (_shellMesh?.Mesh is SphereMesh sm && sm.Material is StandardMaterial3D mat)
        {
            mat.AlbedoColor = new Color(
                0.55f + t * 0.40f,
                0.65f + t * 0.15f,
                0.90f - t * 0.55f);
            mat.Emission = new Color(t * 0.80f, t * 0.40f, 0.10f);
            mat.EmissionEnergyMultiplier = t * 1.6f;
        }

        // Wobble harder as hatch nears
        if (_shellMesh != null)
        {
            float wobble = t * MathF.Sin(_pulsePhase * 9f) * 0.18f;
            _shellMesh.Rotation = new Vector3(wobble, 0, wobble * 0.5f);
        }

        // Glow follows warmth
        if (_glow != null)
            _glow.LightEnergy = 0.15f + t * 0.7f;

        if (_age >= HatchTime) Hatch();
    }

    private void Hatch()
    {
        _hatched = true;
        var nornScene = GD.Load<PackedScene>("res://scenes/Norn.tscn");
        if (nornScene != null && GetParent() != null)
        {
            var norn = (CreatureNode)nornScene.Instantiate();
            if (!string.IsNullOrEmpty(GenomePath))
                norn.GenomePath = GenomePath;
            // Spawn at egg position, slight nudge so it doesn't intersect siblings
            norn.Position = Position + new Vector3(0.3f, 0, 0);
            GetParent()!.AddChild(norn);
            GD.Print($"[Egg] Hatched! Genome={GenomePath}");
        }
        QueueFree();
    }

    private void BuildVisual()
    {
        _shellMesh = new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = 0.12f, Height = 0.20f,
                RadialSegments = 12, Rings = 7,
            },
            Position = new Vector3(0, 0.12f, 0),
        };
        if (_shellMesh.Mesh is SphereMesh sm)
        {
            sm.Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.55f, 0.65f, 0.90f),
                Roughness   = 0.4f,
                Emission    = new Color(0, 0, 0),
            };
        }
        AddChild(_shellMesh);

        _glow = new OmniLight3D
        {
            LightColor  = new Color(1.0f, 0.7f, 0.4f),
            LightEnergy = 0.15f,
            OmniRange   = 1.5f,
            Position    = new Vector3(0, 0.15f, 0),
        };
        AddChild(_glow);
    }
}
