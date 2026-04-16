using System;
using Godot;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Godot;

/// <summary>
/// The Colony metaroom — a new norn settlement on a planet surface after
/// the Shee Ark (Creatures 3 spaceship) has landed.
///
/// Visual concept: An alien but habitable world. The crashed/landed Ark
/// is visible in the background. The colony is built around the landing
/// site with:
///   - Lush alien vegetation (bioluminescent plants, alien trees)
///   - A cleared settlement area with primitive shelters
///   - A stream/pool for water
///   - The Ark wreckage/landed ship as a landmark
///   - Alien sky with two moons
///
/// The metaroom uses the new Room system with multiple walkable areas
/// connected by doors (pathways, ramps, tunnels).
/// </summary>
[GlobalClass]
public partial class ColonyMetaroomNode : Node3D
{
    [Export] public float RoomWidth    = 28.0f;
    [Export] public float FloorY      =  0.0f;

    // The proper sim-side metaroom with rooms
    public MetaRoom MetaRoom { get; private set; } = null!;

    private Random _rng = new Random(42069);

    public override void _Ready()
    {
        // Create the metaroom in world-sim coordinates
        MetaRoom = new MetaRoom
        {
            Id         = 0,
            Name       = "Norn Colony",
            X          = -RoomWidth * 0.5f,
            Y          = -5.0f,
            Width      = RoomWidth,
            Height     = 10.0f,
            Background = "colony",
            Music      = "colony_ambient",
        };

        BuildRoomLayout();
        BuildAlienSky();
        BuildDistantArk();
        BuildAlienMountains();
        BuildAlienForest();
        BuildClouds();
        BuildGround();
        BuildStream();
        BuildSettlement();
        BuildAlienVegetation();
        BuildWalls();
        BuildForegroundPlants();
        ScatterDetails();
        SetupLighting();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Room layout — multiple walkable zones connected by doors
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildRoomLayout()
    {
        float left  = MetaRoom.X;
        float right = MetaRoom.X + MetaRoom.Width;
        float floor = FloorY;
        float ceil  = FloorY - 3.0f;

        // Main colony ground — large central area
        var mainRoom = new Room
        {
            Id            = 0,
            XLeft         = left + 2.0f,
            XRight        = right - 2.0f,
            YLeftCeiling  = ceil,
            YRightCeiling = ceil,
            YLeftFloor    = floor,
            YRightFloor   = floor,
            Type          = RoomType.Outdoor,
        };
        MetaRoom.AddRoom(mainRoom);

        // Left forest clearing
        var leftClearing = new Room
        {
            Id            = 1,
            XLeft         = left,
            XRight        = left + 3.5f,
            YLeftCeiling  = ceil,
            YRightCeiling = ceil,
            YLeftFloor    = floor + 0.1f,
            YRightFloor   = floor,
            Type          = RoomType.Outdoor,
        };
        MetaRoom.AddRoom(leftClearing);

        // Right hillside
        var rightHill = new Room
        {
            Id            = 2,
            XLeft         = right - 3.5f,
            XRight        = right,
            YLeftCeiling  = ceil,
            YRightCeiling = ceil - 0.5f,
            YLeftFloor    = floor,
            YRightFloor   = floor - 0.3f,
            Type          = RoomType.Outdoor,
        };
        MetaRoom.AddRoom(rightHill);

        // Stream bed (lower area in the middle)
        var streamBed = new Room
        {
            Id            = 3,
            XLeft         = -2.0f,
            XRight        = 2.0f,
            YLeftCeiling  = floor,
            YRightCeiling = floor,
            YLeftFloor    = floor + 0.4f,
            YRightFloor   = floor + 0.4f,
            Type          = RoomType.WaterSurface,
        };
        MetaRoom.AddRoom(streamBed);

        // Set up room CA values
        mainRoom.CA[CaIndex.LightSource]    = 0.8f;
        mainRoom.CA[CaIndex.HeatSource]     = 0.5f;
        mainRoom.CA[CaIndex.NutrientSource] = 0.3f;
        leftClearing.CA[CaIndex.LightSource]    = 0.6f;
        leftClearing.CA[CaIndex.NutrientSource] = 0.5f;
        rightHill.CA[CaIndex.LightSource]       = 0.7f;
        streamBed.CA[CaIndex.NutrientSource]    = 0.8f;
        streamBed.CA[CaIndex.HeatSource]        = 0.3f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Alien sky — two moons, aurora, alien colour palette
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildAlienSky()
    {
        var skyMat = new ProceduralSkyMaterial();
        skyMat.SkyTopColor        = new Color(0.06f, 0.08f, 0.22f);   // deep indigo
        skyMat.SkyHorizonColor    = new Color(0.35f, 0.20f, 0.50f);   // purple-pink horizon
        skyMat.GroundHorizonColor = new Color(0.25f, 0.35f, 0.20f);   // alien green
        skyMat.GroundBottomColor  = new Color(0.10f, 0.18f, 0.08f);
        skyMat.SunAngleMax        = 20.0f;

        var sky = new Sky { SkyMaterial = skyMat };

        var env = new global::Godot.Environment();
        env.BackgroundMode     = global::Godot.Environment.BGMode.Sky;
        env.Sky                = sky;
        env.AmbientLightSource = global::Godot.Environment.AmbientSource.Sky;
        env.AmbientLightEnergy = 0.55f;
        env.FogEnabled         = true;
        env.FogMode            = global::Godot.Environment.FogModeEnum.Exponential;
        env.FogDensity         = 0.004f;
        env.FogLightColor      = new Color(0.35f, 0.28f, 0.50f);
        env.FogLightEnergy     = 0.8f;
        env.GlowEnabled        = true;
        env.GlowIntensity      = 0.8f;
        env.GlowBloom          = 0.06f;
        env.GlowStrength       = 1.4f;

        var worldEnv = new WorldEnvironment { Environment = env };
        AddChild(worldEnv);

        // Large moon (reddish-purple, behind the mountains)
        AddMoon(new Vector3(-6f, 5.5f, -6f), 1.2f, new Color(0.72f, 0.45f, 0.60f, 0.9f));
        // Small moon (pale blue, higher)
        AddMoon(new Vector3(8f, 7.0f, -5.5f), 0.5f, new Color(0.60f, 0.70f, 0.90f, 0.85f));
    }

    private void AddMoon(Vector3 pos, float radius, Color col)
    {
        var mesh = new SphereMesh
        {
            Radius = radius,
            Height = radius * 2,
            RadialSegments = 24,
            Rings = 12,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = col,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        mesh.Material = mat;
        var mi = new MeshInstance3D { Mesh = mesh, Position = pos };
        AddChild(mi);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // The landed Shee Ark — visible in the distance
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildDistantArk()
    {
        var ark = new Node3D();
        ark.Position = new Vector3(-9f, 0.6f, -4.5f);

        // Main hull (elongated cylinder, slightly tilted — it crash-landed)
        var hull = new MeshInstance3D();
        var hullMesh = new CylinderMesh
        {
            TopRadius    = 0.4f,
            BottomRadius = 0.55f,
            Height       = 3.5f,
            RadialSegments = 10,
        };
        hullMesh.Material = LitMat(new Color(0.45f, 0.50f, 0.55f), 0.6f);
        hull.Mesh = hullMesh;
        hull.RotationDegrees = new Vector3(0, 30, 12);  // tilted from landing
        ark.AddChild(hull);

        // Engine section (wider cylinder at back)
        var engine = new MeshInstance3D();
        var engMesh = new CylinderMesh
        {
            TopRadius = 0.55f, BottomRadius = 0.7f, Height = 1.2f, RadialSegments = 10,
        };
        engMesh.Material = LitMat(new Color(0.35f, 0.38f, 0.42f), 0.7f);
        engine.Mesh = engMesh;
        engine.Position = new Vector3(-0.6f, -0.3f, 0);
        engine.RotationDegrees = new Vector3(0, 30, 12);
        ark.AddChild(engine);

        // Landing scar (dark ground beneath)
        var scar = new MeshInstance3D();
        var scarMesh = new PlaneMesh { Size = new Vector2(4f, 2f) };
        scarMesh.Material = LitMat(new Color(0.15f, 0.12f, 0.10f), 1.0f);
        scar.Mesh = scarMesh;
        scar.Position = new Vector3(0, -0.55f, 0);
        ark.AddChild(scar);

        // Faint glow from the Ark's still-functioning systems
        var glow = new OmniLight3D
        {
            LightColor  = new Color(0.4f, 0.6f, 0.9f),
            LightEnergy = 0.4f,
            OmniRange   = 4.0f,
        };
        glow.Position = new Vector3(0, 0.5f, 0);
        ark.AddChild(glow);

        AddChild(ark);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Alien mountains
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildAlienMountains()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        float w    = RoomWidth + 14f;
        float baseY = FloorY - 0.3f;
        float z    = -5.0f;
        int peaks  = 12;
        float step = w / peaks;

        // Alien mountains — taller, more jagged, with purple-tinted rock
        float[] peakX = new float[peaks + 2];
        float[] peakY = new float[peaks + 2];
        peakX[0] = -w * 0.5f; peakY[0] = baseY;
        peakX[peaks + 1] = w * 0.5f; peakY[peaks + 1] = baseY;

        for (int i = 1; i <= peaks; i++)
        {
            peakX[i] = -w * 0.5f + (i - 0.5f) * step + Rng(-step * 0.3f, step * 0.3f);
            peakY[i] = FloorY + Rng(2.0f, 5.5f);
        }

        var col = new Color(0.18f, 0.15f, 0.28f);  // dark purple-slate
        for (int i = 0; i < peaks; i++)
        {
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i], baseY, z));
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i + 1], peakY[i + 1], z));
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i + 2], baseY, z));

            st.SetColor(col); st.AddVertex(new Vector3(peakX[i], baseY, z));
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i + 1], peakY[i + 1], z));
            st.SetColor(col); st.AddVertex(new Vector3(peakX[i + 1], baseY, z));
        }

        var mat = UnlitMat(col);
        mat.VertexColorUseAsAlbedo = true;
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, mat);
        AddChild(new MeshInstance3D { Mesh = mesh });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Alien forest canopy — bioluminescent trees with unusual shapes
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildAlienForest()
    {
        float z = -3.5f;
        int count = 18;
        float step = (RoomWidth + 8) / count;

        for (int i = 0; i < count; i++)
        {
            float x = -(RoomWidth + 8) * 0.5f + i * step + Rng(-step * 0.3f, step * 0.3f);
            float r = Rng(0.5f, 1.0f);
            float y = FloorY + r * 0.4f + Rng(0, 0.5f);

            // Alien tree canopy — elongated shapes in unusual colours
            var s = new SphereMesh { Radius = r, Height = r * 1.8f, RadialSegments = 8, Rings = 4 };
            var treeCols = new Color[] {
                new(0.10f, 0.50f, 0.35f),  // teal-green
                new(0.15f, 0.42f, 0.48f),  // cyan-teal
                new(0.30f, 0.55f, 0.25f),  // alien green
                new(0.20f, 0.35f, 0.50f),  // blue-green
            };
            s.Material = UnlitMat(treeCols[_rng.Next(treeCols.Length)]);
            var m = new MeshInstance3D { Mesh = s };
            m.Position = new Vector3(x, y, z);
            m.Scale = new Vector3(1f, Rng(0.9f, 1.5f), 0.5f);
            AddChild(m);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Alien clouds
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildClouds()
    {
        float[] xs = { -8f, -1f, 5f, 10f, -12f, 3f };
        float[] ys = { 4.0f, 4.8f, 3.8f, 5.2f, 4.4f, 5.5f };

        for (int c = 0; c < xs.Length; c++)
        {
            var cloud = new Node3D { Position = new Vector3(xs[c], ys[c], -3.0f) };
            int blobs = _rng.Next(3, 6);
            for (int b = 0; b < blobs; b++)
            {
                float r = Rng(0.25f, 0.55f);
                var s = new SphereMesh { Radius = r, Height = r * 1.3f, RadialSegments = 8, Rings = 4 };
                var mat = new StandardMaterial3D
                {
                    // Slightly purple-tinted clouds on an alien world
                    AlbedoColor  = new Color(0.75f, 0.72f, 0.85f, 0.70f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode     = BaseMaterial3D.CullModeEnum.Disabled,
                };
                s.Material = mat;
                var m = new MeshInstance3D { Mesh = s };
                m.Position = new Vector3(Rng(-0.5f, 0.5f) * blobs * 0.3f, Rng(-0.1f, 0.1f), 0);
                cloud.AddChild(m);
            }
            AddChild(cloud);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ground — alien soil with mossy/crystalline patches
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildGround()
    {
        // Main ground plane — darker alien soil
        var plane = new MeshInstance3D();
        var pm = new PlaneMesh
        {
            Size = new Vector2(RoomWidth + 4f, 10f),
            SubdivideDepth = 8,
            SubdivideWidth = 24,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor      = new Color(0.22f, 0.35f, 0.18f),
            Roughness        = 0.95f,
            MetallicSpecular = 0.0f,
        };
        pm.Material = mat;
        plane.Mesh = pm;
        plane.Position = new Vector3(0, FloorY, 0);
        AddChild(plane);

        // Colony settlement clearing (lighter, packed earth)
        var clearing = new MeshInstance3D();
        var cp = new PlaneMesh { Size = new Vector2(8.0f, 6f) };
        cp.Material = LitMat(new Color(0.40f, 0.32f, 0.22f), 1.0f);
        clearing.Mesh = cp;
        clearing.Position = new Vector3(3f, FloorY + 0.002f, 0);
        AddChild(clearing);

        // Alien moss patches (bioluminescent green-blue)
        for (int i = 0; i < 8; i++)
        {
            float x = Rng(-RoomWidth * 0.45f, RoomWidth * 0.45f);
            float z = Rng(-1.5f, 1.5f);
            var patch = new MeshInstance3D();
            var pp = new PlaneMesh { Size = new Vector2(Rng(0.8f, 1.5f), Rng(0.6f, 1.2f)) };
            pp.Material = LitMat(new Color(0.15f, 0.55f, 0.40f), 0.7f);
            patch.Mesh = pp;
            patch.Position = new Vector3(x, FloorY + 0.003f, z);
            patch.RotationDegrees = new Vector3(0, Rng(0, 360), 0);
            AddChild(patch);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stream / water pool
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildStream()
    {
        // A winding stream flowing through the colony
        float streamZ = 0.8f;

        // Main water surface (semi-transparent blue)
        var water = new MeshInstance3D();
        var wm = new PlaneMesh { Size = new Vector2(5.0f, 1.5f) };
        var waterMat = new StandardMaterial3D
        {
            AlbedoColor  = new Color(0.15f, 0.40f, 0.55f, 0.75f),
            Roughness    = 0.1f,
            Metallic     = 0.3f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        wm.Material = waterMat;
        water.Mesh = wm;
        water.Position = new Vector3(0, FloorY - 0.05f, streamZ);
        AddChild(water);

        // Stream banks (raised earth edges)
        for (int side = -1; side <= 1; side += 2)
        {
            var bank = new MeshInstance3D();
            var bm = new BoxMesh { Size = new Vector3(5.5f, 0.12f, 0.3f) };
            bm.Material = LitMat(new Color(0.28f, 0.22f, 0.14f), 0.95f);
            bank.Mesh = bm;
            bank.Position = new Vector3(0, FloorY + 0.03f, streamZ + side * 0.8f);
            AddChild(bank);
        }

        // Bioluminescent water plants (glowing spots in the water)
        for (int i = 0; i < 6; i++)
        {
            var glow = new OmniLight3D
            {
                LightColor  = new Color(0.1f, 0.8f, 0.6f),
                LightEnergy = 0.15f,
                OmniRange   = 0.6f,
            };
            glow.Position = new Vector3(Rng(-2f, 2f), FloorY + 0.05f, streamZ + Rng(-0.4f, 0.4f));
            AddChild(glow);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Settlement structures — primitive shelters built from Ark salvage
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildSettlement()
    {
        // Shelter 1 — lean-to made from hull plating
        BuildShelter(new Vector3(4f, FloorY, -0.5f), 1.0f);
        // Shelter 2 — smaller hut
        BuildShelter(new Vector3(6.5f, FloorY, 0.2f), 0.7f);
        // Storage container (salvaged from the Ark)
        BuildContainer(new Vector3(2.5f, FloorY, -0.8f));
    }

    private void BuildShelter(Vector3 pos, float scale)
    {
        var shelter = new Node3D { Position = pos };

        // Support posts (metal from the Ark)
        for (int i = -1; i <= 1; i += 2)
        {
            var post = new MeshInstance3D();
            var pm = new CylinderMesh
            {
                TopRadius = 0.03f * scale, BottomRadius = 0.04f * scale,
                Height = 0.8f * scale, RadialSegments = 6,
            };
            pm.Material = LitMat(new Color(0.50f, 0.55f, 0.58f), 0.5f);
            post.Mesh = pm;
            post.Position = new Vector3(i * 0.5f * scale, 0.4f * scale, 0);
            shelter.AddChild(post);
        }

        // Roof (angled metal plate)
        var roof = new MeshInstance3D();
        var rm = new BoxMesh { Size = new Vector3(1.2f * scale, 0.04f, 0.8f * scale) };
        rm.Material = LitMat(new Color(0.42f, 0.45f, 0.50f), 0.6f);
        roof.Mesh = rm;
        roof.Position = new Vector3(0, 0.82f * scale, 0);
        roof.RotationDegrees = new Vector3(8, 0, 0);
        shelter.AddChild(roof);

        AddChild(shelter);
    }

    private void BuildContainer(Vector3 pos)
    {
        var box = new MeshInstance3D();
        var bm = new BoxMesh { Size = new Vector3(0.6f, 0.4f, 0.4f) };
        bm.Material = LitMat(new Color(0.55f, 0.52f, 0.45f), 0.7f);
        box.Mesh = bm;
        box.Position = pos + new Vector3(0, 0.2f, 0);
        AddChild(box);

        // Hazard stripes detail
        var stripe = new MeshInstance3D();
        var sm = new BoxMesh { Size = new Vector3(0.61f, 0.06f, 0.41f) };
        sm.Material = LitMat(new Color(0.85f, 0.65f, 0.10f), 0.8f);
        stripe.Mesh = sm;
        stripe.Position = pos + new Vector3(0, 0.35f, 0);
        AddChild(stripe);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Alien vegetation — bioluminescent plants with unusual forms
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildAlienVegetation()
    {
        // Tall bioluminescent stalks
        float[] stalkXs = { -5f, -3f, 7f, 9f, -8f, 11f, -11f };
        foreach (float x in stalkXs)
            BuildGlowStalk(x, Rng(-0.5f, 0.8f));

        // Alien fern clusters
        for (int i = 0; i < 10; i++)
        {
            float x = Rng(-RoomWidth * 0.45f, RoomWidth * 0.45f);
            float z = Rng(-1.0f, 0.6f);
            BuildAlienFern(x, z);
        }

        // Crystal formations (mineral deposits from alien soil)
        BuildCrystalCluster(new Vector3(-7f, FloorY, 0.2f));
        BuildCrystalCluster(new Vector3(10f, FloorY, -0.3f));
    }

    private void BuildGlowStalk(float x, float z)
    {
        float height = Rng(0.8f, 1.8f);

        // Stalk
        var stalk = new MeshInstance3D();
        var sm = new CylinderMesh
        {
            TopRadius = 0.015f, BottomRadius = 0.03f,
            Height = height, RadialSegments = 6,
        };
        sm.Material = LitMat(new Color(0.12f, 0.45f, 0.30f), 0.9f);
        stalk.Mesh = sm;
        stalk.Position = new Vector3(x, FloorY + height * 0.5f, z);
        stalk.Rotation = new Vector3(Rng(-0.1f, 0.1f), 0, Rng(-0.1f, 0.1f));
        AddChild(stalk);

        // Glowing bulb at top
        var bulb = new MeshInstance3D();
        var bm = new SphereMesh { Radius = Rng(0.06f, 0.12f), Height = Rng(0.10f, 0.18f),
                                   RadialSegments = 8, Rings = 4 };
        var bulbCol = new Color[] {
            new(0.2f, 0.9f, 0.6f),   // cyan-green
            new(0.5f, 0.3f, 0.9f),   // purple
            new(0.9f, 0.7f, 0.2f),   // amber
            new(0.3f, 0.7f, 0.9f),   // sky blue
        };
        var chosenCol = bulbCol[_rng.Next(bulbCol.Length)];
        var bulbMat = new StandardMaterial3D
        {
            AlbedoColor = chosenCol,
            Emission    = chosenCol,
            EmissionEnergyMultiplier = 2.0f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        bm.Material = bulbMat;
        bulb.Mesh = bm;
        bulb.Position = new Vector3(x, FloorY + height + 0.05f, z);
        AddChild(bulb);

        // Point light for the glow
        var light = new OmniLight3D
        {
            LightColor  = chosenCol,
            LightEnergy = 0.25f,
            OmniRange   = 1.2f,
        };
        light.Position = bulb.Position;
        AddChild(light);
    }

    private void BuildAlienFern(float x, float z)
    {
        int fronds = _rng.Next(3, 6);
        for (int f = 0; f < fronds; f++)
        {
            float w = Rng(0.08f, 0.20f);
            float h = Rng(0.20f, 0.50f);
            var frond = new MeshInstance3D();
            var q = new QuadMesh { Size = new Vector2(w, h) };
            var col = new Color(Rng(0.08f, 0.18f), Rng(0.45f, 0.65f), Rng(0.25f, 0.45f));
            q.Material = LitMat(col, 0.7f);
            ((StandardMaterial3D)q.Material).CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            frond.Mesh = q;
            frond.Position = new Vector3(x + Rng(-0.15f, 0.15f), FloorY + h * 0.5f, z + Rng(-0.1f, 0.1f));
            frond.Rotation = new Vector3(Rng(-0.3f, 0.3f), Rng(0, MathF.PI), Rng(-0.5f, 0.5f));
            AddChild(frond);
        }
    }

    private void BuildCrystalCluster(Vector3 pos)
    {
        int crystals = _rng.Next(3, 7);
        var crystalCol = new Color(0.55f, 0.40f, 0.85f);  // purple crystal

        for (int c = 0; c < crystals; c++)
        {
            float h = Rng(0.15f, 0.50f);
            float r = Rng(0.02f, 0.06f);
            var crystal = new MeshInstance3D();
            var cm = new CylinderMesh
            {
                TopRadius = 0.005f, BottomRadius = r,
                Height = h, RadialSegments = 5,
            };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = crystalCol,
                Emission    = crystalCol * 0.5f,
                EmissionEnergyMultiplier = 1.5f,
                Roughness   = 0.1f,
                Metallic    = 0.5f,
            };
            cm.Material = mat;
            crystal.Mesh = cm;
            crystal.Position = pos + new Vector3(Rng(-0.2f, 0.2f), h * 0.5f, Rng(-0.15f, 0.15f));
            crystal.Rotation = new Vector3(Rng(-0.3f, 0.3f), Rng(0, MathF.PI), Rng(-0.3f, 0.3f));
            AddChild(crystal);
        }

        // Glow
        var glow = new OmniLight3D
        {
            LightColor  = crystalCol,
            LightEnergy = 0.3f,
            OmniRange   = 1.5f,
        };
        glow.Position = pos + new Vector3(0, 0.3f, 0);
        AddChild(glow);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Boundary walls (rocky cliff faces)
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildWalls()
    {
        float left  = MetaRoom.X;
        float right = MetaRoom.X + MetaRoom.Width;
        BuildCliffWall(left - 0.2f, +1);
        BuildCliffWall(right + 0.2f, -1);
    }

    private void BuildCliffWall(float x, int side)
    {
        // Main cliff body (irregular rock)
        var body = new MeshInstance3D();
        var b = new BoxMesh { Size = new Vector3(0.5f, 3.5f, 4f) };
        b.Material = LitMat(new Color(0.30f, 0.25f, 0.32f), 0.95f);
        body.Mesh = b;
        body.Position = new Vector3(x, FloorY + 1.75f, 0);
        AddChild(body);

        // Rock ledge details
        for (int row = 0; row < 4; row++)
        {
            var ledge = new MeshInstance3D();
            var lm = new BoxMesh { Size = new Vector3(0.52f, Rng(0.08f, 0.15f), Rng(1.5f, 3.5f)) };
            lm.Material = LitMat(new Color(0.25f, 0.20f, 0.28f), 0.95f);
            ledge.Mesh = lm;
            ledge.Position = new Vector3(x + side * 0.05f, FloorY + 0.5f + row * 0.7f, Rng(-0.5f, 0.5f));
            AddChild(ledge);
        }

        // Alien vine tendrils
        int vines = _rng.Next(2, 5);
        for (int v = 0; v < vines; v++)
        {
            float vy = FloorY + Rng(0.3f, 2.5f);
            float vz = Rng(-1.0f, 1.0f);
            var vine = new MeshInstance3D();
            var vm = new CapsuleMesh { Radius = 0.02f, Height = Rng(0.4f, 1.0f) };
            vm.Material = LitMat(new Color(0.10f, 0.50f, 0.30f), 0.8f);
            vine.Mesh = vm;
            vine.Position = new Vector3(x + side * 0.15f, vy, vz);
            vine.Rotation = new Vector3(Rng(-0.5f, 0.5f), 0, Rng(-0.8f, 0.8f));
            AddChild(vine);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Foreground plants
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildForegroundPlants()
    {
        float[] xs = { -12f, -8f, 8f, 12f, -4f, 4f };
        foreach (float x in xs)
        {
            int leaves = _rng.Next(3, 6);
            for (int i = 0; i < leaves; i++)
            {
                float w = Rng(0.15f, 0.30f);
                float h = Rng(0.35f, 0.70f);
                var leaf = new MeshInstance3D();
                var q = new QuadMesh { Size = new Vector2(w, h) };
                var col = new Color(Rng(0.08f, 0.18f), Rng(0.42f, 0.60f), Rng(0.20f, 0.40f));
                q.Material = LitMat(col, 0.7f);
                ((StandardMaterial3D)q.Material).CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                leaf.Mesh = q;
                leaf.Position = new Vector3(x + Rng(-0.3f, 0.3f), FloorY + h * 0.5f, 0.7f + Rng(0, 0.3f));
                leaf.Rotation = new Vector3(Rng(-0.2f, 0.2f), Rng(-0.4f, 0.4f), Rng(-0.4f, 0.4f));
                AddChild(leaf);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ground scatter details
    // ─────────────────────────────────────────────────────────────────────────
    private void ScatterDetails()
    {
        float left  = MetaRoom.X + 0.5f;
        float right = MetaRoom.X + MetaRoom.Width - 0.5f;

        // Alien flowers (bioluminescent)
        for (int i = 0; i < 18; i++)
        {
            float x = Rng(left, right);
            float z = Rng(-1.0f, 0.5f);
            AddAlienFlower(x, z);
        }

        // Rocks (darker, alien minerals)
        for (int i = 0; i < 10; i++)
        {
            float x = Rng(left, right);
            float z = Rng(-0.8f, 0.5f);
            var sm = new SphereMesh
            {
                Radius = Rng(0.04f, 0.09f), Height = Rng(0.05f, 0.10f),
                RadialSegments = 6, Rings = 4,
            };
            sm.Material = LitMat(new Color(Rng(0.30f, 0.42f), Rng(0.28f, 0.36f), Rng(0.35f, 0.45f)), 0.95f);
            var rm = new MeshInstance3D { Mesh = sm };
            rm.Position = new Vector3(x, FloorY + 0.03f, z);
            rm.Scale = new Vector3(Rng(0.8f, 1.4f), Rng(0.5f, 0.8f), Rng(0.7f, 1.2f));
            AddChild(rm);
        }

        // Scattered Ark debris (small metal pieces)
        for (int i = 0; i < 5; i++)
        {
            float x = Rng(left, right);
            float z = Rng(-0.5f, 0.3f);
            var debris = new MeshInstance3D();
            var dm = new BoxMesh { Size = new Vector3(Rng(0.05f, 0.15f), 0.02f, Rng(0.04f, 0.12f)) };
            dm.Material = LitMat(new Color(0.50f, 0.52f, 0.48f), 0.6f);
            debris.Mesh = dm;
            debris.Position = new Vector3(x, FloorY + 0.01f, z);
            debris.RotationDegrees = new Vector3(Rng(-10, 10), Rng(0, 360), Rng(-10, 10));
            AddChild(debris);
        }
    }

    private void AddAlienFlower(float x, float z)
    {
        // Stem
        float stemH = Rng(0.08f, 0.18f);
        var stem = new MeshInstance3D();
        var smc = new CylinderMesh
        {
            TopRadius = 0.010f, BottomRadius = 0.012f,
            Height = stemH, RadialSegments = 5,
        };
        smc.Material = LitMat(new Color(0.15f, 0.45f, 0.25f), 0.9f);
        stem.Mesh = smc;
        stem.Position = new Vector3(x, FloorY + stemH * 0.5f, z);
        AddChild(stem);

        // Glowing blossom
        Color[] palette = {
            new(0.2f, 0.9f, 0.7f), new(0.8f, 0.3f, 0.9f),
            new(0.9f, 0.8f, 0.2f), new(0.3f, 0.6f, 0.95f),
            new(0.95f, 0.4f, 0.3f),
        };
        var col = palette[_rng.Next(palette.Length)];
        var flower = new MeshInstance3D();
        var fm = new SphereMesh { Radius = 0.04f, Height = 0.06f, RadialSegments = 7, Rings = 4 };
        var fmat = new StandardMaterial3D
        {
            AlbedoColor = col,
            Emission = col,
            EmissionEnergyMultiplier = 1.2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        fm.Material = fmat;
        flower.Mesh = fm;
        flower.Position = new Vector3(x, FloorY + stemH + 0.03f, z);
        AddChild(flower);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lighting — alien world dual-sun + fill
    // ─────────────────────────────────────────────────────────────────────────
    private void SetupLighting()
    {
        // Primary sun (warm, from upper-left)
        var sun = new DirectionalLight3D
        {
            LightColor  = new Color(0.95f, 0.85f, 0.70f),
            LightEnergy = 0.7f,
        };
        sun.RotationDegrees = new Vector3(-35, -25, 0);
        AddChild(sun);

        // Secondary fill light (cool, from opposite side — simulates alien atmosphere scatter)
        var fill = new DirectionalLight3D
        {
            LightColor  = new Color(0.50f, 0.55f, 0.80f),
            LightEnergy = 0.3f,
            ShadowEnabled = false,
        };
        fill.RotationDegrees = new Vector3(-20, 150, 0);
        AddChild(fill);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private float Rng(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

    private static StandardMaterial3D UnlitMat(Color col) => new()
    {
        AlbedoColor = col,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    private static StandardMaterial3D LitMat(Color col, float roughness = 0.8f) => new()
    {
        AlbedoColor      = col,
        Roughness        = roughness,
        MetallicSpecular = 0.15f,
    };
}
