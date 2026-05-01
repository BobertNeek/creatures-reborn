using System;
using Godot;
using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Lab;
using CreaturesReborn.Sim.Save;
using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Godot.Agents;

/// <summary>
/// A laid egg sitting in the world.
///
/// Spawned by CreatureNode.LayEggWith when two norns mate. Holds a path to
/// the freshly-crossed child genome on disk (written to user://). Sits for
/// HatchTime seconds, modulating cold-blue → warm-orange and wobbling
/// harder as the hatch nears, then instantiates a Norn.tscn at its position
/// with that genome path. The egg node frees itself.
///
/// Rendering: a Sprite3D (billboard) consistent with the game's 2.5D style,
/// not a 3D sphere. If <c>res://art/agents/egg.png</c> exists it's used;
/// otherwise we paint a small oval egg-shape with speckles procedurally —
/// identical fallback pattern to TreehouseMetaroomNode.BuildBackdrop.
/// </summary>
[GlobalClass]
public partial class EggNode : Node3D, IHandCarryable
{
    [Export] public float  HatchTime  = 12.0f;                          // seconds before hatch
    [Export] public string GenomePath = "";                             // path on disk; blank → starter
    [Export] public int    Sex        = GeneConstants.MALE;              // expressed sex for hatched creature
    [Export] public int    Variant    = 0;                               // expressed variant for hatched creature
    [Export] public string EggSpritePath = "res://art/agents/egg.png";  // override sprite
    [Export] public float  SpriteSize = 0.55f;                          // world-units tall

    public AgentArchetype AgentArchetype => AgentCatalog.Egg;
    public AgentClassifier Classifier => AgentArchetype.Classifier;
    public int ObjectCategory => AgentArchetype.ObjectCategory;
    public bool CanBeCarriedByHand => !_hatched && !IsStillborn;
    public Node3D CarryNode => this;
    public bool IsStillborn { get; private set; }
    public string StillbornReport { get; private set; } = "";

    private float _age;
    private bool  _hatched;
    private float _pulsePhase;

    private Sprite3D?    _sprite;
    private OmniLight3D? _glow;

    public override void _Ready()
    {
        BuildVisual();
        if (IsStillborn)
            ApplyStillbornVisual();
    }

    public override void _Process(double delta)
    {
        if (_hatched) return;

        float dt = (float)delta;
        _age        += dt;
        _pulsePhase += dt;

        float t = Math.Min(_age / HatchTime, 1.0f);

        // Cold blue → warm orange via sprite modulate. Modulate multiplies
        // with the texture's own colour so the speckle pattern stays visible
        // through the tint shift.
        if (_sprite != null)
        {
            _sprite.Modulate = new Color(
                0.70f + t * 0.30f,
                0.80f + t * 0.15f,
                1.00f - t * 0.45f);

            // Wobble harder as hatch nears — rotate around Z
            float wobble = t * MathF.Sin(_pulsePhase * 9f) * 0.18f;
            _sprite.Rotation = new Vector3(0, 0, wobble);
        }

        if (_glow != null)
            _glow.LightEnergy = 0.15f + t * 0.7f;

        if (_age >= HatchTime) Hatch();
    }

    public void PickUp(Node3D holder)
    {
    }

    public void Drop(Vector3 worldPos)
    {
        Position = worldPos;
    }

    private void Hatch()
    {
        if (_hatched)
            return;

        string resolvedGenomePath = ResolveGenomePath();
        byte[] genomeBytes;
        try
        {
            genomeBytes = System.IO.File.ReadAllBytes(resolvedGenomePath);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Egg] Hatch refused - genome could not be read: {resolvedGenomePath}. {ex.Message}");
            MarkStillborn($"Stillborn: unreadable genome\n{ex.Message}");
            return;
        }

        string childMoniker = System.IO.Path.GetFileNameWithoutExtension(resolvedGenomePath);
        HatchResult result = CreatureHatchService.AttemptHatch(
            new EggGenomePayload(genomeBytes, Sex, Variant, childMoniker),
            new HatchAttemptContext(childMoniker, MotherMoniker: null, FatherMoniker: null, BirthTick: 0, Generation: 0),
            new StatefulRng((int)GD.Randi()));

