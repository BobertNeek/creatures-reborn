using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Util;
using Xunit;
using C = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Sim.Tests;

public sealed class BrainMonitorAdapterTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void BrainMonitorFrame_ProjectsLobeGeometryAndSampledNeuronRows()
    {
        C creature = LoadCreature();
        BrainMonitorFrame frame = BrainMonitorFrame.Create(
            creature,
            new BrainMonitorOptions(MaxNeuronsPerLobe: 2, MaxDendritesPerTract: 1, MaxTableRows: 128));

        Assert.NotEmpty(frame.Lobes);
        Assert.NotEmpty(frame.Neurons);
        Assert.All(frame.Lobes, lobe =>
        {
            Assert.True(lobe.Width > 0);
            Assert.True(lobe.Height > 0);
            Assert.True(lobe.NeuronCount > 0);
            Assert.InRange(lobe.WinningNeuronId, 0, Math.Max(0, lobe.NeuronCount - 1));
            Assert.InRange(lobe.Activation, -1.0f, 1.0f);
        });
        Assert.All(frame.Lobes, lobe => Assert.True(frame.Neurons.Count(row => row.LobeIndex == lobe.Index) <= 2));
        Assert.Contains(frame.Lobes, lobe => lobe.X >= 0 && lobe.Y >= 0);
    }

    [Fact]
    public void BrainMonitorFrame_ProjectsTractsModulesPortsDrivesChemicalsAndMotorOutput()
    {
        C creature = LoadCreature();
        creature.SetChemical(ChemID.ATP, 0.75f);
        creature.SetChemical(ChemID.Reward, 0.25f);
        creature.AddDriveInput(DriveId.HungerForCarb, 0.6f);
        creature.Tick();

        BrainMonitorFrame frame = BrainMonitorFrame.Create(
            creature,
            new BrainMonitorOptions(
                MaxNeuronsPerLobe: 1,
                MaxDendritesPerTract: 2,
                WatchedChemicals: [ChemID.ATP, ChemID.Reward]));

        Assert.NotEmpty(frame.Tracts);
        Assert.All(frame.Tracts, tract => Assert.True(tract.DendriteCount >= tract.Dendrites.Count));
        Assert.Contains(frame.Ports, port => port.Name == "motor:verb");
        Assert.Contains(frame.Drives, drive => drive.Id == DriveId.HungerForCarb);
        Assert.Contains(frame.Chemicals, chemical => chemical.Id == ChemID.ATP && chemical.Value > 0);
        Assert.Equal(creature.Motor.CurrentVerb, frame.MotorVerb);
        Assert.Equal(creature.Motor.CurrentNoun, frame.MotorNoun);
        Assert.Equal(creature.Brain.GetModuleDescriptors().Count, frame.Modules.Count);
    }

    [Fact]
    public void BrainMonitorHistory_StoresRingBuffersForSelectedSeries()
    {
        C creature = LoadCreature();
        var history = new BrainMonitorHistory(capacity: 3);

        for (int i = 0; i < 5; i++)
        {
            creature.SetChemical(ChemID.Reward, i / 10f);
            history.Record(BrainMonitorFrame.Create(
                creature,
                new BrainMonitorOptions(MaxNeuronsPerLobe: 1, MaxDendritesPerTract: 1, WatchedChemicals: [ChemID.Reward])));
        }

        BrainMonitorSeries reward = Assert.Single(history.ChemicalSeries.Where(series => series.Id == ChemID.Reward));
        Assert.Equal(3, reward.Values.Count);
        Assert.Equal(0.2f, reward.Values[0], precision: 5);
        Assert.Equal(0.4f, reward.Values[2], precision: 5);
    }

    private static C LoadCreature()
        => C.LoadFromFile(Path.GetFullPath(StarterGenomePath), new Rng(70), moniker: "monitor");
}
