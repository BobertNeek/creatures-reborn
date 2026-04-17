using System;
using Godot;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Godot.Agents;

/// <summary>
/// Food plant agent — a growing plant that periodically produces fruit.
/// Port of DS food pod agents (carrot pod, lemon pod, justanut pod, stinger pod).
///
/// The plant has three life stages:
///   1. Seedling — small, growing
///   2. Mature — full size, producing fruit on a timer
///   3. Withered — dies after a lifespan, drops seeds
///
/// When activated (clicked or creature push), the plant drops a fruit
/// that creatures can eat. The fruit provides nutrients based on the
/// plant's FoodType.
/// </summary>
[GlobalClass]
public partial class FoodPlantNode : Node3D
{
    public enum PlantType { Carrot, Lemon, Justanut, Stinger, AlienBerry }

    [Export] public PlantType Type         = PlantType.AlienBerry;
    [Export] public float     GrowthTime   = 30.0f;   // seconds to mature
    [Export] public float     FruitTimer   = 25.0f;   // seconds between fruit drops
    [Export] public int       MaxFruit     = 3;        // max fruit on ground nearby
    [Export] public float     Lifespan     = 300.0f;   // seconds before withering (0 = immortal)

    // ── State ───────────────────────────────────────────────────────────────
    private float _age;
    private float _fruitCooldown;
    private int   _fruitCount;
    private bool  _mature;
    private bool  _withered;

    // ── Visuals ─────────────────────────────────────────────────────────────
    private Node3D? _stemNode;
    private Node3D? _canopyNode;
    private float   _growthScale;

    public override void _Ready()
    {
        _fruitCooldown = FruitTimer * 0.5f;  // first fruit comes sooner
        BuildVisual();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;

        if (_withered) return;

        // Growth
        if (!_mature)
        {
            _growthScale = Math.Min(_age / GrowthTime, 1.0f);
            if (_growthScale >= 1.0f) _mature = true;
            UpdateScale();
        }

        // Lifespan
        if (Lifespan > 0 && _age > Lifespan)
        {
            Wither();
            return;
        }

        // Fruit production
        if (_mature)
        {
            _fruitCooldown -= dt;
            if (_fruitCooldown <= 0 && CountNearbyFruit() < MaxFruit)
            {
                DropFruit();
                _fruitCooldown = FruitTimer;
            }
        }

        // Gentle sway animation
        if (_canopyNode != null)
        {
            float sway = MathF.Sin(_age * 1.2f + Position.X * 2f) * 0.05f;
            _canopyNode.Rotation = new Vector3(sway, 0, sway * 0.7f);
        }
    }

    // ── Activation (click or creature push) ─────────────────────────────────
    public void Activate()
    {
        if (!_mature || _withered) return;
        if (CountNearbyFruit() < MaxFruit + 1)
        {
            DropFruit();
            _fruitCooldown = FruitTimer;  // reset timer
        }
    }

    // ── Fruit spawning ──────────────────────────────────────────────────────
    private void DropFruit()
    {
        var (glyc, atp, col) = GetFruitProps();

        var food = new FoodNode
        {
            GlycogenAmount = glyc,
            ATPAmount      = atp,
        };

        float dropX = Position.X + (float)(new Random().NextDouble() - 0.5) * 1.5f;
        food.Position = new Vector3(dropX, 0.18f, Position.Z);

        GetParent()?.AddChild(food);
        _fruitCount++;
    }

    private (float glyc, float atp, Color col) GetFruitProps() => Type switch
    {
        PlantType.Carrot     => (0.35f, 0.15f, new Color(1.0f, 0.55f, 0.15f)),
        PlantType.Lemon      => (0.25f, 0.25f, new Color(1.0f, 0.95f, 0.30f)),
        PlantType.Justanut   => (0.40f, 0.10f, new Color(0.65f, 0.45f, 0.25f)),
        PlantType.Stinger    => (0.20f, 0.30f, new Color(0.20f, 0.80f, 0.35f)),
        PlantType.AlienBerry => (0.30f, 0.20f, new Color(0.55f, 0.25f, 0.85f)),
        _ => (0.25f, 0.20f, new Color(1.0f, 0.3f, 0.3f)),
    };

