using System;
using Godot;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Godot;

/// <summary>
/// A "home smell" anchor — the norn equivalent of a Norn Home / fireplace.
///
/// Each physics tick it scans for <see cref="CreatureNode"/> siblings and:
///   • Suppresses Loneliness and Boredom when a creature is within <see cref="Radius"/>.
///   • The effect fades linearly to zero at the edge of the radius.
///
/// Visually rendered as a soft glowing disc on the floor.
/// </summary>
[GlobalClass]
public partial class HomeNode : Node3D
{
    [Export] public float Radius        = 3.0f;
    [Export] public float LonelinessSuppress = 0.25f;   // max suppression per tick
    [Export] public float BoredomSuppress    = 0.15f;

    public override void _Ready()
    {
        AddChild(BuildDisc());
        AddChild(BuildGlow());
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GetParent() == null) return;

        foreach (Node n in GetParent().GetChildren())
        {
            if (n is not CreatureNode cn || cn.Creature == null) continue;

            float dist      = Position.DistanceTo(cn.Position);
            if (dist >= Radius) continue;

            float proximity = 1.0f - dist / Radius;   // 1.0 at centre, 0.0 at edge

            // Negative AddDriveInput suppresses that drive this tick
            cn.Creature.AddDriveInput(DriveId.Loneliness, -proximity * LonelinessSuppress);
            cn.Creature.AddDriveInput(DriveId.Boredom,    -proximity * BoredomSuppress);
        }
    }

    // -------------------------------------------------------------------------
    private MeshInstance3D BuildDisc()
    {
        var m   = new MeshInstance3D();
        var cyl = new CylinderMesh();
        cyl.TopRadius    = Radius;
        cyl.BottomRadius = Radius;
        cyl.Height       = 0.01f;
        var mat = new StandardMaterial3D();
        mat.AlbedoColor  = new Color(1.0f, 0.92f, 0.5f, 0.25f);
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded;
        cyl.Material = mat;
        m.Mesh      = cyl;
        m.Position  = new Vector3(0, 0.005f, 0);
        return m;
    }

    private OmniLight3D BuildGlow()
    {
        var light = new OmniLight3D();
        light.LightColor   = new Color(1.0f, 0.9f, 0.5f);
        light.LightEnergy  = 0.6f;
        light.OmniRange    = Radius * 1.5f;
        light.Position     = new Vector3(0, 0.3f, 0);
        return light;
    }
}
