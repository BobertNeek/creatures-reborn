using System;
using Godot;

namespace CreaturesReborn.Godot;

/// <summary>
/// Dev-only one-shot screenshot helper. If launched with the command-line
/// argument <c>--screenshot=PATH</c>, waits a configurable number of seconds
/// for the scene to settle (so lights + materials stabilise, procedural
/// backdrops finish building), grabs the main viewport, writes a PNG to
/// <c>PATH</c>, and quits.
///
/// Used to iterate on floor/stair alignment against the painted backdrop
/// without the user having to manually re-launch and screenshot each time.
/// Inert if the flag is absent, so it's safe to leave autoloaded.
/// </summary>
public partial class DebugScreenshot : Node
{
    private string? _outPath;
    private float   _elapsed;
    private const float SettleTime = 2.0f; // seconds before capture

    public override void _Ready()
    {
        foreach (string arg in OS.GetCmdlineArgs())
        {
            if (arg.StartsWith("--screenshot="))
            {
                _outPath = arg.Substring("--screenshot=".Length);
                GD.Print($"[DebugScreenshot] Will capture to {_outPath} in {SettleTime}s");
                SetProcess(true);
                return;
            }
        }
        // No flag → disable ourselves entirely.
        SetProcess(false);
        QueueFree();
    }

    public override void _Process(double delta)
    {
        if (_outPath == null) return;
        _elapsed += (float)delta;
        if (_elapsed < SettleTime) return;

        Image img = CaptureImage(out bool usedFallback);
        Error err = img.SavePng(_outPath);
        string mode = usedFallback ? "headless fallback" : "viewport";
        GD.Print($"[DebugScreenshot] Saved PNG {mode} to {_outPath} (err={err})");
        GetTree().Quit();
    }

    private Image CaptureImage(out bool usedFallback)
    {
        if (IsHeadlessDisplay())
        {
            usedFallback = true;
            return CreateFallbackImage();
        }

        var viewport = GetViewport();
        var texture = viewport.GetTexture();
        var image = texture.GetImage();
        if (image != null)
        {
            usedFallback = false;
            return image;
        }

        usedFallback = true;
        return CreateFallbackImage();
    }

    private static bool IsHeadlessDisplay()
        => string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase);

    private static Image CreateFallbackImage()
    {
        var fallback = Image.CreateEmpty(96, 54, false, Image.Format.Rgba8);
        fallback.Fill(new Color(0.06f, 0.08f, 0.09f, 1.0f));
        for (int y = 0; y < 54; y++)
        {
            for (int x = 0; x < 96; x++)
            {
                if ((x + y) % 17 == 0)
                    fallback.SetPixel(x, y, new Color(0.10f, 0.45f, 0.42f, 1.0f));
            }
        }

        return fallback;
    }
}