    private int CountNearbyFruit()
    {
        int count = 0;
        if (GetParent() == null) return 0;
        foreach (Node n in GetParent().GetChildren())
        {
            if (n is FoodNode food && !food.IsConsumed)
            {
                if (food.Position.DistanceTo(Position) < 4.0f)
                    count++;
            }
        }
        return count;
    }

    // ── Wither ──────────────────────────────────────────────────────────────
    private void Wither()
    {
        _withered = true;
        // Shrink and brown out
        if (_canopyNode != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(_canopyNode, "scale", Vector3.One * 0.2f, 3.0f);
        }
        // TODO: drop seeds for regrowth
    }

    // ── Visuals ─────────────────────────────────────────────────────────────
    private void BuildVisual()
    {
        var (_, _, fruitCol) = GetFruitProps();

        // Stem
        _stemNode = new Node3D();
        var stem = new MeshInstance3D();
        var sm = new CylinderMesh
        {
            TopRadius = 0.02f, BottomRadius = 0.035f,
            Height = 0.6f, RadialSegments = 6,
        };
        var stemCol = Type == PlantType.AlienBerry
            ? new Color(0.12f, 0.48f, 0.30f)
            : new Color(0.20f, 0.55f, 0.15f);
        sm.Material = new StandardMaterial3D { AlbedoColor = stemCol, Roughness = 0.9f };
        stem.Mesh = sm;
        stem.Position = new Vector3(0, 0.3f, 0);
        _stemNode.AddChild(stem);
        AddChild(_stemNode);

        // Canopy / pod
        _canopyNode = new Node3D();
        _canopyNode.Position = new Vector3(0, 0.6f, 0);

        // Main pod body
        var pod = new MeshInstance3D();
        var podMesh = new SphereMesh
        {
            Radius = 0.18f, Height = 0.28f, RadialSegments = 10, Rings = 6,
        };

        Color podCol = Type switch
        {
            PlantType.Carrot     => new Color(0.25f, 0.65f, 0.20f),
            PlantType.Lemon      => new Color(0.30f, 0.60f, 0.18f),
            PlantType.Justanut   => new Color(0.20f, 0.50f, 0.15f),
            PlantType.Stinger    => new Color(0.15f, 0.55f, 0.25f),
            PlantType.AlienBerry => new Color(0.18f, 0.50f, 0.40f),
            _ => new Color(0.25f, 0.55f, 0.20f),
        };

        var podMat = new StandardMaterial3D { AlbedoColor = podCol, Roughness = 0.7f };
        if (Type == PlantType.AlienBerry)
        {
            podMat.Emission = new Color(0.1f, 0.3f, 0.25f);
            podMat.EmissionEnergyMultiplier = 0.5f;
        }
        podMesh.Material = podMat;
        pod.Mesh = podMesh;
        _canopyNode.AddChild(pod);

        // Leaves
        for (int i = 0; i < 4; i++)
        {
            var leaf = new MeshInstance3D();
            var lq = new QuadMesh { Size = new Vector2(0.15f, 0.10f) };
            var leafCol = new Color(stemCol.R * 0.9f, stemCol.G * 1.1f, stemCol.B * 0.8f);
            lq.Material = new StandardMaterial3D
            {
                AlbedoColor = leafCol,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                Roughness = 0.8f,
            };
            leaf.Mesh = lq;
            float angle = i * MathF.PI / 2f;
            leaf.Position = new Vector3(MathF.Cos(angle) * 0.15f, -0.05f, MathF.Sin(angle) * 0.15f);
            leaf.Rotation = new Vector3(-0.4f, angle, 0.3f);
            _canopyNode.AddChild(leaf);
        }

        AddChild(_canopyNode);
        _growthScale = 0.1f;
        UpdateScale();
    }

    private void UpdateScale()
    {
        float s = Math.Max(_growthScale, 0.1f);
        if (_stemNode != null) _stemNode.Scale = new Vector3(s, s, s);
        if (_canopyNode != null)
        {
            _canopyNode.Scale = new Vector3(s, s, s);
            _canopyNode.Position = new Vector3(0, 0.6f * s, 0);
        }
    }
}
