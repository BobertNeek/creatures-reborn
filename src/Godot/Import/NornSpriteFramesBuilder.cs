using System;
using Godot;

namespace CreaturesReborn.Godot.Import;

/// <summary>
/// Builds Godot <see cref="SpriteFrames"/> resources from C3/DS .c16 sprite files.
///
/// C3/DS frame layout (per body part, from SkeletalCreature.cpp):
///   frame = basepose + (posedirection * 4)
///   posedirection 0 = East  → frames  0.. 3
///   posedirection 1 = West  → frames  4.. 7
///   posedirection 2 = South → frames  8..11   (toward camera; used for rest)
///   posedirection 3 = North → frames 12..15
///
/// A walking cycle uses all 4 poses within a direction; idle uses pose 0 only.
///
/// Animation names returned by <see cref="BuildForBodyPart"/>:
///   "idle_right", "walk_east", "idle_left", "walk_west", "rest"
/// </summary>
public static class NornSpriteFramesBuilder
{
    // Frame ranges per posedirection (C3 convention)
    private const int FramesPerDir = 4;
    private const int DirEast      = 0;   // frames  0-3
    private const int DirWest      = 1;   // frames  4-7
    private const int DirSouth     = 2;   // frames  8-11

    public const string IdleRight = "idle_right";
    public const string WalkEast  = "walk_east";
    public const string IdleLeft  = "idle_left";
    public const string WalkWest  = "walk_west";
    public const string Rest      = "rest";

    // -------------------------------------------------------------------------
    /// <summary>
    /// Builds a <see cref="SpriteFrames"/> resource from a decoded C16 sprite.
    ///
    /// <paramref name="framePixelSize"/> is the Godot world-unit size of one pixel,
    /// which should match the <c>PixelSize</c> property on the target
    /// <see cref="AnimatedSprite3D"/>.
    /// </summary>
    public static SpriteFrames BuildForBodyPart(Image[] frames, float fps = 8.0f)
    {
        if (frames == null || frames.Length == 0)
            return BuildFallback();

        var sf = new SpriteFrames();

        // Remove the default "default" animation Godot creates
        sf.RemoveAnimation("default");

        int total = frames.Length;

        // Clamp to available frames
        int eastStart  = DirEast  * FramesPerDir;                           // 0
        int westStart  = DirWest  * FramesPerDir;                           // 4
        int southStart = DirSouth * FramesPerDir;                           // 8

        int eastCount  = Math.Clamp(FramesPerDir, 0, Math.Max(0, total - eastStart));
        int westCount  = Math.Clamp(FramesPerDir, 0, Math.Max(0, total - westStart));
        int southCount = Math.Clamp(FramesPerDir, 0, Math.Max(0, total - southStart));

        // If the sprite has no left-facing frames, mark that so the caller
        // can set FlipH instead
        bool hasWestFrames = westStart < total;

        // ── idle_right ────────────────────────────────────────────────────────
        sf.AddAnimation(IdleRight);
        sf.SetAnimationSpeed(IdleRight, 4.0f);
        sf.SetAnimationLoop(IdleRight, true);
        if (eastCount > 0)
            sf.AddFrame(IdleRight, ImageTexture.CreateFromImage(frames[eastStart]));
        else
            sf.AddFrame(IdleRight, ImageTexture.CreateFromImage(frames[0]));

        // ── walk_east ─────────────────────────────────────────────────────────
        sf.AddAnimation(WalkEast);
        sf.SetAnimationSpeed(WalkEast, fps);
        sf.SetAnimationLoop(WalkEast, true);
        for (int i = 0; i < Math.Max(1, eastCount); i++)
            sf.AddFrame(WalkEast, ImageTexture.CreateFromImage(
                frames[Math.Min(eastStart + i, total - 1)]));

        // ── idle_left ─────────────────────────────────────────────────────────
        sf.AddAnimation(IdleLeft);
        sf.SetAnimationSpeed(IdleLeft, 4.0f);
        sf.SetAnimationLoop(IdleLeft, true);
        if (hasWestFrames && westCount > 0)
            sf.AddFrame(IdleLeft, ImageTexture.CreateFromImage(frames[westStart]));
        else
            sf.AddFrame(IdleLeft, ImageTexture.CreateFromImage(frames[eastStart])); // fallback (caller flips)

        // ── walk_west ─────────────────────────────────────────────────────────
        sf.AddAnimation(WalkWest);
        sf.SetAnimationSpeed(WalkWest, fps);
        sf.SetAnimationLoop(WalkWest, true);
        for (int i = 0; i < Math.Max(1, hasWestFrames ? westCount : eastCount); i++)
        {
            int idx = hasWestFrames
                ? Math.Min(westStart + i, total - 1)
                : Math.Min(eastStart + i, total - 1);
            sf.AddFrame(WalkWest, ImageTexture.CreateFromImage(frames[idx]));
        }

        // ── rest ──────────────────────────────────────────────────────────────
        sf.AddAnimation(Rest);
        sf.SetAnimationSpeed(Rest, 2.0f);
        sf.SetAnimationLoop(Rest, true);
        // Use south-facing frame if available, otherwise fall back to idle
        int restFrame = southCount > 0 ? southStart : eastStart;
        sf.AddFrame(Rest, ImageTexture.CreateFromImage(frames[Math.Min(restFrame, total - 1)]));
        if (southCount > 1)
            sf.AddFrame(Rest, ImageTexture.CreateFromImage(frames[Math.Min(restFrame + 1, total - 1)]));

        return sf;
    }

    /// <summary>
    /// Convenience: load from a .c16 file path, decode, and build SpriteFrames.
    /// Returns null if the file cannot be decoded.
    /// </summary>
    public static SpriteFrames? FromFile(string c16Path, float fps = 8.0f)
    {
        var frames = C16Decoder.DecodeFile(c16Path);
        if (frames.Length == 0)
        {
            GD.PrintErr($"[NornSpriteFramesBuilder] No frames decoded from {c16Path}");
            return null;
        }
        GD.Print($"[NornSpriteFramesBuilder] Decoded {frames.Length} frames from {c16Path} ({frames[0].GetWidth()}×{frames[0].GetHeight()})");
        return BuildForBodyPart(frames, fps);
    }

    // -------------------------------------------------------------------------
    private static SpriteFrames BuildFallback()
    {
        var sf = new SpriteFrames();
        sf.RemoveAnimation("default");
        foreach (string anim in new[] { IdleRight, WalkEast, IdleLeft, WalkWest, Rest })
        {
            sf.AddAnimation(anim);
            sf.SetAnimationLoop(anim, true);
        }
        return sf;
    }
}
