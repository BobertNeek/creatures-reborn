using System;
using System.IO;
using Godot;
using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Godot.Agents;

/// <summary>
/// Heatpan Incubator — port of DS's heatpan incubator.
/// Warms eggs (emits CA heat) and hatches creatures when activated.
///
/// Click to inject a new egg. The incubator warms it over time,
/// and when ready, a new creature hatches from it.
///
/// In DS, the incubator detects nearby eggs and warms them.
/// Here we simplify: clicking spawns a new norn from a random
/// genome, with a warming animation before hatch.
/// </summary>
[GlobalClass]
public partial class IncubatorNode : Node3D
{
    [Export] public float WarmUpTime = 8.0f;   // seconds to warm the egg
    [Export] public string DefaultGenomePath = "res://data/genomes/starter.gen";

    public AgentArchetype AgentArchetype => AgentCatalog.Incubator;
    public AgentClassifier Classifier => AgentArchetype.Classifier;
    public int ObjectCategory => AgentArchetype.ObjectCategory;

    // ── State ───────────────────────────────────────────────────────────────
    private bool  _hasEgg;
    private float _warmTimer;
    private bool  _ready;

    // ── Visuals ─────────────────────────────────────────────────────────────
    private MeshInstance3D? _baseMesh;
    private MeshInstance3D? _eggMesh;
    private OmniLight3D?    _heatGlow;
    private float           _pulsePhase;

    public override void _Ready()
    {
        BuildVisual();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _pulsePhase += dt;

        // Heat glow pulse
        if (_heatGlow != null)
        {
            float glow = _hasEgg ? 0.5f + MathF.Sin(_pulsePhase * 2f) * 0.3f : 0.15f;
            _heatGlow.LightEnergy = glow;
        }

        // Warming
        if (_hasEgg && !_ready)
        {
            _warmTimer += dt;

            // Egg color transition: cold blue → warm orange
            if (_eggMesh?.Mesh is SphereMesh sm && sm.Material is StandardMaterial3D mat)
            {
                float t = Math.Min(_warmTimer / WarmUpTime, 1.0f);
                mat.AlbedoColor = new Color(
                    0.6f + t * 0.35f,
                    0.7f + t * 0.1f,
                    0.9f - t * 0.5f
                );
                mat.Emission = new Color(t * 0.8f, t * 0.4f, 0.1f);
                mat.EmissionEnergyMultiplier = t * 2.0f;
            }

            // Egg wobble as it gets ready to hatch
            if (_eggMesh != null)
            {
                float wobble = (_warmTimer / WarmUpTime) * MathF.Sin(_pulsePhase * 8f) * 0.15f;
                _eggMesh.Rotation = new Vector3(wobble, 0, wobble * 0.5f);
            }

            if (_warmTimer >= WarmUpTime)
            {
                _ready = true;
                Hatch();
            }
        }
    }

    /// <summary>Inject an egg into the incubator.</summary>
    public void InjectEgg()
    {
        if (_hasEgg) return;

        _hasEgg    = true;
        _warmTimer = 0;
        _ready     = false;

        if (_eggMesh != null) _eggMesh.Visible = true;
        GD.Print("[Incubator] Egg injected, warming...");
    }

    /// <summary>Called on click — injects egg if empty, hatches if ready.</summary>
    public void Activate()
    {
        if (!_hasEgg)
            InjectEgg();
        else if (_ready)
            Hatch();
    }

    private void Hatch()
    {
        if (!_hasEgg) return;

        // Spawn creature
        var nornScene = GD.Load<PackedScene>("res://scenes/Norn.tscn");
        if (nornScene != null)
        {
            var norn = (CreatureNode)nornScene.Instantiate();
            norn.GenomePath = DefaultGenomePath;
            norn.Position = Position + new Vector3(0.5f, 0, 0);
            GetParent()?.AddChild(norn);
            GD.Print("[Incubator] Creature hatched!");
        }

        // Clear egg
        _hasEgg = false;
        _ready  = false;
        if (_eggMesh != null) _eggMesh.Visible = false;
    }

    // ── Visuals ─────────────────────────────────────────────────────────────
    private void BuildVisual()
    {
        // Base pan
        _baseMesh = new MeshInstance3D();
        var pan = new CylinderMesh
        {
            TopRadius = 0.35f, BottomRadius = 0.30f,
            Height = 0.15f, RadialSegments = 16,
        };
        pan.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.50f, 0.45f, 0.40f),
            Metallic = 0.7f,
            Roughness = 0.3f,
        };
        _baseMesh.Mesh = pan;
        _baseMesh.Position = new Vector3(0, 0.075f, 0);
        AddChild(_baseMesh);

        // Inner basin (darker)
        var basin = new MeshInstance3D();
        var basinMesh = new CylinderMesh
        {
            TopRadius = 0.28f, BottomRadius = 0.25f,
            Height = 0.12f, RadialSegments = 16,
        };
        basinMesh.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.30f, 0.28f),
            Metallic = 0.5f,
            Roughness = 0.4f,
        };
        basin.Mesh = basinMesh;
        basin.Position = new Vector3(0, 0.10f, 0);
        AddChild(basin);

        // Egg (hidden until injected)
        _eggMesh = new MeshInstance3D();
        var eggM = new SphereMesh
        {
            Radius = 0.10f, Height = 0.16f,
            RadialSegments = 10, Rings = 6,
        };
        eggM.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.88f, 0.92f),
            Roughness = 0.4f,
        };
        _eggMesh.Mesh = eggM;
        _eggMesh.Position = new Vector3(0, 0.20f, 0);
        _eggMesh.Visible = false;
        AddChild(_eggMesh);

        // Heat glow
        _heatGlow = new OmniLight3D
        {
            LightColor  = new Color(1.0f, 0.6f, 0.2f),
            LightEnergy = 0.15f,
            OmniRange   = 2.0f,
        };
        _heatGlow.Position = new Vector3(0, 0.15f, 0);
        AddChild(_heatGlow);

        Sprite3D? sprite = AgentSpriteFactory.Create(AgentArchetype, 0.95f);
        if (sprite != null)
            AddChild(sprite);
    }
}
