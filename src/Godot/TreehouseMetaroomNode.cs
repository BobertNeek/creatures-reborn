using System;
using System.Collections.Generic;
using Godot;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Godot;

/// <summary>
/// The Treehouse metaroom — a cozy multi-floor biotech tree dwelling.
///
/// Based on the regenerated v2 backdrop
/// (art/metaroom/metaroom-right-connector-v2.png), the
/// scene uses authored FloorPlateNode spans so walkable geometry can stay
/// aligned with the painting without placing foreground art over the norn lanes.
///
///   Floor 0: library / observatory / study.
///   Floor 1: lab / hammock / greenhouse deck.
///   Floor 2: pond / cave / root nursery deck.
///
/// Floor-to-floor travel is by two side ramps plus the central elevator.
///
/// Rendering: the backdrop PNG is painted onto a huge QuadMesh that sits
/// behind all agents (z = -10). If the file is missing we build a procedural
/// placeholder (three coloured bands) so the scene still loads.
/// </summary>
[GlobalClass]
public partial class TreehouseMetaroomNode : Node3D
{
    // ── Exports ──────────────────────────────────────────────────────────────
    [Export] public Texture2D? Backdrop;
    [Export] public float RoomWidth  = 40.0f;
    [Export] public float RoomHeight = 13.5f;
    [Export] public string BackdropPath = "res://art/metaroom/metaroom-right-connector-v2.png";

    // ── Floor Y values (match image layout) ──────────────────────────────────
    // Measured against the regenerated Treehouse paintings after replacing the
    // Nano Banana background. The image reads as three main gameplay tiers.
    public const float TopFloorY     = 11.25f;
    public const float MidHighFloorY = 7.65f;
    public const float MidLowFloorY  = 7.65f;
    public const float BottomFloorY  = 3.35f;
    public const float BackdropCenterY = 8.45f;

    // Legacy alias kept pointing at the most common mid height so existing
    // call sites (PointerAgent drop logic, etc.) don't break while the scene
    // is being re-laid-out against the painting.
    public const float MidFloorY = MidHighFloorY;

    // Per-floor walkable x range. The metaroom is symmetric around x = 0
    // and the ±RoomWidth/2 values define hard walls.
    public float LeftWall  => -RoomWidth * 0.5f + 2.0f;
    public float RightWall =>  RoomWidth * 0.5f - 2.0f;

    // ── Sim-side MetaRoom (one logical room per floor) ───────────────────────
    public MetaRoom MetaRoom { get; private set; } = null!;

    public override void _Ready()
    {
        MetaRoom = new MetaRoom
        {
            Id         = 0,
            Name       = "Treehouse",
            X          = -RoomWidth * 0.5f,
            Y          = -1.0f,
            Width      = RoomWidth,
            Height     = RoomHeight,
            Background = "treehouse",
            Music      = "treehouse_ambient",
        };

        BuildRoomLayout();
        BuildBackdrop();
        // NOTE: no more auto-BuildFloorPlates. The painted backdrop already
        // shows the floors, and the painting's walkable decks are at many
        // different Y values (not three clean bands). Walkable regions are
        // instead placed in the scene as FloorPlateNode instances so each
        // can be tuned to its own X/Y without fighting the painting.
        SetupLighting();
    }

