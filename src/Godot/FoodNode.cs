using System;
using Godot;

namespace CreaturesReborn.Godot;

/// <summary>
/// Edible food agent — rendered as a glowing fruit with a leaf on top.
/// Provides glycogen and ATP when consumed by a <see cref="CreatureNode"/>.
/// </summary>
[GlobalClass]
public partial class FoodNode : Node3D
{
    [Export] public float GlycogenAmount = 0.3f;
    [Export] public float ATPAmount      = 0.2f;

    public bool IsConsumed { get; private set; }
    public bool IsHeld     { get; private set; }

    private Node3D? _visual;      // parent of fruit + leaf + glow light

    public override void _Ready()
    {
        _visual = BuildVisual();
        AddChild(_visual);
    }

    public override void _Process(double delta)
    {
        if (!IsHeld && !IsConsumed && _visual != null)
        {
            float t = (float)Time.GetTicksMsec() * 0.001f;
            float bob = MathF.Sin(t * 1.8f + Position.X) * 0.06f;
            float spin = t * 0.5f;
            _visual.Position = new Vector3(0, bob, 0);
            _visual.Rotation = new Vector3(0, spin, 0);
        }
    }

    // ── Interaction API ───────────────────────────────────────────────────────
    public void PickUp(Node3D holder)
    {
        IsHeld = true;
        if (_visual != null)
        {
            _visual.Position = Vector3.Zero;
            _visual.Rotation = Vector3.Zero;
        }
    }

    public void Drop(Vector3 worldPos)
    {
        IsHeld   = false;
        Position = worldPos + new Vector3(0.4f, 0, 0);
        if (_visual != null) _visual.Position = Vector3.Zero;
    }

    public void Consume()
    {
        if (IsConsumed) return;
        IsConsumed = true;
        QueueFree();
    }

    // ── Visual construction ───────────────────────────────────────────────────
    private static Node3D BuildVisual()
    {
        var root = new Node3D();

        // ── Fruit sphere (apple-like) ────────────────────────────────────────
        var fruit = new MeshInstance3D();
        var fMesh = new SphereMesh { Radius = 0.17f, Height = 0.30f, RadialSegments = 12, Rings = 6 };
        var fMat  = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.20f, 0.12f),    // red apple
            MetallicSpecular = 0.4f,
            Roughness = 0.35f,
        };
        fMesh.Material = fMat;
        fruit.Mesh     = fMesh;
        fruit.Position = new Vector3(0, 0.17f, 0);
        root.AddChild(fruit);

        // ── Stem ────────────────────────────────────────────────────────────
        var stem  = new MeshInstance3D();
        var sMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 0.12f };
        var sMat  = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.20f, 0.08f) };
        sMesh.Material = sMat;
        stem.Mesh     = sMesh;
        stem.Position = new Vector3(0, 0.40f, 0);
        root.AddChild(stem);

        // ── Leaf (flat quad, slightly angled) ────────────────────────────────
        var leaf  = new MeshInstance3D();
        var lMesh = new QuadMesh { Size = new Vector2(0.16f, 0.10f) };
        var lMat  = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.62f, 0.14f),
            CullMode    = BaseMaterial3D.CullModeEnum.Disabled,
        };
        lMesh.Material = lMat;
        leaf.Mesh     = lMesh;
        leaf.Position = new Vector3(0.08f, 0.45f, 0);
        leaf.Rotation = new Vector3(-0.3f, 0.4f, 0.6f);
        root.AddChild(leaf);

        // ── Omni glow ────────────────────────────────────────────────────────
        var glow = new OmniLight3D
        {
            LightColor  = new Color(1.0f, 0.6f, 0.2f),
            LightEnergy = 0.35f,
            OmniRange   = 1.2f,
        };
        glow.Position = new Vector3(0, 0.18f, 0);
        root.AddChild(glow);

        return root;
    }
}
