using System;
using System.Collections.Generic;
using Godot;
using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Godot;

[GlobalClass]
public partial class DoorNode : Node3D
{
    [Export] public string MetaroomId = "";
    [Export] public string DoorId = "";
    [Export] public string TargetMetaroomId = "";
    [Export] public string TargetDoorId = "";
    [Export] public Vector3 TargetWorldPosition = Vector3.Zero;
    [Export] public float CaptureRadius = 0.8f;
    [Export] public bool Enabled = true;
    [Export] public float CooldownSeconds = 1.0f;

    public AgentArchetype AgentArchetype => AgentCatalog.Door;
    public AgentClassifier Classifier => AgentArchetype.Classifier;
    public int ObjectCategory => AgentArchetype.ObjectCategory;

    private static readonly Dictionary<ulong, double> CreatureCooldowns = new();
    private MeshInstance3D? _marker;

    public override void _Ready()
    {
        BuildVisual();
    }

    public override void _Process(double delta)
    {
        if (!Enabled || TargetWorldPosition == Vector3.Zero)
            return;

        Node? parent = GetParent();
        if (parent == null)
            return;

        foreach (Node node in parent.GetChildren())
        {
            if (node is not CreatureNode creature)
                continue;
            if (!CanTeleport(creature))
                continue;

            TeleportCreature(creature);
        }
    }

    public void TeleportCreature(CreatureNode creature)
    {
        ulong id = creature.GetInstanceId();
        double now = Time.GetTicksMsec() / 1000.0;
        CreatureCooldowns[id] = now + CooldownSeconds;

        creature.Position = new Vector3(
            TargetWorldPosition.X,
            TargetWorldPosition.Y,
            creature.Position.Z);
        MoveCameraAndPointerFocus();
        Flash();
        GD.Print($"[DoorNode] {MetaroomId}:{DoorId} sent creature to {TargetMetaroomId}:{TargetDoorId}.");
    }

    private bool CanTeleport(CreatureNode creature)
    {
        ulong id = creature.GetInstanceId();
        double now = Time.GetTicksMsec() / 1000.0;
        if (CreatureCooldowns.TryGetValue(id, out double until) && until > now)
            return false;

        float dx = creature.Position.X - Position.X;
        float dy = creature.Position.Y - Position.Y;
        return dx * dx + dy * dy <= CaptureRadius * CaptureRadius;
    }

    private void MoveCameraAndPointerFocus()
    {
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera != null)
        {
            Vector3 pos = camera.GlobalPosition;
            camera.GlobalPosition = new Vector3(TargetWorldPosition.X, pos.Y, pos.Z);
        }

        Node? parent = GetParent();
        if (parent == null)
            return;

        foreach (Node node in parent.GetChildren())
        {
            if (node is PointerAgent hand)
                hand.Position = new Vector3(TargetWorldPosition.X, TargetWorldPosition.Y, hand.Position.Z);
        }
    }

    private void BuildVisual()
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.95f, 1.0f, 0.55f),
            Emission = new Color(0.25f, 0.8f, 1.0f),
            EmissionEnergyMultiplier = 0.8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        _marker = new MeshInstance3D
        {
            Name = "DoorMarker",
            Mesh = new TorusMesh
            {
                InnerRadius = CaptureRadius * 0.35f,
                OuterRadius = CaptureRadius * 0.45f,
                Rings = 18,
                RingSegments = 8,
            },
            MaterialOverride = mat,
            RotationDegrees = new Vector3(90, 0, 0),
            Position = new Vector3(0, 0.02f, 0),
        };
        AddChild(_marker);

        AddChild(new OmniLight3D
        {
            Name = "DoorGlow",
            LightColor = new Color(0.45f, 0.9f, 1.0f),
            LightEnergy = 0.55f,
            OmniRange = MathF.Max(1.0f, CaptureRadius * 2.0f),
            Position = new Vector3(0, 0.5f, 0),
        });
    }

    private void Flash()
    {
        if (_marker?.MaterialOverride is not StandardMaterial3D mat)
            return;

        mat.EmissionEnergyMultiplier = 1.6f;
        SceneTreeTimer timer = GetTree().CreateTimer(0.25);
        timer.Timeout += () =>
        {
            if (mat != null)
                mat.EmissionEnergyMultiplier = 0.8f;
        };
    }
}