        if (result.Stillborn != null)
        {
            MarkStillborn(SimulationReportFormatters.FormatStillbornReport(result.Stillborn));
            return;
        }

        if (result.Creature == null)
        {
            MarkStillborn(SimulationReportFormatters.FormatSafetyReport(result.SafetyReport));
            return;
        }

        _hatched = true;
        var nornScene = GD.Load<PackedScene>("res://scenes/Norn.tscn");
        if (nornScene != null && GetParent() != null)
        {
            var norn = (CreatureNode)nornScene.Instantiate();
            norn.InitializeFromHatch(result.Creature, resolvedGenomePath, Sex, age: 0, Variant, childMoniker);
            // Spawn at egg position, slight nudge so it doesn't intersect siblings
            norn.Position = Position + new Vector3(0.3f, 0, 0);
            GetParent()!.AddChild(norn);
            GD.Print($"[Egg] Hatched! Genome={resolvedGenomePath}");
        }
        QueueFree();
    }

    public SavedEggState CreateSavedState()
    {
        byte[] genomeBytes = Array.Empty<byte>();
        string localPath = ProjectSettings.GlobalizePath(GenomePath);
        if (!string.IsNullOrWhiteSpace(localPath) && System.IO.File.Exists(localPath))
            genomeBytes = System.IO.File.ReadAllBytes(localPath);

        return new SavedEggState
        {
            GenomePath = GenomePath,
            GenomeBytes = genomeBytes,
            Sex = Sex,
            Variant = Variant,
            IsStillborn = IsStillborn,
            StillbornReport = StillbornReport,
            Age = _age,
            HatchTime = HatchTime,
            X = Position.X,
            Y = Position.Y,
            Z = Position.Z,
        };
    }

    public void RestoreFromSavedState(SavedEggState state)
    {
        GenomePath = state.GenomePath;
        if (state.GenomeBytes.Length > 0)
        {
            string restoredPath = System.IO.Path.Combine(
                ProjectSettings.GlobalizePath("user://"),
                $"restored_egg_{(ulong)Time.GetTicksMsec()}.gen");
            System.IO.File.WriteAllBytes(restoredPath, state.GenomeBytes);
            GenomePath = restoredPath;
        }

        Sex = state.Sex;
        Variant = state.Variant;
        IsStillborn = state.IsStillborn;
        StillbornReport = state.StillbornReport;
        _hatched = IsStillborn;
        _age = state.Age;
        HatchTime = state.HatchTime;
        Position = new Vector3(state.X, state.Y, state.Z);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void BuildVisual()
    {
        _sprite = new Sprite3D
        {
            Texture       = LoadOrPaintEggTexture(),
            Billboard     = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize     = 1.0f / 64.0f * SpriteSize,   // texture is 64 px tall ~> SpriteSize world units
            Shaded        = false,
            Position      = new Vector3(0, SpriteSize * 0.5f, 0),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
        };
        AddChild(_sprite);

        _glow = new OmniLight3D
        {
            LightColor  = new Color(1.0f, 0.7f, 0.4f),
            LightEnergy = 0.15f,
            OmniRange   = 1.5f,
            Position    = new Vector3(0, SpriteSize * 0.5f, 0),
        };
        AddChild(_glow);
    }

    private string ResolveGenomePath()
    {
        string path = string.IsNullOrWhiteSpace(GenomePath)
            ? "res://data/genomes/starter.gen"
            : GenomePath;
        return path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("user://", StringComparison.OrdinalIgnoreCase)
            ? ProjectSettings.GlobalizePath(path)
            : path;
    }

    private void MarkStillborn(string report)
    {
        IsStillborn = true;
        StillbornReport = report;
        _hatched = true;
        ApplyStillbornVisual();
        GD.PrintErr($"[Egg] Stillborn hatch outcome.\n{StillbornReport}");
    }

    private void ApplyStillbornVisual()
    {
        if (_sprite != null)
        {
            _sprite.Modulate = new Color(0.35f, 0.32f, 0.30f);
            _sprite.Rotation = Vector3.Zero;
        }

        if (_glow != null)
        {
            _glow.LightColor = new Color(0.55f, 0.12f, 0.08f);
            _glow.LightEnergy = 0.08f;
        }
    }

    /// <summary>
    /// 3-step fallback identical to the metaroom backdrop loader:
    ///   1. ResourceLoader on res:// (needs Godot .import sidecar)
    ///   2. Raw Image.LoadFromFile on the globalised path (works pre-import)
    ///   3. Painted placeholder texture
    /// </summary>
    private Texture2D LoadOrPaintEggTexture()
    {
        if (ResourceLoader.Exists(EggSpritePath))
        {
            var t = GD.Load<Texture2D>(EggSpritePath);
            if (t != null) return t;
        }

        string absPath = ProjectSettings.GlobalizePath(EggSpritePath);
        if (System.IO.File.Exists(absPath))
        {
            var img = Image.LoadFromFile(absPath);
            if (img != null) return ImageTexture.CreateFromImage(img);
        }

        return PaintEggTexture();
    }

    /// <summary>
    /// Procedural egg sprite: 48×64 image, RGBA8, background transparent,
    /// oval egg shape with a soft highlight and random speckles. Deterministic
    /// (fixed seed) so every un-overridden egg looks identical.
    /// </summary>
    private static ImageTexture PaintEggTexture()
    {
        const int W = 48, H = 64;
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        var transparent = new Color(0, 0, 0, 0);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                img.SetPixel(x, y, transparent);

        // Egg shape: asymmetric ellipse — narrow top, fat bottom.
        // Represented as: ((x-cx)/rx)^2 + ((y-cy)/ry)^2 <= 1, but with ry
        // varying with y position (narrower at top).
        float cx = W * 0.5f;
        float cy = H * 0.55f;           // geometric centre biased down
        float rxMax = 16f;
        float ryTop = 24f, ryBot = 28f;

        // Base egg colour (painted, then modulate will tint it in flight)
        var shell    = new Color(0.95f, 0.92f, 0.82f);  // creamy
        var shadow   = new Color(0.68f, 0.62f, 0.52f);  // underside
        var highlite = new Color(1.00f, 1.00f, 0.95f);  // top-left sheen
        var speckle  = new Color(0.45f, 0.35f, 0.25f);  // brown spots

        for (int y = 0; y < H; y++)
        {
            // Narrower at top, wider at bottom
            float yt      = (y - cy);
            float ryHere  = yt < 0 ? ryTop : ryBot;
            float normY   = yt / ryHere;
            if (normY < -1 || normY > 1) continue;
            float halfXAtY = rxMax * MathF.Sqrt(1 - normY * normY);
            int xLo = (int)MathF.Floor(cx - halfXAtY);
            int xHi = (int)MathF.Floor(cx + halfXAtY + 0.999f);

            for (int x = Math.Max(0, xLo); x <= Math.Min(W - 1, xHi); x++)
            {
                // Shading: top-left highlighted, bottom-right shadowed
                float nx = (x - cx) / rxMax;
                float ny = normY;
                // dot with light direction (-0.6, -0.8) normalised
                float lit = -0.6f * nx + -0.8f * ny;
                lit = Mathf.Clamp(lit * 0.5f + 0.5f, 0f, 1f);

                Color c = shadow.Lerp(shell, lit);
                // A sharper specular glint top-left
                if (lit > 0.88f) c = c.Lerp(highlite, (lit - 0.88f) / 0.12f);
                img.SetPixel(x, y, c);
            }
        }

        // Brown speckles — deterministic spatter
        var rng = new Random(0xE665);
        int specklesTarget = 26;
        int placed = 0, attempts = 0;
        while (placed < specklesTarget && attempts++ < 400)
        {
            int sx = rng.Next(W);
            int sy = rng.Next(H);
            if (img.GetPixel(sx, sy).A < 0.5f) continue;
            // mini cluster
            int size = 1 + rng.Next(2);
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                {
                    int px = sx + dx, py = sy + dy;
                    if (px < 0 || px >= W || py < 0 || py >= H) continue;
                    if (img.GetPixel(px, py).A < 0.5f) continue;
                    Color blended = img.GetPixel(px, py).Lerp(speckle, 0.75f);
                    img.SetPixel(px, py, blended);
                }
            placed++;
        }

        return ImageTexture.CreateFromImage(img);
    }
}