    public void RebuildRoomLayoutFromFloorPlates(IEnumerable<FloorPlateNode> floorPlates)
    {
        MetaRoom.ClearRooms();

        int id = 0;
        foreach (FloorPlateNode plate in floorPlates)
        {
            if (plate.XRight <= plate.XLeft) continue;

            float ceilingOffset = EstimateCeilingOffset(plate);
            MetaRoom.AddRoom(new Room
            {
                Id = id++,
                XLeft = plate.XLeft,
                XRight = plate.XRight,
                YLeftFloor = plate.YLeft,
                YRightFloor = plate.YRight,
                YLeftCeiling = plate.YLeft + ceilingOffset,
                YRightCeiling = plate.YRight + ceilingOffset,
                Type = EstimateRoomType(plate),
            });
        }

        if (id == 0)
            BuildRoomLayout();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public helpers for other nodes
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Get the world-Y for a floor index (0=top, 1=mid, 2=bottom).</summary>
    public static float GetFloorY(int floor) => floor switch
    {
        0 => TopFloorY,
        1 => MidFloorY,
        _ => BottomFloorY,
    };

    /// <summary>Map a world-Y back to a floor index (nearest).</summary>
    public static int GetFloorIndex(float y)
    {
        float dTop = MathF.Abs(y - TopFloorY);
        float dMid = MathF.Abs(y - MidFloorY);
        float dBot = MathF.Abs(y - BottomFloorY);
        if (dTop < dMid && dTop < dBot) return 0;
        if (dMid < dBot) return 1;
        return 2;
    }

    public float GetNearestFloorY(float x, float y)
    {
        if (MetaRoom.Rooms.Count == 0)
            return GetFloorY(GetFloorIndex(y));

        Room? best = null;
        float bestDistance = float.MaxValue;
        float bestY = BottomFloorY;

        for (int i = 0; i < MetaRoom.Rooms.Count; i++)
        {
            Room room = MetaRoom.Rooms[i];
            if (x < room.XLeft || x > room.XRight) continue;

            float floorY = room.FloorYAtX(x);
            float distance = MathF.Abs(floorY - y);
            if (distance < bestDistance)
            {
                best = room;
                bestY = floorY;
                bestDistance = distance;
            }
        }

        if (best != null) return bestY;

        for (int i = 0; i < MetaRoom.Rooms.Count; i++)
        {
            Room room = MetaRoom.Rooms[i];
            float sx = Math.Clamp(x, room.XLeft, room.XRight);
            float floorY = room.FloorYAtX(sx);
            float dx = sx - x;
            float dy = floorY - y;
            float distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestY = floorY;
                bestDistance = distance;
            }
        }

        return bestY;
    }

    private static float EstimateCeilingOffset(FloorPlateNode plate)
        => plate.Name.ToString().Contains("Pond", StringComparison.OrdinalIgnoreCase)
            ? 2.0f
            : 3.0f;

