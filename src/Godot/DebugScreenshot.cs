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

        var viewport = GetViewport();
        var img = viewport.GetTexture().GetImage();
        if (img != null)
        {
            Error err = img.SavePng(_outPath);
            GD.Print($"[DebugScreenshot] Saved PNG to {_outPath} (err={err})");
        }
        else
        {
            GD.PrintErr("[DebugScreenshot] Viewport image was null");
        }
        GetTree().Quit();
    }
}
