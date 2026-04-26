using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using Xunit;
using B = CreaturesReborn.Sim.Brain.Brain;

namespace CreaturesReborn.Sim.Tests;

public class BrainDiagnosticModuleTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void DiagnosticBrainModule_IsNotRegisteredByDefault()
    {
        var (brain, _) = LoadBrain(seed: 110);

        Assert.DoesNotContain(brain.GetModuleDescriptors(), descriptor => descriptor.Name == nameof(DiagnosticBrainModule));
    }

    [Fact]
    public void DiagnosticBrainModule_CapturesPortSnapshotWhenRegistered()
    {
        var (brain, chemicals) = LoadBrain(seed: 111);
        var module = new DiagnosticBrainModule();
        brain.RegisterModule(module);
        chemicals[ChemID.Reward] = 0.75f;
        brain.SetInput(B.TokenFromString("driv"), DriveId.HungerForCarb, 1.0f);

        brain.Update();

        DiagnosticBrainModuleSnapshot snapshot = Assert.IsType<DiagnosticBrainModuleSnapshot>(module.LatestSnapshot);
        Assert.True(snapshot.Tick > 0);
        Assert.Contains(snapshot.Readings, reading => reading.Port.Name == "chemical:reward" && reading.Value == 0.75f);
        Assert.Contains(snapshot.Readings, reading => reading.Port.Name == "drive:2");
        Assert.Contains(snapshot.Readings, reading => reading.Port.Name == "motor:verb");
        Assert.Contains(brain.GetModuleDescriptors(), descriptor =>
            descriptor.Name == nameof(DiagnosticBrainModule) &&
            descriptor.IsPassive);
    }

    [Fact]
    public void DiagnosticBrainModule_DoesNotChangeBrainOutputs()
    {
        var (plain, plainChemicals) = LoadBrain(seed: 112);
        var (diagnostic, diagnosticChemicals) = LoadBrain(seed: 112);
        diagnostic.RegisterModule(new DiagnosticBrainModule());
        plainChemicals[ChemID.Reward] = 0.25f;
        diagnosticChemicals[ChemID.Reward] = 0.25f;

        for (int i = 0; i < 4; i++)
        {
            plain.SetInput(B.TokenFromString("driv"), DriveId.HungerForCarb, 1.0f);
            diagnostic.SetInput(B.TokenFromString("driv"), DriveId.HungerForCarb, 1.0f);
            plain.Update();
            diagnostic.Update();
        }

        Assert.Equal(
            plain.GetWinningId(B.TokenFromString("decn")),
            diagnostic.GetWinningId(B.TokenFromString("decn")));
    }

    private static (B Brain, float[] Chemicals) LoadBrain(int seed)
    {
        var genome = GenomeReader.LoadNew(new Rng(seed), Path.GetFullPath(StarterGenomePath));
        var brain = new B();
        brain.ReadFromGenome(genome, new Rng(seed));
        var chemicals = new float[BiochemConst.NUMCHEM];
        chemicals[ChemID.ATP] = 1.0f;
        brain.RegisterBiochemistry(chemicals);
        return (brain, chemicals);
    }
}
