using System;
using Godot;

namespace CreaturesReborn.Godot;

/// <summary>
/// One piecewise walkable region of the treehouse metaroom. The painted
/// backdrop has many small decks at many different heights (library floor,
/// observatory platform, reading nook, hammock alcove, alchemy lab, pond
/// edges, spider cave ledge…) — and several of them are slanted, not flat.
/// Each painted deck becomes one FloorPlateNode instance with its own
/// (XLeft, XRight, YLeft, YRight).
///
/// A flat floor: set YLeft == YRight (or just set legacy <c>Y</c>).
/// A slanted floor: set YLeft != YRight. The segment linearly interpolates.
///
/// Visual: off by default so the painting is the floor. Set <c>ShowDebug
/// = true</c> in the inspector to draw a thin coloured bar along the
/// slant — useful when placing new plates, then flip back off.
///
/// Semantics: at the moment, creatures don't query these explicitly;
/// stairs (StairsNode) still carry norns between floor Y values, and
/// norns walk horizontally at their current Y. FloorPlateNode documents
/// the walkable topology and is the future hook for collision / drop
/// targets / room assignment.
/// </summary>
[GlobalClass]
public partial class FloorPlateNode : Node3D
{
    [Export] public float XLeft     = -5.0f;
    [Export] public float XRight    =  5.0f;

    // Slanted-floor support: YLeft is the y at XLeft, YRight the y at XRight.
    // For a flat floor leave them equal; the legacy <c>Y</c> export sets both.
    [Export] public float YLeft     =  3.0f;
    [Export] public float YRight    =  3.0f;

    /// <summary>
    /// Legacy flat-floor convenience. Setting this makes both endpoints
    /// equal, so existing scenes authored before YLeft/YRight still work.
    /// </summary>
    [Export] public float Y
    {
        get => (YLeft + YRight) * 0.5f;
        set { YLeft = value; YRight = value; }
    }

    [Export] public bool  ShowDebug = false;
    [Export] public Color DebugTint = new Color(1.0f, 0.5f, 0.2f, 0.5f);

    public float Width    => XRight - XLeft;
    public float CenterX  => (XLeft + XRight) * 0.5f;
    public float CenterY  => (YLeft + YRight) * 0.5f;

    /// <summary>Interpolated Y at an arbitrary X within [XLeft, XRight].</summary>
    public float GetYAt(float x)
    {
        if (XRight <= XLeft) return YLeft;
        float t = Math.Clamp((x - XLeft) / (XRight - XLeft), 0f, 1f);
        return Mathf.Lerp(YLeft, YRight, t);
    }

    public override void _Ready()
    {
        if (!ShowDebug) return;

        // Slant → length + rotation. A BoxMesh is axis-aligned, so to draw
        // a slanted line we compute the segment length and rotate the box
        // around Z by the segment angle.
        float dx = XRight - XLeft;
        float dy = YRight - YLeft;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        float angle  = MathF.Atan2(dy, dx);

        var bar = new MeshInstance3D
        {
            Name = "DebugBar",
            Mesh = new BoxMesh { Size = new Vector3(length, 0.08f, 0.4f) },
            Position = new Vector3(CenterX, CenterY - 0.04f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor  = DebugTint,
                Transparency = DebugTint.A < 1f
                    ? BaseMaterial3D.TransparencyEnum.Alpha
                    : BaseMaterial3D.TransparencyEnum.Disabled,
                ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        bar.RotationDegrees = new Vector3(0, 0, Mathf.RadToDeg(angle));
        AddChild(bar);
    }
}
