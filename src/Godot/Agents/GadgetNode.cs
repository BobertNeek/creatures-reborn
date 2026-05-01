using System;
using Godot;
using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Godot.Agents;

/// <summary>
/// Generic gadget node — base for various DS gadgets:
///   - Empathic Vendor: listens for hungry creatures, dispenses food
///   - Holistic Learning Machine: teaches creatures vocabulary
///   - Training Dummy: creatures practice hitting, reduces boredom
///   - Robot Toy: creatures play with it, reduces boredom
///   - Musicola: plays music, mild entertainment
///
/// Each gadget type has different behaviour on activation and timer.
/// </summary>
[GlobalClass]
public partial class GadgetNode : Node3D
{
    public enum GadgetType
    {
        EmpathicVendor,
        LearningMachine,
        TrainingDummy,
        RobotToy,
        Musicola,
    }

    [Export] public GadgetType Type = GadgetType.EmpathicVendor;
    [Export] public float ScanRadius = 6.0f;
    [Export] public float TimerInterval = 10.0f;

    public AgentArchetype AgentArchetype => Type switch
    {
        GadgetType.EmpathicVendor => AgentCatalog.EmpathicVendor,
        GadgetType.LearningMachine => AgentCatalog.LearningMachine,
        GadgetType.TrainingDummy => AgentCatalog.TrainingDummy,
        GadgetType.RobotToy => AgentCatalog.RobotToy,
        GadgetType.Musicola => AgentCatalog.Musicola,
        _ => AgentCatalog.Machine,
    };

    public AgentClassifier Classifier => AgentArchetype.Classifier;
    public int ObjectCategory => AgentArchetype.ObjectCategory;

    private float _timer;
    private float _animPhase;

    // Visuals
    private Node3D? _bodyNode;
    private MeshInstance3D? _indicatorMesh;

    public override void _Ready()
    {
        _timer = TimerInterval;
        BuildVisual();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _animPhase += dt;
        _timer -= dt;

        // Gadget-specific timer behaviour
        if (_timer <= 0)
        {
            _timer = TimerInterval;
            OnTimer();
        }

        // Animate indicator
        AnimateIndicator();
    }

    /// <summary>Activated by click or creature push.</summary>
    public void Activate(CreatureNode? byCreature = null)
    {
        switch (Type)
        {
            case GadgetType.EmpathicVendor:
                DispenseFood();
                if (byCreature?.Creature != null)
                    StimulusTable.Apply(byCreature.Creature, StimulusId.VendorGaveFood);
                break;

            case GadgetType.LearningMachine:
                TeachNearestCreature();
                break;

            case GadgetType.TrainingDummy:
                if (byCreature?.Creature != null)
                {
                    StimulusTable.Apply(byCreature.Creature, StimulusId.PlayedWithToy);
                    GD.Print("[TrainingDummy] Creature practiced!");
                }
                break;

            case GadgetType.RobotToy:
                if (byCreature?.Creature != null)
                {
                    StimulusTable.Apply(byCreature.Creature, StimulusId.PlayedWithToy);
                    GD.Print("[RobotToy] Creature played!");
                }
                break;

            case GadgetType.Musicola:
                GD.Print("[Musicola] Playing music...");
                // TODO: play actual music
                break;
        }
    }

    // ── Timer behaviour ─────────────────────────────────────────────────────
    private void OnTimer()
    {
        switch (Type)
        {
            case GadgetType.EmpathicVendor:
                // Scan for hungry creatures and auto-dispense
                ScanAndFeed();
                break;

            case GadgetType.LearningMachine:
                // Periodically teach nearby creatures
                TeachNearestCreature();
                break;
        }
    }

