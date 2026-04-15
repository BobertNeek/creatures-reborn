using System;
using Godot;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Godot;

/// <summary>
/// Procedural metaroom environment with multi-layer parallax depth.
///
/// Depth layers (Z, camera at +14):
///   −4.5  ProceduralSky + WorldEnvironment (infinite)
///   −3.5  Mountain silhouette range (jagged triangle mesh)
///   −2.8  Far forest silhouette strip
///   −2.0  Mid-ground cloud layer (static puffs)
///   −1.5  Mid-ground leafy canopy strip
///   −0.5  Ground plane (grass, subdivided)
///    0.0  Action plane — norns, food, walls
///   +0.6  Foreground plant fronds (for depth)
/// </summary>
[GlobalClass]
public partial class MetaroomNode : Node3D
{
    [Export] public float RoomWidth = 20.0f;
    [Export] public float FloorY    =  0.0f;

    public MetaroomSim Sim { get; } = new MetaroomSim();

    // Seeded RNG so the layout is deterministic
    private Random _rng = new Random(12345);

    public override void _Ready()
    {
        Sim.LeftBound  = -RoomWidth * 0.5f;
        Sim.RightBound =  RoomWidth * 0.5f;
        Sim.FloorY     = FloorY;

        SetupWorldEnvironment();
        BuildMountainRange();
        BuildFarForestStrip();
        BuildClouds();
        BuildMidCanopyStrip();
        BuildGround();
        BuildWalls();
        BuildTrees();
        BuildForegroundFronds();
        ScatterDetails();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. World environment — sky, ambient, subtle fog + glow
    // ─────────────────────────────────────────────────────────────────────────
    private void SetupWorldEnvironment()
    {
        var skyMat = new ProceduralSkyMaterial();
        skyMat.SkyTopColor        = new Color(0.18f, 0.38f, 0.75f);
        skyMat.SkyHorizonColor    = new Color(0.70f, 0.84f, 0.96f);
        skyMat.GroundHorizonColor = new Color(0.50f, 0.68f, 0.40f);
        skyMat.GroundBottomColor  = new Color(0.30f, 0.48f, 0.25f);
        skyMat.SunAngleMax        = 25.0f;

        var sky = new Sky { SkyMaterial = skyMat };

        var env = new global::Godot.Environment();
        env.BackgroundMode     = global::Godot.Environment.BGMode.Sky;
        env.Sky                = sky;
        env.AmbientLightSource = global::Godot.Environment.AmbientSource.Sky;
        env.AmbientLightEnergy = 0.65f;
        env.FogEnabled         = true;
        env.FogMode            = global::Godot.Environment.FogModeEnum.Exponential;
        env.FogDensity         = 0.006f;
        env.FogLightColor      = new Color(0.72f, 0.84f, 0.95f);
        env.FogLightEnergy     = 1.0f;
        env.GlowEnabled        = true;
        env.GlowIntensity      = 0.6f;
        env.GlowBloom          = 0.04f;
        env.GlowStrength       = 1.2f;

        var worldEnv = new WorldEnvironment { Environment = env };
        AddChild(worldEnv);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Distant mountain silhouettes — SurfaceTool triangle mesh
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildMountainRange()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        float w      = RoomWidth + 10f;
        float baseY  = FloorY - 0.3f;
        float z      = -3.5f;

        // Generate irregular peaks
        int    peaks  = 10;
        float  step   = w / peaks;
        float[] peakX = new float[peaks + 2];
        float[] peakY = new float[peaks + 2];
        peakX[0]        = -w * 0.5f; peakY[0] = baseY;
        peakX[peaks + 1] = w * 0.5f;  peakY[peaks + 1] = baseY;
        for (int i = 1; i <= peaks; i++)
        {
            peakX[i] = -w * 0.5f + (i - 0.5f) * step + Rng(-step * 0.4f, step * 0.4f);
            peakY[i] = FloorY + Rng(1.5f, 4.0f);
        }

        // Fill with triangles: each peak makes a triangle fan to the base line
        var col = new Color(0.22f, 0.30f, 0.42f);  // dark slate blue-grey
        for (int i = 0; i < peaks; i++)
        {
            // Triangle: base-left, peak, base-right
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i], baseY, z));
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i + 1], peakY[i + 1], z));
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i + 2], baseY, z));

            // Fill gap at bottom
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i], baseY, z));
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i + 1], peakY[i + 1], z));
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i + 1], baseY, z));
        }

        var mat = UnlitMat(col);
        mat.VertexColorUseAsAlbedo = true;
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, mat);

        var mi = new MeshInstance3D { Mesh = mesh };
        AddChild(mi);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Far forest silhouette strip
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildFarForestStrip()
    {
        // A bumpy quad strip suggesting treetops at the horizon
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        float w    = RoomWidth + 6f;
        float z    = -2.8f;
        float base_ = FloorY + 0.0f;
        int segs   = 30;
        float segW = w / segs;
        var col    = new Color(0.18f, 0.36f, 0.20f);

        for (int i = 0; i < segs; i++)
        {
            float x0 = -w * 0.5f + i * segW;
            float x1 = x0 + segW;
            float top0 = FloorY + Rng(0.6f, 2.0f);
            float top1 = FloorY + Rng(0.6f, 2.0f);

            // Two triangles per segment
            st.SetColor(col); st.AddVertex(new Vector3(x0, base_, z));
            st.SetColor(col); st.AddVertex(new Vector3(x0, top0,  z));
            st.SetColor(col); st.AddVertex(new Vector3(x1, top1,  z));

            st.SetColor(col); st.AddVertex(new Vector3(x0, base_, z));
            st.SetColor(col); st.AddVertex(new Vector3(x1, top1,  z));
            st.SetColor(col); st.AddVertex(new Vector3(x1, base_, z));
        }

        var mat = UnlitMat(col);
        mat.VertexColorUseAsAlbedo = true;
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, mat);
        AddChild(new MeshInstance3D { Mesh = mesh });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Cloud puffs (static, unlit, semi-transparent)
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildClouds()
    {
        float[] xs = { -7f, -2f, 4f, 8.5f, -10f };
        float[] ys = { 3.4f, 4.1f, 3.7f, 4.5f, 3.9f };

        for (int c = 0; c < xs.Length; c++)
        {
            var cloud = new Node3D();
            cloud.Position = new Vector3(xs[c], ys[c], -2.0f);

            int blobs = _rng.Next(3, 6);
            for (int b = 0; b < blobs; b++)
            {
                float r = Rng(0.3f, 0.65f);
                var s = new SphereMesh { Radius = r, Height = r * 1.4f, RadialSegments = 8, Rings = 4 };
                var mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 1f, 1f, 0.82f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode     = BaseMaterial3D.CullModeEnum.Disabled,
                };
                s.Material = mat;
                var m = new MeshInstance3D { Mesh = s };
                m.Position = new Vector3(Rng(-0.5f, 0.5f) * blobs * 0.3f, Rng(-0.15f, 0.15f), 0);
                cloud.AddChild(m);
            }

            AddChild(cloud);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Mid-ground leafy canopy strip
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildMidCanopyStrip()
    {
        // Irregular canopy using overlapping spheres
        float z     = -1.5f;
        float count = 22;
        float step  = (RoomWidth + 4) / count;
        for (int i = 0; i < count; i++)
        {
            float x = -(RoomWidth + 4) * 0.5f + i * step + Rng(-step * 0.3f, step * 0.3f);
            float r = Rng(0.4f, 0.8f);
            float y = FloorY + r * 0.5f + Rng(0, 0.4f);
            var s = new SphereMesh { Radius = r, Height = r * 1.3f, RadialSegments = 8, Rings = 4 };
            var col = new Color(Rng(0.10f, 0.18f), Rng(0.38f, 0.52f), Rng(0.12f, 0.22f));
            s.Material = UnlitMat(col);
            var m = new MeshInstance3D { Mesh = s };
            m.Position = new Vector3(x, y, z);
            m.Scale = new Vector3(1f, Rng(0.8f, 1.2f), 0.5f);
            AddChild(m);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Ground plane — subdivided for vertex-colour grass variation
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildGround()
    {
        // Main grass plane
        var plane = new MeshInstance3D();
        var pm    = new PlaneMesh
        {
            Size             = new Vector2(RoomWidth + 2f, 8f),
            SubdivideDepth   = 6,
            SubdivideWidth   = 20,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor    = new Color(0.34f, 0.62f, 0.24f),
            Roughness      = 0.95f,
            MetallicSpecular = 0.0f,
        };
        pm.Material = mat;
        plane.Mesh  = pm;
        plane.Position = new Vector3(0, FloorY, 0);
        AddChild(plane);

        // Dirt path strip down the centre (slightly lower, brownish)
        var path = new MeshInstance3D();
        var pp   = new PlaneMesh { Size = new Vector2(3.0f, 8f) };
        var pm2  = new StandardMaterial3D { AlbedoColor = new Color(0.52f, 0.40f, 0.28f), Roughness = 1f };
        pp.Material = pm2;
        path.Mesh   = pp;
        path.Position = new Vector3(0, FloorY + 0.001f, 0);
        AddChild(path);

        // Subtle grass tuft edge along path
        for (int i = 0; i < 14; i++)
        {
            float x = Rng(-1.8f, 1.8f);
            float z = Rng(-1.5f, 1.5f);
            AddGrassTuft(new Vector3(x, FloorY, z));
        }
    }

    private void AddGrassTuft(Vector3 pos)
    {
        for (int b = 0; b < 3; b++)
        {
            var q   = new QuadMesh { Size = new Vector2(0.05f, Rng(0.12f, 0.22f)) };
            var mat = UnlitMat(new Color(Rng(0.22f, 0.35f), Rng(0.55f, 0.75f), 0.18f));
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            q.Material = mat;
            var m = new MeshInstance3D { Mesh = q };
            m.Position = pos + new Vector3(Rng(-0.08f, 0.08f), q.Size.Y * 0.5f, Rng(-0.05f, 0.05f));
            m.Rotation = new Vector3(Rng(-0.3f, 0.3f), Rng(0, MathF.PI), Rng(-0.2f, 0.2f));
            AddChild(m);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Stone walls with vine detail
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildWalls()
    {
        BuildWall(Sim.LeftBound  - 0.2f, +1);
        BuildWall(Sim.RightBound + 0.2f, -1);
    }

    private void BuildWall(float x, int side)
    {
        // Main wall body
        var body = new MeshInstance3D();
        var b    = new BoxMesh { Size = new Vector3(0.35f, 3.0f, 3.5f) };
        b.Material = LitMat(new Color(0.52f, 0.46f, 0.38f), 0.9f);
        body.Mesh = b;
        body.Position = new Vector3(x, FloorY + 1.5f, 0);
        AddChild(body);

        // Stone brick detail rows (thin box slabs)
        for (int row = 0; row < 5; row++)
        {
            var slab = new MeshInstance3D();
            var sm   = new BoxMesh { Size = new Vector3(0.36f, 0.04f, 3.5f) };
            sm.Material = LitMat(new Color(0.38f, 0.33f, 0.26f), 0.95f);
            slab.Mesh = sm;
            slab.Position = new Vector3(x, FloorY + 0.4f + row * 0.55f, 0);
            AddChild(slab);
        }

        // Vine tendrils (thin elongated spheres)
        int vines = _rng.Next(3, 6);
        for (int v = 0; v < vines; v++)
        {
            float vy = FloorY + Rng(0.2f, 2.5f);
            float vz = Rng(-1.2f, 1.2f);
            var vm = new MeshInstance3D();
            var vs = new CapsuleMesh { Radius = 0.025f, Height = Rng(0.3f, 0.9f) };
            vs.Material = LitMat(new Color(0.14f, 0.42f, 0.14f), 0.9f);
            vm.Mesh = vs;
            vm.Position = new Vector3(x + side * 0.12f, vy, vz);
            vm.Rotation = new Vector3(Rng(-0.5f, 0.5f), 0, Rng(-0.8f, 0.8f));
            AddChild(vm);

            // Leaf on vine
            var lm = new MeshInstance3D();
            var lq = new QuadMesh { Size = new Vector2(Rng(0.10f, 0.18f), Rng(0.10f, 0.16f)) };
            lq.Material = LitMat(new Color(0.16f, 0.52f, 0.18f), 0.8f);
            ((StandardMaterial3D)lq.Material).CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            lm.Mesh = lq;
            lm.Position = vm.Position + new Vector3(Rng(-0.1f, 0.1f), Rng(0.1f, 0.25f), Rng(-0.05f, 0.05f));
            lm.Rotation = new Vector3(Rng(-0.4f, 0.4f), Rng(0, MathF.PI), Rng(-0.3f, 0.3f));
            AddChild(lm);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. Detailed trees near the walls + mid-room
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildTrees()
    {
        float[] xs = { -8.0f, -5.5f, 5.5f, 8.0f };
        foreach (float x in xs)
            BuildTree(x, 0.0f);
    }

    private void BuildTree(float x, float baseZ)
    {
        float trunkH = Rng(1.1f, 1.6f);
        float trunkR = Rng(0.09f, 0.13f);

        // Trunk
        var trunk = new MeshInstance3D();
        var tm    = new CylinderMesh
        {
            TopRadius     = trunkR * 0.6f,
            BottomRadius  = trunkR,
            Height        = trunkH,
            RadialSegments = 8,
        };
        tm.Material = LitMat(new Color(0.35f, 0.22f, 0.10f), 0.95f);
        trunk.Mesh  = tm;
        trunk.Position = new Vector3(x, FloorY + trunkH * 0.5f, baseZ);
        AddChild(trunk);

        // Multi-blob canopy
        float topY = FloorY + trunkH;
        int blobs  = _rng.Next(4, 7);
        for (int b = 0; b < blobs; b++)
        {
            float r  = Rng(0.40f, 0.70f);
            float bx = x + Rng(-0.45f, 0.45f);
            float by = topY + Rng(0f, 0.55f);
            float bz = baseZ + Rng(-0.15f, 0.15f);

            var canopy = new MeshInstance3D();
            var sm     = new SphereMesh { Radius = r, Height = r * 1.3f, RadialSegments = 10, Rings = 6 };

            // Colour variation: lighter green on top, darker below
            float bright = 0.82f + Rng(0, 0.18f);
            sm.Material = LitMat(new Color(0.13f * bright, 0.50f * bright, 0.14f * bright), 0.85f);
            canopy.Mesh = sm;
            canopy.Position = new Vector3(bx, by, bz);
            canopy.Scale    = new Vector3(1f, Rng(0.85f, 1.1f), Rng(0.7f, 0.9f));
            AddChild(canopy);
        }

        // A couple of branches (thin boxes, angled)
        for (int br = 0; br < 2; br++)
        {
            float angle = MathF.PI * (0.2f + br * 0.6f);
            var brNode = new MeshInstance3D();
            var bm = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.03f, Height = 0.45f, RadialSegments = 5 };
            bm.Material = LitMat(new Color(0.32f, 0.20f, 0.09f), 0.95f);
            brNode.Mesh = bm;
            brNode.Position = new Vector3(x + MathF.Cos(angle) * 0.2f, topY + 0.15f,
                                          baseZ + MathF.Sin(angle) * 0.2f);
            brNode.Rotation = new Vector3(0, 0, -angle * 0.4f);
            AddChild(brNode);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. Foreground plant fronds (closest to camera)
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildForegroundFronds()
    {
        float[] xs = { -9f, -6.5f, 6.5f, 9f, -3f, 3f };
        foreach (float x in xs)
        {
            int leaves = _rng.Next(3, 6);
            for (int i = 0; i < leaves; i++)
            {
                float w = Rng(0.18f, 0.34f);
                float h = Rng(0.40f, 0.80f);
                var lm = new MeshInstance3D();
                var q  = new QuadMesh { Size = new Vector2(w, h) };
                var c  = new Color(Rng(0.10f, 0.22f), Rng(0.50f, 0.68f), Rng(0.10f, 0.22f));
                q.Material = LitMat(c, 0.8f);
                ((StandardMaterial3D)q.Material).CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                lm.Mesh = q;
                lm.Position = new Vector3(x + Rng(-0.3f, 0.3f),
                                          FloorY + h * 0.5f,
                                          0.6f + Rng(0, 0.3f));
                lm.Rotation = new Vector3(Rng(-0.2f, 0.2f), Rng(-0.4f, 0.4f), Rng(-0.4f, 0.4f));
                AddChild(lm);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. Scattered ground details
    // ─────────────────────────────────────────────────────────────────────────
    private void ScatterDetails()
    {
        // Flowers
        for (int i = 0; i < 22; i++)
        {
            float x = Rng(Sim.LeftBound + 0.5f, Sim.RightBound - 0.5f);
            float z = Rng(-1.0f, 0.8f);
            AddFlower(x, z);
        }

        // Mushrooms
        for (int i = 0; i < 6; i++)
        {
            float x = Rng(Sim.LeftBound + 0.5f, Sim.RightBound - 0.5f);
            float z = Rng(-0.8f, 0.5f);
            AddMushroom(new Vector3(x, FloorY, z));
        }

        // Rocks
        for (int i = 0; i < 8; i++)
        {
            float x = Rng(Sim.LeftBound + 0.5f, Sim.RightBound - 0.5f);
            float z = Rng(-0.8f, 0.5f);
            var sm = new SphereMesh { Radius = Rng(0.05f, 0.10f), Height = Rng(0.06f, 0.12f),
                                      RadialSegments = 6, Rings = 4 };
            sm.Material = LitMat(new Color(Rng(0.45f, 0.60f), Rng(0.42f, 0.55f), Rng(0.38f, 0.50f)), 0.95f);
            var rm = new MeshInstance3D { Mesh = sm };
            rm.Position = new Vector3(x, FloorY + 0.04f, z);
            rm.Scale    = new Vector3(Rng(0.8f, 1.4f), Rng(0.6f, 0.9f), Rng(0.7f, 1.2f));
            AddChild(rm);
        }
    }

    private void AddFlower(float x, float z)
    {
        // Stem
        var stem = new MeshInstance3D();
        var smc  = new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f,
                                      Height = Rng(0.10f, 0.20f), RadialSegments = 5 };
        smc.Material = LitMat(new Color(0.20f, 0.55f, 0.18f), 0.9f);
        stem.Mesh = smc;
        float h = smc.Height;
        stem.Position = new Vector3(x, FloorY + h * 0.5f, z);
        AddChild(stem);

        // Petals (small sphere, bright colour)
        Color[] palette = {
            new(0.98f, 0.30f, 0.35f), new(0.98f, 0.82f, 0.15f),
            new(0.95f, 0.50f, 0.90f), new(0.35f, 0.55f, 0.98f),
            new(1.00f, 0.65f, 0.20f),
        };
        var flower = new MeshInstance3D();
        var fm = new SphereMesh { Radius = 0.055f, Height = 0.07f, RadialSegments = 7, Rings = 4 };
        fm.Material = LitMat(palette[_rng.Next(palette.Length)], 0.5f);
        flower.Mesh = fm;
        flower.Position = new Vector3(x, FloorY + h + 0.04f, z);
        AddChild(flower);
    }

    private void AddMushroom(Vector3 pos)
    {
        // Stalk
        var stalk = new MeshInstance3D();
        float sh = Rng(0.10f, 0.18f);
        var sm = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.025f,
                                    Height = sh, RadialSegments = 7 };
        sm.Material = LitMat(new Color(0.90f, 0.85f, 0.78f), 0.9f);
        stalk.Mesh = sm;
        stalk.Position = pos + new Vector3(0, sh * 0.5f, 0);
        AddChild(stalk);

        // Cap
        var cap = new MeshInstance3D();
        var cm = new SphereMesh { Radius = Rng(0.07f, 0.12f), Height = 0.12f, RadialSegments = 8, Rings = 4 };
        cm.Material = LitMat(new Color(Rng(0.60f, 0.90f), Rng(0.15f, 0.30f), 0.10f), 0.7f);
        cap.Mesh = cm;
        cap.Position = pos + new Vector3(0, sh + 0.05f, 0);
        cap.Scale    = new Vector3(1f, 0.55f, 1f);
        AddChild(cap);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private float Rng(float min, float max) =>
        min + (float)_rng.NextDouble() * (max - min);

    private static StandardMaterial3D UnlitMat(Color col)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = col,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
    }

    private static StandardMaterial3D LitMat(Color col, float roughness = 0.8f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor      = col,
            Roughness        = roughness,
            MetallicSpecular = 0.15f,
        };
    }
}
