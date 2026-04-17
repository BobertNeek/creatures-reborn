using System;
using Godot;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Godot;

/// <summary>
/// The Treehouse metaroom — a magical multi-floor tree dwelling.
///
/// Based on the Gemini-generated backdrop (art/metaroom/treehouse_backdrop.png),
/// the image shows three distinct walkable floors separated by gnarled root
/// dividers:
///
///   Floor 0 (Y = TopFloorY    = 9.0): Observatory / Library — the sunlit
///       upper level with a domed telescope platform in the centre,
///       fireplace-library on the left, and map-study on the right.
///   Floor 1 (Y = MidFloorY    = 4.5): Living / Lab — hammock bedroom,
///       reading nook, and alchemy lab with crystal wall.
///   Floor 2 (Y = BottomFloorY = 0.0): Caves / Pond — glowing crystal caves
///       on the sides, central mushroom cavern, pond with fish in the middle.
///
/// Two elevator shafts (x = -8 and x = +6) connect all three floors along
/// the natural tree-trunk columns visible in the art.
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
    [Export] public string BackdropPath = "res://art/metaroom/treehouse_backdrop.png";

    // ── Floor Y values (match image layout) ──────────────────────────────────
    public const float TopFloorY    = 9.0f;
    public const float MidFloorY    = 4.5f;
    public const float BottomFloorY = 0.0f;

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
        BuildFloorPlates();
        SetupLighting();
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
        // Try the assigned texture first, then the conventional path, then fallback
        var tex = Backdrop ?? GD.Load<Texture2D>(BackdropPath);
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
            GD.PrintErr($"[TreehouseMetaroom] Backdrop not found at {BackdropPath} — using placeholder.");
            mat = BuildPlaceholderMaterial();
        }

        var backdrop = new MeshInstance3D
        {
            Name = "Backdrop",
            Mesh = new QuadMesh { Size = new Vector2(RoomWidth, RoomHeight) },
            Position = new Vector3(0, (TopFloorY + BottomFloorY) * 0.5f + 2.0f, -5.0f),
        };
        backdrop.MaterialOverride = mat;
        AddChild(backdrop);
    }

    /// <summary>Solid three-band placeholder if the PNG is missing.</summary>
    private StandardMaterial3D BuildPlaceholderMaterial()
    {
        // A simple procedural "three bands" gradient baked at build time.
        var img = Image.CreateEmpty(64, 192, false, Image.Format.Rgb8);
        for (int y = 0; y < 192; y++)
        {
            Color c = y < 64  ? new Color(0.20f, 0.28f, 0.45f)   // top: night-sky blue
                   : y < 128 ? new Color(0.45f, 0.30f, 0.20f)    // middle: warm wood
                             : new Color(0.10f, 0.18f, 0.14f);   // bottom: cave green
            for (int x = 0; x < 64; x++) img.SetPixel(x, y, c);
        }
        return new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(img),
            ShadingMode   = BaseMaterial3D.ShadingModeEnum.Unshaded,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
        };
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
