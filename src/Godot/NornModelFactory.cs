using Godot;

namespace CreaturesReborn.Godot;

internal static class NornModelFactory
{
    public static Node3D Create()
    {
        var root = new Node3D { Name = "ProceduralNornModel" };

        AddPart(root, "Body4",
            new SphereMesh { Radius = 0.34f, Height = 0.56f, RadialSegments = 24, Rings = 12 },
            new Vector3(0, 0.60f, 0),
            new Vector3(0.94f, 0.96f, 0.72f),
            new Color(0.82f, 0.55f, 0.24f));

        AddPart(root, "Head1_normal",
            new SphereMesh { Radius = 0.31f, Height = 0.38f, RadialSegments = 24, Rings = 12 },
            new Vector3(0.02f, 1.02f, 0.04f),
            new Vector3(1.12f, 0.94f, 0.90f),
            new Color(0.90f, 0.64f, 0.34f));

        AddPart(root, "Bald Patch",
            new SphereMesh { Radius = 0.18f, Height = 0.08f, RadialSegments = 16, Rings = 6 },
            new Vector3(0.02f, 1.22f, 0.03f),
            new Vector3(0.80f, 0.20f, 0.68f),
            new Color(0.96f, 0.79f, 0.58f));

        AddPart(root, "ear_4L_chichi",
            new SphereMesh { Radius = 0.16f, Height = 0.24f, RadialSegments = 16, Rings = 8 },
            new Vector3(-0.27f, 1.05f, 0.02f),
            new Vector3(0.58f, 1.08f, 0.30f),
            new Color(0.85f, 0.55f, 0.26f));

        AddPart(root, "ear_4R_chichi",
            new SphereMesh { Radius = 0.16f, Height = 0.24f, RadialSegments = 16, Rings = 8 },
            new Vector3(0.29f, 1.05f, 0.02f),
            new Vector3(0.58f, 1.08f, 0.30f),
            new Color(0.85f, 0.55f, 0.26f));

        AddPart(root, "Eye_L",
            new SphereMesh { Radius = 0.045f, Height = 0.05f, RadialSegments = 12, Rings = 6 },
            new Vector3(-0.10f, 1.05f, -0.25f),
            Vector3.One,
            new Color(0.18f, 0.86f, 0.86f));

        AddPart(root, "Eye_R",
            new SphereMesh { Radius = 0.045f, Height = 0.05f, RadialSegments = 12, Rings = 6 },
            new Vector3(0.14f, 1.05f, -0.25f),
            Vector3.One,
            new Color(0.18f, 0.86f, 0.86f));

        AddPart(root, "Lid_L",
            new SphereMesh { Radius = 0.052f, Height = 0.03f, RadialSegments = 12, Rings = 4 },
            new Vector3(-0.10f, 1.075f, -0.245f),
            new Vector3(1.0f, 0.30f, 0.32f),
            new Color(0.89f, 0.64f, 0.34f));

        AddPart(root, "Lid_R",
            new SphereMesh { Radius = 0.052f, Height = 0.03f, RadialSegments = 12, Rings = 4 },
            new Vector3(0.14f, 1.075f, -0.245f),
            new Vector3(1.0f, 0.30f, 0.32f),
            new Color(0.89f, 0.64f, 0.34f));

        AddPart(root, "Hair_m",
            new SphereMesh { Radius = 0.18f, Height = 0.18f, RadialSegments = 16, Rings = 8 },
            new Vector3(0.01f, 1.27f, -0.01f),
            new Vector3(0.72f, 0.44f, 0.62f),
            new Color(0.18f, 0.16f, 0.10f));

        AddPart(root, "Hair_m_civet",
            new SphereMesh { Radius = 0.14f, Height = 0.18f, RadialSegments = 16, Rings = 8 },
            new Vector3(0.01f, 1.19f, 0.09f),
            new Vector3(0.52f, 0.76f, 0.42f),
            new Color(0.15f, 0.18f, 0.12f));

        AddLimb(root, "Thigh_L", new Vector3(-0.16f, 0.38f, 0), 0.085f, 0.26f, new Color(0.75f, 0.46f, 0.20f));
        AddLimb(root, "Thigh_R", new Vector3(0.16f, 0.38f, 0), 0.085f, 0.26f, new Color(0.75f, 0.46f, 0.20f));
        AddLimb(root, "Shin_L", new Vector3(-0.16f, 0.19f, 0), 0.070f, 0.23f, new Color(0.78f, 0.50f, 0.24f));
        AddLimb(root, "Shin_R", new Vector3(0.16f, 0.19f, 0), 0.070f, 0.23f, new Color(0.78f, 0.50f, 0.24f));

        AddPart(root, "Foot_4L",
            new SphereMesh { Radius = 0.10f, Height = 0.08f, RadialSegments = 14, Rings = 6 },
            new Vector3(-0.17f, 0.05f, -0.05f),
            new Vector3(1.45f, 0.55f, 0.72f),
            new Color(0.92f, 0.72f, 0.48f));

        AddPart(root, "Foot_4R",
            new SphereMesh { Radius = 0.10f, Height = 0.08f, RadialSegments = 14, Rings = 6 },
            new Vector3(0.17f, 0.05f, -0.05f),
            new Vector3(1.45f, 0.55f, 0.72f),
            new Color(0.92f, 0.72f, 0.48f));

        AddLimb(root, "Humerous_L", new Vector3(-0.34f, 0.67f, -0.01f), 0.060f, 0.24f, new Color(0.78f, 0.49f, 0.22f));
        AddLimb(root, "Humerous_R", new Vector3(0.36f, 0.67f, -0.01f), 0.060f, 0.24f, new Color(0.78f, 0.49f, 0.22f));
        AddLimb(root, "radius_L", new Vector3(-0.41f, 0.48f, -0.02f), 0.050f, 0.22f, new Color(0.84f, 0.55f, 0.26f));
        AddLimb(root, "radius_R", new Vector3(0.43f, 0.48f, -0.02f), 0.050f, 0.22f, new Color(0.84f, 0.55f, 0.26f));

        MeshInstance3D tail = AddLimb(root, "tail", new Vector3(0, 0.50f, 0.30f), 0.070f, 0.30f, new Color(0.75f, 0.45f, 0.20f));
        tail.RotationDegrees = new Vector3(68, 0, 0);
        MeshInstance3D tailTip = AddPart(root, "tailtip_f",
            new SphereMesh { Radius = 0.085f, Height = 0.12f, RadialSegments = 14, Rings = 6 },
            new Vector3(0, 0.34f, 0.49f),
            new Vector3(0.85f, 1.20f, 0.85f),
            new Color(0.20f, 0.38f, 0.34f));
        tailTip.RotationDegrees = new Vector3(68, 0, 0);

        return root;
    }

    private static MeshInstance3D AddLimb(Node3D parent, string name, Vector3 position, float radius, float length, Color color)
    {
        var mesh = new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = radius * 1.08f,
            Height = length,
            RadialSegments = 12,
        };

        return AddPart(parent, name, mesh, position, Vector3.One, color);
    }

    private static MeshInstance3D AddPart(
        Node3D parent,
        string name,
        Mesh mesh,
        Vector3 position,
        Vector3 scale,
        Color color)
    {
        var part = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = position,
            Scale = scale,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Roughness = 0.82f,
                MetallicSpecular = 0.06f,
            },
        };
        parent.AddChild(part);
        return part;
    }
}
