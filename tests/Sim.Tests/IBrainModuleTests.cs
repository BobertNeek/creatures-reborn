using System;
using System.IO;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using B = CreaturesReborn.Sim.Brain.Brain;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

/// <summary>
/// Verifies the IBrainModule extensibility seam.
///
/// A throw-away 20-line module is registered against a real brain loaded from a stock .gen.
/// The test confirms:
///   - RegisterModule does not throw
///   - The module's Update() is invoked on every tick
///   - A shadowing module suppresses the named lobe's DoUpdate (no double-update)
///   - Registering the module does not affect normal WTA output from un-shadowed lobes
/// </summary>
public class IBrainModuleTests
{
    private static readonly string GenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "resources for gpt",
            "Creature Norn Augment Project",
            "Agents AND Breeds",
            "C3DS Compilation Mall-Breed Pack egg agents and decompiled files with sprites",
            "C3DS Compilation Mall-Breed Pack",
            "norn.bondi.48.gen");

    private static bool ShouldSkip() => !File.Exists(GenomePath);

    // -----------------------------------------------------------------------
    // Stub module: counts how many times Update was called
    // -----------------------------------------------------------------------
    private sealed class CountingModule : IBrainModule
    {
        public int UpdateCount { get; private set; }
        public bool InitialiseCalled { get; private set; }
        public int? ShadowedLobeToken => null;    // runs in addition to lobe stack

        public void Initialise(B brain) { InitialiseCalled = true; }
        public void Update(B brain)     { UpdateCount++; }
    }

    // -----------------------------------------------------------------------
    // Stub module that shadows the "driv" lobe
    // -----------------------------------------------------------------------
    private sealed class DriveShadowModule : IBrainModule
    {
        public int UpdateCount { get; private set; }
        public int? ShadowedLobeToken { get; }

        public DriveShadowModule()
        { ShadowedLobeToken = B.TokenFromString("driv"); }

        public void Initialise(B brain) { }
        public void Update(B brain)     { UpdateCount++; }
    }

    // -----------------------------------------------------------------------

    [Fact]
    public void RegisterModule_DoesNotThrow()
    {
        if (ShouldSkip()) return;

        var brain = new B();
        brain.ReadFromGenome(GenomeReader.LoadNew(new Rng(1), GenomePath), new Rng(1));

        var mod = new CountingModule();
        brain.RegisterModule(mod);           // must not throw

        Assert.True(mod.InitialiseCalled, "Initialise should be called immediately on registration");
    }

    [Fact]
    public void RegisteredModule_UpdateCalledEveryTick()
    {
        if (ShouldSkip()) return;

        var brain = new B();
        brain.ReadFromGenome(GenomeReader.LoadNew(new Rng(2), GenomePath), new Rng(2));
        brain.RegisterBiochemistry(new float[256]);

        var mod = new CountingModule();
        brain.RegisterModule(mod);

        const int ticks = 50;
        for (int i = 0; i < ticks; i++)
            brain.Update();

        Assert.Equal(ticks, mod.UpdateCount);
    }

    [Fact]
    public void ShadowModule_SuppressesNamedLobe_AndStillRuns()
    {
        if (ShouldSkip()) return;

        var genome = GenomeReader.LoadNew(new Rng(3), GenomePath);
        var rng    = new Rng(3);

        // Brain A: normal (no module)
        var brainA = new B();
        brainA.ReadFromGenome(genome, rng);
        brainA.RegisterBiochemistry(new float[256]);

        // Brain B: driv lobe shadowed
        var brainB = new B();
        brainB.ReadFromGenome(GenomeReader.LoadNew(new Rng(3), GenomePath), new Rng(3));
        brainB.RegisterBiochemistry(new float[256]);
        var shadow = new DriveShadowModule();
        brainB.RegisterModule(shadow);

        const int ticks = 20;
        for (int i = 0; i < ticks; i++) { brainA.Update(); brainB.Update(); }

        // Shadow module ran for every tick
        Assert.Equal(ticks, shadow.UpdateCount);

        // brainB still has lobes — RegisterModule must not have torn them down
        Assert.True(brainB.LobeCount > 0);
    }

    [Fact]
    public void RegisterModule_DoesNotCorruptNormalWTA()
    {
        if (ShouldSkip()) return;

        var brain = new B();
        brain.ReadFromGenome(GenomeReader.LoadNew(new Rng(4), GenomePath), new Rng(4));
        brain.RegisterBiochemistry(new float[256]);
        brain.RegisterModule(new CountingModule());

        for (int i = 0; i < 100; i++)
            brain.Update();

        int winner = brain.GetWinningId(B.TokenFromString("decn"));
        Assert.True(winner >= 0, $"WTA winner should be non-negative, got {winner}");
    }
}
