using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.Creature;
using Godot;

namespace CreaturesReborn.Godot;

internal static class NornAppearanceApplier
{
    private enum TintRole
    {
        Fur,
        FurSkin,
        MarkedFur,
        Hair,
        Eye,
    }

    private static readonly Dictionary<string, (string texture, TintRole role)> TextureMap = new()
    {
        { "Body4",           ("Body_F.png", TintRole.FurSkin) },
        { "Head1_normal",    ("Head_F.png", TintRole.FurSkin) },
        { "Bald Patch",      ("Head_F.png", TintRole.FurSkin) },
        { "ear_4L_chichi",   ("Ear_F.png", TintRole.Fur) },
        { "ear_4R_chichi",   ("Ear_F.png", TintRole.Fur) },
        { "Thigh_L",         ("Thigh_F.png", TintRole.Fur) },
        { "Thigh_R",         ("Thigh_F.png", TintRole.Fur) },
        { "Shin_L",          ("Shin_F.png", TintRole.Fur) },
        { "Shin_R",          ("Shin_F.png", TintRole.Fur) },
        { "Foot_4L",         ("Feet_F.png", TintRole.FurSkin) },
        { "Foot_4R",         ("Feet_F.png", TintRole.FurSkin) },
        { "Humerous_L",      ("Humerus_F.png", TintRole.Fur) },
        { "Humerous_R",      ("Humerus_F.png", TintRole.Fur) },
        { "radius_L",        ("Radius_F.png", TintRole.Fur) },
        { "radius_R",        ("Radius_F.png", TintRole.Fur) },
        { "tail",            ("Tail_Base_F.png", TintRole.MarkedFur) },
        { "tailtip_f",       ("Tail_Tip_F.png", TintRole.MarkedFur) },
        { "Lid_L",           ("Head_F.png", TintRole.FurSkin) },
        { "Lid_R",           ("Head_F.png", TintRole.FurSkin) },
        { "Hair_m",          ("Hair.png", TintRole.Hair) },
        { "Hair_m_civet",    ("Hair.png", TintRole.Hair) },
    };

    private static readonly Dictionary<string, Func<CreatureAppearance, Vector3>> ScaleMap = new()
    {
        { "Body4",         a => new Vector3(a.BodyWidthScale, a.BodyHeightScale, a.BodyWidthScale) },
        { "Head1_normal",  a => Vector3.One * a.HeadScale },
        { "Bald Patch",    a => Vector3.One * a.HeadScale },
        { "Lid_L",         a => Vector3.One * a.HeadScale },
        { "Lid_R",         a => Vector3.One * a.HeadScale },
        { "Eye_L",         a => Vector3.One * (a.HeadScale * 1.02f) },
        { "Eye_R",         a => Vector3.One * (a.HeadScale * 1.02f) },
        { "ear_4L_chichi", a => Vector3.One * a.EarScale },
        { "ear_4R_chichi", a => Vector3.One * a.EarScale },
        { "Thigh_L",       a => Vector3.One * a.LimbScale },
        { "Thigh_R",       a => Vector3.One * a.LimbScale },
        { "Shin_L",        a => Vector3.One * a.LimbScale },
        { "Shin_R",        a => Vector3.One * a.LimbScale },
        { "Foot_4L",       a => Vector3.One * a.LimbScale },
        { "Foot_4R",       a => Vector3.One * a.LimbScale },
        { "Humerous_L",    a => Vector3.One * a.LimbScale },
        { "Humerous_R",    a => Vector3.One * a.LimbScale },
        { "radius_L",      a => Vector3.One * a.LimbScale },
        { "radius_R",      a => Vector3.One * a.LimbScale },
        { "tail",          a => Vector3.One * a.TailScale },
        { "tailtip_f",     a => Vector3.One * a.TailScale },
        { "Hair_m",        a => Vector3.One * a.HairScale },
        { "Hair_m_civet",  a => Vector3.One * a.HairScale },
    };

