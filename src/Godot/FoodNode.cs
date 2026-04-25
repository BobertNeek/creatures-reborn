using System;
using Godot;
using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Godot;

/// <summary>
/// Edible agent. C3/DS distinguishes fruit, seeds, and food; the visual and
/// stimulus mapping are selected by <see cref="FoodKind"/>.
/// </summary>
[GlobalClass]
public partial class FoodNode : Node3D
{
    [Export] public FoodKind FoodKind = FoodKind.Fruit;

    // Legacy scene/API knobs retained for spawned food. StimulusTable carries
    // the real C3/DS nutrition; ATP is a small extra energy top-up.
    [Export] public float GlycogenAmount = 0.3f;
    [Export] public float ATPAmount = -1.0f;

    public bool IsConsumed { get; private set; }
    public bool IsHeld     { get; private set; }
    public FoodNutrition Nutrition => FoodNutrition.ForKind(FoodKind);
    public int EatStimulusId => Nutrition.StimulusId;
    public float ResolvedATPAmount => ATPAmount >= 0 ? ATPAmount : Nutrition.ATPAmount;
    public AgentArchetype AgentArchetype => AgentCatalog.ForFood(FoodKind);
    public AgentClassifier Classifier => AgentArchetype.Classifier;
    public int ObjectCategory => AgentArchetype.ObjectCategory;

    private Node3D? _visual;      // parent of fruit + leaf + glow light

    public override void _Ready()
    {
        _visual = BuildVisual(FoodKind);
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
    private static Node3D BuildVisual(FoodKind kind)
    {
        return kind switch
        {
            FoodKind.Seed => BuildSeedVisual(),
            FoodKind.Food => BuildFoodVisual(),
            _ => BuildFruitVisual(),
        };
    }

    private static Node3D BuildFruitVisual()
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

    private static Node3D BuildSeedVisual()
    {
        var root = new Node3D();
        var shellMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.78f, 0.55f, 0.28f),
            Roughness = 0.55f,
        };
        var stripeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.34f, 0.22f, 0.11f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        var seed = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = shellMat,
            Scale = new Vector3(0.78f, 1.18f, 0.78f),
            Position = new Vector3(0, 0.20f, 0),
            RotationDegrees = new Vector3(0, 0, -18),
        };
        root.AddChild(seed);

        var stripe = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.035f, 0.23f, 0.025f) },
            MaterialOverride = stripeMat,
            Position = new Vector3(0.035f, 0.22f, 0.14f),
            RotationDegrees = new Vector3(0, 0, -18),
        };
        root.AddChild(stripe);

        var glow = new OmniLight3D
        {
            LightColor = new Color(0.9f, 0.62f, 0.24f),
            LightEnergy = 0.22f,
            OmniRange = 0.9f,
            Position = new Vector3(0, 0.18f, 0),
        };
        root.AddChild(glow);

        return root;
    }

    private static Node3D BuildFoodVisual()
    {
        var root = new Node3D();
        var baseMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.76f, 0.42f),
            Roughness = 0.45f,
        };
        var capMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.52f, 0.33f, 0.18f),
            Roughness = 0.5f,
        };

        var pellet = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.34f, 0.22f, 0.26f) },
            MaterialOverride = baseMat,
            Position = new Vector3(0, 0.18f, 0),
            RotationDegrees = new Vector3(0, 18, 0),
        };
        root.AddChild(pellet);

        var cap = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 0.16f,
                BottomRadius = 0.16f,
                Height = 0.06f,
                RadialSegments = 12,
            },
            MaterialOverride = capMat,
            Position = new Vector3(0, 0.33f, 0),
            RotationDegrees = new Vector3(90, 0, 0),
        };
        root.AddChild(cap);

        var glow = new OmniLight3D
        {
            LightColor = new Color(1.0f, 0.82f, 0.38f),
            LightEnergy = 0.28f,
            OmniRange = 1.1f,
            Position = new Vector3(0, 0.18f, 0),
        };
        root.AddChild(glow);

        return root;
    }
}