    private static RoomType EstimateRoomType(FloorPlateNode plate)
    {
        string name = plate.Name.ToString();
        if (name.Contains("Pond", StringComparison.OrdinalIgnoreCase))
            return RoomType.Underwater;
        if (name.Contains("Bottom", StringComparison.OrdinalIgnoreCase))
            return RoomType.Soil;
        return RoomType.IndoorWood;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Room layout — one sim-Room per floor (walkable X range)
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildRoomLayout()
    {
        // Top floor
        MetaRoom.AddRoom(new Room
        {
            Id            = 0,
            XLeft         = LeftWall,
            XRight        = RightWall,
            YLeftFloor    = TopFloorY,
            YRightFloor   = TopFloorY,
            YLeftCeiling  = TopFloorY + 3.5f,
            YRightCeiling = TopFloorY + 3.5f,
            Type          = RoomType.IndoorWood,
        });

        // Middle floor
        MetaRoom.AddRoom(new Room
        {
            Id            = 1,
            XLeft         = LeftWall,
            XRight        = RightWall,
            YLeftFloor    = MidFloorY,
            YRightFloor   = MidFloorY,
            YLeftCeiling  = MidFloorY + 3.5f,
            YRightCeiling = MidFloorY + 3.5f,
            Type          = RoomType.IndoorWood,
        });

        // Bottom floor — includes a water region for the pond
        MetaRoom.AddRoom(new Room
        {
            Id            = 2,
            XLeft         = LeftWall,
            XRight        = RightWall,
            YLeftFloor    = BottomFloorY,
            YRightFloor   = BottomFloorY,
            YLeftCeiling  = BottomFloorY + 3.5f,
            YRightCeiling = BottomFloorY + 3.5f,
            Type          = RoomType.Soil,
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Backdrop — a single huge textured quad behind everything
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildBackdrop()
    {
        // 3-step fallback to maximise the chance the user's PNG is found:
        //   1. Honour the [Export] Backdrop assigned in the editor
        //   2. Raw Image.LoadFromFile on the globalised filesystem path
        //      (works even if Godot hasn't imported the asset yet)
        //   3. ResourceLoader on the res:// path (needs Godot import .import file)
        // Only after all three fail do we paint the procedural placeholder.
        var tex = Backdrop;

        if (tex == null)
        {
            string absPath = ProjectSettings.GlobalizePath(BackdropPath);
            if (System.IO.File.Exists(absPath))
            {
                var img = Image.LoadFromFile(absPath);
                if (img != null)
                {
                    tex = ImageTexture.CreateFromImage(img);
                    GD.Print($"[TreehouseMetaroom] Loaded backdrop directly from {absPath}.");
                }
            }
        }

        if (tex == null && ResourceLoader.Exists(BackdropPath))
            tex = GD.Load<Texture2D>(BackdropPath);

        StandardMaterial3D mat;
        if (tex != null)
        {
            mat = new StandardMaterial3D
            {
                AlbedoTexture = tex,
                ShadingMode   = BaseMaterial3D.ShadingModeEnum.Unshaded,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
                CullMode      = BaseMaterial3D.CullModeEnum.Disabled,
            };
        }
        else
        {
            GD.PrintErr($"[TreehouseMetaroom] Backdrop not found at {BackdropPath} " +
                        $"(also tried {ProjectSettings.GlobalizePath(BackdropPath)}) — using placeholder.");
            mat = BuildPlaceholderMaterial();
        }

        var backdrop = new MeshInstance3D
        {
            Name = "Backdrop",
            Mesh = new QuadMesh { Size = new Vector2(RoomWidth, RoomHeight) },
            Position = new Vector3(0, BackdropCenterY, -5.0f),
        };
        backdrop.MaterialOverride = mat;
        AddChild(backdrop);
    }

    /// <summary>
    /// Painted-in-code treehouse placeholder used when treehouse_backdrop.png is
    /// missing. Wide aspect (matches RoomWidth:RoomHeight ≈ 3:1) and reads as a
    /// real backdrop — night sky + canopy on top, wood-panelled observatory,
    /// gnarled root divider, living/lab with crystal wall, another root divider,
    /// then cave-and-pond at the bottom. Two vertical tree-trunk columns mark
    /// the elevator shafts at world x = -8 and x = +6.
    ///
    /// Deterministic: all noise comes from a fixed-seed RNG so the placeholder
    /// looks identical every launch.
    /// </summary>
    private StandardMaterial3D BuildPlaceholderMaterial()
    {
        const int W = 384, H = 128;
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgb8);
        var rng = new Random(0x7EA7);

        // Vertical band boundaries (image y grows downward):
        //   0 .. 10     sky
        //  10 .. 22     canopy leaves fringe
        //  22 .. 46     top floor interior (observatory/library)
        //  46 .. 54     gnarled root divider
        //  54 .. 80     middle floor interior (living/lab)
        //  80 .. 88     gnarled root divider
        //  88 .. 128    bottom floor (caves + pond)
        //
        // Tree-trunk columns (elevator shafts) map world x = -8, +6 onto image
        // x via x_img = (world_x + RoomWidth/2) / RoomWidth * W.
        //   -8 → (12/40) * 384 = 115
        //   +6 → (26/40) * 384 = 250
        int trunkL = 115, trunkR = 250;
        const int trunkHalfW = 8;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                Color c = PaintPixel(x, y, W, H, trunkL, trunkR, trunkHalfW, rng);
                img.SetPixel(x, y, c);
            }
        }

        // Scatter a few stars into the sky band
        var starRng = new Random(0xC0FFEE);
        for (int i = 0; i < 30; i++)
        {
            int sx = starRng.Next(W);
            int sy = starRng.Next(10);
            float b = 0.75f + (float)starRng.NextDouble() * 0.25f;
            img.SetPixel(sx, sy, new Color(b, b, b * 0.95f));
        }

        // Scatter glowing crystals into the cave band
        var crystalRng = new Random(0xBADC);
        for (int i = 0; i < 18; i++)
        {
            int cx = crystalRng.Next(W);
            int cy = 95 + crystalRng.Next(25);
            var hue = crystalRng.Next(3) switch
            {
                0 => new Color(0.55f, 0.95f, 1.00f),  // cyan
                1 => new Color(0.90f, 0.60f, 1.00f),  // violet
                _ => new Color(0.70f, 1.00f, 0.80f),  // pale green
            };
            img.SetPixel(cx, cy, hue);
            if (cx + 1 < W) img.SetPixel(cx + 1, cy, hue * 0.75f);
            if (cy + 1 < H) img.SetPixel(cx, cy + 1, hue * 0.55f);
        }

        return new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(img),
            ShadingMode   = BaseMaterial3D.ShadingModeEnum.Unshaded,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
        };
    }