    public static Dictionary<string, Vector3> CaptureBaseScales(Node3D model)
    {
        var scales = new Dictionary<string, Vector3>();
        foreach (string name in ScaleMap.Keys)
        {
            if (model.FindChild(name, true, false) is Node3D node)
                scales[name] = node.Scale;
        }

        return scales;
    }

    public static void Apply(
        Node3D model,
        CreatureAppearance appearance,
        IReadOnlyDictionary<string, Vector3> baseScales,
        Func<string, Texture2D?> textureLoader)
    {
        ApplyMaterials(model, appearance, textureLoader);
        ApplyScales(model, appearance, baseScales);
    }

    private static void ApplyMaterials(
        Node3D model,
        CreatureAppearance appearance,
        Func<string, Texture2D?> textureLoader)
    {
        foreach (var (nodeName, entry) in TextureMap)
        {
            if (model.FindChild(nodeName, true, false) is not MeshInstance3D mesh)
                continue;

            Texture2D? tex = textureLoader(entry.texture);
            if (tex == null)
                continue;

            mesh.MaterialOverride = BuildMaterial(tex, ResolveTint(appearance, entry.role));
        }

        Texture2D? eyeTex = textureLoader("Eye.png");
        if (eyeTex == null)
            return;

        foreach (string name in new[] { "Eye_L", "Eye_R" })
        {
            if (model.FindChild(name, true, false) is MeshInstance3D eye)
                eye.MaterialOverride = BuildEyeMaterial(eyeTex, ToColor(appearance.EyeColor));
        }
    }

    private static void ApplyScales(
        Node3D model,
        CreatureAppearance appearance,
        IReadOnlyDictionary<string, Vector3> baseScales)
    {
        foreach (var (nodeName, scaleForAppearance) in ScaleMap)
        {
            if (model.FindChild(nodeName, true, false) is not Node3D node)
                continue;

            Vector3 baseScale = baseScales.TryGetValue(nodeName, out Vector3 s)
                ? s
                : Vector3.One;
            Vector3 multiplier = scaleForAppearance(appearance);
            node.Scale = new Vector3(
                baseScale.X * multiplier.X,
                baseScale.Y * multiplier.Y,
                baseScale.Z * multiplier.Z);
        }
    }

    private static StandardMaterial3D BuildMaterial(Texture2D tex, Color tint)
        => new()
        {
            AlbedoTexture = tex,
            AlbedoColor = tint,
            Roughness = 0.82f,
            MetallicSpecular = 0.08f,
        };

    private static StandardMaterial3D BuildEyeMaterial(Texture2D tex, Color tint)
        => new()
        {
            AlbedoTexture = tex,
            AlbedoColor = tint,
            Roughness = 0.04f,
            MetallicSpecular = 0.45f,
            EmissionEnabled = true,
            Emission = tint,
            EmissionEnergyMultiplier = 0.08f,
        };

    private static Color ResolveTint(CreatureAppearance appearance, TintRole role)
    {
        AppearanceColor color = role switch
        {
            TintRole.FurSkin => AppearanceColor.Lerp(appearance.FurColor, appearance.SkinColor, 0.22f),
            TintRole.MarkedFur => AppearanceColor.Lerp(
                appearance.FurColor,
                appearance.MarkingColor,
                appearance.MarkingStrength * 0.28f),
            TintRole.Hair => appearance.HairColor,
            TintRole.Eye => appearance.EyeColor,
            _ => appearance.FurColor,
        };

        float strength = role == TintRole.Hair ? 0.52f : 0.34f;
        return ToMaterialTint(color, strength);
    }

    private static Color ToMaterialTint(AppearanceColor color, float strength)
        => new(
            1.0f + (color.R - 1.0f) * strength,
            1.0f + (color.G - 1.0f) * strength,
            1.0f + (color.B - 1.0f) * strength,
            1.0f);

    private static Color ToColor(AppearanceColor color)
        => new(color.R, color.G, color.B, 1.0f);
}