    // ── Empathic Vendor: find hungry creature and drop food ─────────────────
    private void ScanAndFeed()
    {
        if (GetParent() == null) return;

        CreatureNode? hungriest = null;
        float maxHunger = 0.3f;  // only feed if hunger > 0.3

        foreach (Node n in GetParent().GetChildren())
        {
            if (n is not CreatureNode cn || cn.Creature == null) continue;
            float dist = Position.DistanceTo(cn.Position);
            if (dist > ScanRadius) continue;

            float hunger = cn.Creature.GetDriveLevel(DriveId.HungerForCarb);
            if (hunger > maxHunger)
            {
                hungriest = cn;
                maxHunger = hunger;
            }
        }

        if (hungriest != null)
        {
            DispenseFood();
            StimulusTable.Apply(hungriest.Creature!, StimulusId.VendorGaveFood);
        }
    }

    private void DispenseFood()
    {
        var food = new FoodNode
        {
            FoodKind = FoodKind.Food,
            GlycogenAmount = 0.35f,
            ATPAmount = 0.15f,
        };
        food.Position = Position + new Vector3(0.8f, 0.18f, 0);
        GetParent()?.AddChild(food);
        GD.Print("[EmpathicVendor] Dispensed food.");
    }

    // ── Learning Machine: teach vocabulary ──────────────────────────────────
    private void TeachNearestCreature()
    {
        if (GetParent() == null) return;

        foreach (Node n in GetParent().GetChildren())
        {
            if (n is not CreatureNode cn || cn.Creature == null) continue;
            float dist = Position.DistanceTo(cn.Position);
            if (dist > ScanRadius) continue;

            // Give a small reward for being near the learning machine
            StimulusTable.Apply(cn.Creature, StimulusId.Activate1Good);
            GD.Print("[LearningMachine] Teaching creature vocabulary.");
            break;
        }
    }

    // ── Visuals ─────────────────────────────────────────────────────────────
    private void BuildVisual()
    {
        _bodyNode = new Node3D();

        switch (Type)
        {
            case GadgetType.EmpathicVendor:
                BuildVendorVisual();
                break;
            case GadgetType.LearningMachine:
                BuildMachineVisual(new Color(0.3f, 0.5f, 0.9f));
                break;
            case GadgetType.TrainingDummy:
                BuildDummyVisual();
                break;
            case GadgetType.RobotToy:
                BuildToyVisual();
                break;
            case GadgetType.Musicola:
                BuildMachineVisual(new Color(0.8f, 0.5f, 0.3f));
                break;
        }

        Sprite3D? sprite = AgentSpriteFactory.Create(AgentArchetype, 1.15f);
        if (sprite != null)
            _bodyNode.AddChild(sprite);

        AddChild(_bodyNode);
    }

