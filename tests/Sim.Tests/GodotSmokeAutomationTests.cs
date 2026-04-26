using System;
using System.IO;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class GodotSmokeAutomationTests
{
    private static readonly string ScriptPath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tools", "run_godot_smoke.ps1");

    [Fact]
    public void SmokeScript_CoversTreehouseAndNornColonyWithScreenshots()
    {
        string script = File.ReadAllText(Path.GetFullPath(ScriptPath));

        Assert.Contains("Treehouse.tscn", script);
        Assert.Contains("NornColony.tscn", script);
        Assert.Contains("--headless", script);
        Assert.Contains("--screenshot=", script);
        Assert.Contains("Simulation world initialised", script);
        Assert.Contains("Saved PNG", script);
        Assert.Contains("Unexpected Godot asset warnings", script);
        Assert.DoesNotContain("texture_2d_get", script);
        Assert.DoesNotContain("Parameter `\"t`\" is null", script);
    }
}
