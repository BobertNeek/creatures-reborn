using System;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class BrainParityTraceTests
{
    [Fact]
    public void CreateParityTrace_ExposesBrainBootLobesTractsWinnersAndInstinctState()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ());
        var creature = Creature.Creature.CreateFromGenome(
            genome,
            new Rng(51),
            new CreatureImportOptions(BiochemistryMode: BiochemistryCompatibilityMode.C3DS));

        BrainParityTrace trace = creature.Brain.CreateParityTrace();

        Assert.True(trace.Boot.LobeCount >= 2);
        Assert.True(trace.Boot.TractCount >= 1);
        Assert.Contains(trace.Boot.Lobes, lobe => lobe.TokenText == "driv" && lobe.NeuronCount == 16);
        Assert.Contains(trace.Boot.Tracts, tract => tract.SourceTokenText == "driv" && tract.DestinationTokenText == "decn");
        Assert.NotNull(trace.Instincts);
        Assert.Empty(trace.ModuleNames);
    }

    [Fact]
    public void SVRuleParityCase_EnumeratesEveryOpcodeAndOperand()
    {
        SVRuleParityCase inventory = SVRuleParityCase.CreateOpcodeInventory();

        Assert.Equal((int)SVRule.Op.NumOpCodes, inventory.Opcodes.Count);
        Assert.Equal((int)SVRule.Operand.NumOperands, inventory.Operands.Count);
        Assert.Contains(nameof(SVRule.Op.DoWinnerTakesAll), inventory.Opcodes);
        Assert.Contains(nameof(SVRule.Op.SetRewardChemicalIndex), inventory.Opcodes);
        Assert.Contains(nameof(SVRule.Operand.ChemBySrc), inventory.Operands);
        Assert.Contains(nameof(SVRule.Operand.ChemByDst), inventory.Operands);
    }

    [Fact]
    public void BrainTrace_IsDeterministicAcrossOneAndTenTicks()
    {
        var genomeA = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ());
        var genomeB = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ());
        var a = Creature.Creature.CreateFromGenome(genomeA, new Rng(52), new CreatureImportOptions(BiochemistryMode: BiochemistryCompatibilityMode.C3DS));
        var b = Creature.Creature.CreateFromGenome(genomeB, new Rng(52), new CreatureImportOptions(BiochemistryMode: BiochemistryCompatibilityMode.C3DS));

        a.Tick();
        b.Tick();
        BrainParityTrace oneTickA = a.Brain.CreateParityTrace();
        BrainParityTrace oneTickB = b.Brain.CreateParityTrace();
        for (int i = 0; i < 9; i++)
        {
            a.Tick();
            b.Tick();
        }
        BrainParityTrace tenTickA = a.Brain.CreateParityTrace();
        BrainParityTrace tenTickB = b.Brain.CreateParityTrace();

        Assert.Equal(oneTickA.Boot.Lobes.Select(lobe => lobe.WinningNeuronId), oneTickB.Boot.Lobes.Select(lobe => lobe.WinningNeuronId));
        Assert.Equal(tenTickA.Boot.Lobes.Select(lobe => lobe.WinningNeuronId), tenTickB.Boot.Lobes.Select(lobe => lobe.WinningNeuronId));
    }
}