    private void BuildVendorVisual()
    {
        // Main body — tall machine shape
        var body = new MeshInstance3D();
        var bm = new BoxMesh { Size = new Vector3(0.5f, 0.9f, 0.4f) };
        bm.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.50f, 0.60f),
            Metallic = 0.5f, Roughness = 0.4f,
        };
        body.Mesh = bm;
        body.Position = new Vector3(0, 0.45f, 0);
        _bodyNode!.AddChild(body);

        // Dispensing nozzle
        var nozzle = new MeshInstance3D();
        var nm = new CylinderMesh
        {
            TopRadius = 0.06f, BottomRadius = 0.08f,
            Height = 0.15f, RadialSegments = 8,
        };
        nm.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.60f, 0.55f, 0.50f),
            Metallic = 0.6f, Roughness = 0.3f,
        };
        nozzle.Mesh = nm;
        nozzle.Position = new Vector3(0.2f, 0.7f, 0);
        nozzle.RotationDegrees = new Vector3(0, 0, -30);
        _bodyNode.AddChild(nozzle);

        // Indicator light
        _indicatorMesh = new MeshInstance3D();
        var im = new SphereMesh { Radius = 0.05f, RadialSegments = 8, Rings = 4 };
        im.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.9f, 0.3f),
            Emission = new Color(0.2f, 0.9f, 0.3f),
            EmissionEnergyMultiplier = 1.5f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        _indicatorMesh.Mesh = im;
        _indicatorMesh.Position = new Vector3(0, 0.85f, 0.15f);
        _bodyNode.AddChild(_indicatorMesh);
    }

    private void BuildMachineVisual(Color accentCol)
    {
        var body = new MeshInstance3D();
        var bm = new BoxMesh { Size = new Vector3(0.6f, 0.7f, 0.35f) };
        bm.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.45f, 0.48f, 0.52f),
            Metallic = 0.4f, Roughness = 0.5f,
        };
        body.Mesh = bm;
        body.Position = new Vector3(0, 0.35f, 0);
        _bodyNode!.AddChild(body);

        // Screen/display
        var screen = new MeshInstance3D();
        var sm = new QuadMesh { Size = new Vector2(0.35f, 0.25f) };
        sm.Material = new StandardMaterial3D
        {
            AlbedoColor = accentCol,
            Emission = accentCol * 0.5f,
            EmissionEnergyMultiplier = 1.0f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        screen.Mesh = sm;
        screen.Position = new Vector3(0, 0.55f, 0.18f);
        _bodyNode.AddChild(screen);

        _indicatorMesh = screen;
    }

    private void BuildDummyVisual()
    {
        // Post
        var post = new MeshInstance3D();
        var pm = new CylinderMesh
        {
            TopRadius = 0.04f, BottomRadius = 0.05f,
            Height = 0.8f, RadialSegments = 6,
        };
        pm.Material = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.42f, 0.25f) };
        post.Mesh = pm;
        post.Position = new Vector3(0, 0.4f, 0);
        _bodyNode!.AddChild(post);

        // Target pad
        var pad = new MeshInstance3D();
        var padM = new CylinderMesh
        {
            TopRadius = 0.20f, BottomRadius = 0.20f,
            Height = 0.08f, RadialSegments = 12,
        };
        padM.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.25f, 0.20f),
            Roughness = 0.6f,
        };
        pad.Mesh = padM;
        pad.Position = new Vector3(0, 0.7f, 0.05f);
        pad.RotationDegrees = new Vector3(90, 0, 0);
        _bodyNode.AddChild(pad);
    }

    private void BuildToyVisual()
    {
        // Body — rounded robot shape
        var body = new MeshInstance3D();
        var bm = new SphereMesh { Radius = 0.18f, Height = 0.30f, RadialSegments = 10, Rings = 6 };
        bm.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.6f, 0.9f),
            Metallic = 0.4f, Roughness = 0.3f,
        };
        body.Mesh = bm;
        body.Position = new Vector3(0, 0.25f, 0);
        _bodyNode!.AddChild(body);

        // Eyes (two small spheres)
        for (int side = -1; side <= 1; side += 2)
        {
            var eye = new MeshInstance3D();
            var em = new SphereMesh { Radius = 0.04f, RadialSegments = 8, Rings = 4 };
            em.Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 1f, 0.3f),
                Emission = new Color(1f, 1f, 0.3f),
                EmissionEnergyMultiplier = 1.0f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            eye.Mesh = em;
            eye.Position = new Vector3(side * 0.07f, 0.30f, 0.14f);
            _bodyNode.AddChild(eye);
        }

        _indicatorMesh = body;
    }

    private void AnimateIndicator()
    {
        if (_indicatorMesh?.Mesh == null) return;

        // Gentle pulse for all gadgets
        float pulse = 0.95f + MathF.Sin(_animPhase * 2f) * 0.05f;
        _indicatorMesh.Scale = new Vector3(pulse, pulse, pulse);

        // Robot toy: wobble
        if (Type == GadgetType.RobotToy && _bodyNode != null)
        {
            float wobble = MathF.Sin(_animPhase * 1.5f) * 0.08f;
            _bodyNode.Rotation = new Vector3(0, _animPhase * 0.3f, wobble);
        }
    }
}
