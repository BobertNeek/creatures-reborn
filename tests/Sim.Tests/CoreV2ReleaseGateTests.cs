using System;
using System.IO;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Util;
using Xunit;
using C = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Sim.Tests;

public class CoreV2ReleaseGateTests
{
    private static string RepoPath(params string[] parts)
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            Path.Combine(parts)));

    [Fact]
    public void CoreV2ImplementationNotes_CoverMajorSubsystems()
    {
        string notes = File.ReadAllText(RepoPath(
            "docs",
            "superpowers",
            "specs",
            "2026-04-26-core-v2-implementation-notes.md"));

        Assert.Contains("Genome v2", notes);
        Assert.Contains("Biochemistry Trace", notes);
        Assert.Contains("Brain Snapshot", notes);
        Assert.Contains("Creature Trace", notes);
        Assert.Contains("CA Fields", notes);
        Assert.Contains("Agent Affordances", notes);
        Assert.Contains("Lab Runner", notes);
        Assert.Contains("Evolution Hooks", notes);
        Assert.Contains("Authoring", notes);
        Assert.Contains("Release Gate", notes);
    }

    [Fact]
    public void CoreV2ReleaseGateScript_RunsExpectedVerificationCommands()
    {
        string script = File.ReadAllText(RepoPath("tools", "run_core_v2_release_gate.ps1"));

        Assert.Contains("dotnet test tests\\Sim.Tests\\Sim.Tests.csproj", script);
        Assert.Contains("dotnet build CreaturesReborn.csproj", script);
        Assert.Contains("rg -n \"TODO|TBD|placeholder\"", script);
        Assert.Contains("tools\\run_godot_smoke.ps1", script);
    }

    [Fact]
    public void MultiCreatureTraceReview_CoversBiochemistryBrainAndLearning()
    {
        string starterGenome = RepoPath("data", "genomes", "starter.gen");
        var first = C.LoadFromFile(starterGenome, new Rng(160), moniker: "trace-a");
        var second = C.LoadFromFile(starterGenome, new Rng(161), moniker: "trace-b");
        var options = new CreatureTraceOptions(
            IncludeBiochemistryTrace: true,
            IncludeBrainSnapshot: true,
            IncludeLearningTrace: true);

        CreatureTickTrace firstTrace = first.Tick(options);
        CreatureTickTrace secondTrace = second.Tick(options);

        Assert.Equal(9, firstTrace.Stages.Count);
        Assert.Equal(9, secondTrace.Stages.Count);
        Assert.NotNull(firstTrace.Biochemistry);
        Assert.NotNull(secondTrace.Biochemistry);
        Assert.NotNull(firstTrace.BrainBefore);
        Assert.NotNull(firstTrace.BrainAfter);
        Assert.NotNull(secondTrace.BrainBefore);
        Assert.NotNull(secondTrace.BrainAfter);
        Assert.NotNull(firstTrace.Learning);
        Assert.NotNull(secondTrace.Learning);
        Assert.True(firstTrace.BrainAfter.Lobes.Count > 0);
        Assert.True(secondTrace.BrainAfter.Tracts.Count > 0);
    }
}