    /// <summary>
    /// Per-pixel painter for the procedural treehouse. Split out from the main
    /// loop so the vertical structure reads top-to-bottom and each band has its
    /// own short colour expression.
    /// </summary>
    private static Color PaintPixel(
        int x, int y, int W, int H,
        int trunkL, int trunkR, int trunkHalfW, Random rng)
    {
        // Deterministic low-amplitude noise so surfaces aren't dead flat.
        // Using rng.NextDouble() is fine because we iterate pixels in a fixed
        // order with a fixed-seed Random.
        float n = ((float)rng.NextDouble() - 0.5f) * 0.06f;

        // Tree-trunk columns cut through every band except the sky.
        bool inTrunkL = System.Math.Abs(x - trunkL) <= trunkHalfW;
        bool inTrunkR = System.Math.Abs(x - trunkR) <= trunkHalfW;
        bool inTrunk  = (inTrunkL || inTrunkR) && y > 8;
        if (inTrunk)
        {
            // Gnarled dark-brown trunk with vertical bark striations.
            float bark = ((x + y / 3) % 4 == 0) ? -0.05f : 0.0f;
            return new Color(0.22f + bark + n, 0.14f + n, 0.08f + n);
        }

        // ── Sky (0..10) ─────────────────────────────────────────────────────
        if (y < 10)
            return new Color(0.07f + n, 0.09f + n, 0.22f + n);

        // ── Canopy leaves (10..22) ──────────────────────────────────────────
        if (y < 22)
        {
            // Scalloped lower edge: random short dips
            float scallop = MathF.Sin(x * 0.18f) * 1.2f + MathF.Sin(x * 0.07f) * 2.0f;
            float bottom  = 22 - MathF.Abs(scallop);
            if (y < bottom)
            {
                // Darker deeper in, lighter near edge
                float t = (y - 10) / 12f;
                return new Color(0.06f + t * 0.05f + n,
                                 0.22f + t * 0.15f + n,
                                 0.14f + t * 0.07f + n);
            }
            // else fall through to interior below
        }

        // ── Top floor — observatory / library (22..46) ──────────────────────
        if (y < 46)
        {
            // Warm wood walls. Central dome arch suggested by a lighter band.
            float domeDist = MathF.Abs(x - W * 0.5f);
            bool  inDome   = domeDist < 55 && y < 36;
            if (inDome)
            {
                // Domed ceiling glow
                float g = 0.50f - (y - 22) / 28f;
                return new Color(0.45f + g + n, 0.40f + g + n, 0.30f + g * 0.5f + n);
            }
            // Bookshelves on left third
            if (x < W * 0.22f && ((y / 3) % 2 == 0))
                return new Color(0.30f + n, 0.18f + n, 0.10f + n);
            // Map / study on right third — parchment tone
            if (x > W * 0.78f && y > 28 && y < 42)
                return new Color(0.68f + n, 0.60f + n, 0.42f + n);
            // Default warm wood panel
            return new Color(0.48f + n, 0.33f + n, 0.20f + n);
        }

        // ── Root divider (46..54) ───────────────────────────────────────────
        if (y < 54)
        {
            float knot = MathF.Sin(x * 0.12f) * 0.04f;
            return new Color(0.28f + knot + n, 0.18f + n, 0.10f + n);
        }

        // ── Middle floor — living / lab (54..80) ────────────────────────────
        if (y < 80)
        {
            // Crystal wall on right quarter (pale violet with inner glow)
            if (x > W * 0.75f)
            {
                float t = (x - W * 0.75f) / (W * 0.25f);
                return new Color(0.55f + t * 0.15f + n,
                                 0.45f + t * 0.20f + n,
                                 0.70f + t * 0.20f + n);
            }
            // Hammock-nook silhouette left of centre
            float hx = x - W * 0.30f;
            float hy = y - 68;
            if (hx * hx * 0.02f + hy * hy * 0.25f < 3.0f)
                return new Color(0.32f + n, 0.22f + n, 0.30f + n);
            // Default wood panel, slightly darker than top floor
            return new Color(0.40f + n, 0.28f + n, 0.18f + n);
        }

        // ── Root divider (80..88) ───────────────────────────────────────────
        if (y < 88)
        {
            float knot = MathF.Cos(x * 0.10f) * 0.04f;
            return new Color(0.26f + knot + n, 0.16f + n, 0.09f + n);
        }

        // ── Bottom floor — caves & pond (88..H) ─────────────────────────────
        // Central pond roughly x ∈ [0.35W, 0.65W], y ∈ [108, H]
        bool inPond = x > W * 0.35f && x < W * 0.65f && y > 108;
        if (inPond)
        {
            // Water ripple shading
            float ripple = MathF.Sin(x * 0.15f + y * 0.4f) * 0.04f;
            return new Color(0.10f + ripple + n,
                             0.25f + ripple + n,
                             0.32f + ripple + n);
        }
        // Cave stone walls
        float stone = ((x * 7 + y * 13) % 9 == 0) ? 0.05f : 0f;
        return new Color(0.10f + stone + n,
                         0.13f + stone + n,
                         0.12f + stone + n);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Floor plates — slim visible slabs under each walkable level so the
    // player can see where norns can stand. Semi-transparent so the painted
    // floors in the backdrop still read through.
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildFloorPlates()
    {
        BuildOneFloorPlate(TopFloorY,    new Color(0.70f, 0.55f, 0.35f, 0.35f));
        BuildOneFloorPlate(MidFloorY,    new Color(0.55f, 0.40f, 0.28f, 0.35f));
        BuildOneFloorPlate(BottomFloorY, new Color(0.25f, 0.35f, 0.25f, 0.35f));
    }

    private void BuildOneFloorPlate(float y, Color tint)
    {
        var slab = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(RoomWidth - 2.0f, 0.12f, 1.5f) },
            Position = new Vector3(0, y - 0.06f, 0),
        };
        slab.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor  = tint,
            Transparency = tint.A < 1.0f
                ? BaseMaterial3D.TransparencyEnum.Alpha
                : BaseMaterial3D.TransparencyEnum.Disabled,
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        AddChild(slab);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void SetupLighting()
    {
        // Soft ambient so the backdrop reads without washing out lit agents.
        // `global::Godot.Environment` disambiguates from other Environment types.
        var env = new WorldEnvironment();
        env.Environment = new global::Godot.Environment
        {
            BackgroundMode     = global::Godot.Environment.BGMode.Color,
            BackgroundColor    = new Color(0.06f, 0.05f, 0.10f),
            AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
            AmbientLightColor  = new Color(0.85f, 0.80f, 0.72f),
            AmbientLightEnergy = 0.55f,
        };
        AddChild(env);

        var sun = new DirectionalLight3D
        {
            LightColor  = new Color(1.0f, 0.92f, 0.78f),
            LightEnergy = 0.7f,
        };
        sun.RotationDegrees = new Vector3(-45, -25, 0);
        AddChild(sun);
    }
}
