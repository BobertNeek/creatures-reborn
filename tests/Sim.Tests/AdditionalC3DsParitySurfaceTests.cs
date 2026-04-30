using System;
using System.IO;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class C3DsFixtureDiscoveryTests
{
    [Fact]
    public void Discover_MissingFixtureRootReturnsEmptySet()
    {
        C3DsFixtureSet fixtureSet = C3DsFixtureSet.Discover(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"));

        Assert.Empty(fixtureSet.Genomes);
    }
}

public sealed class StimulusGeneExpressionTests
{
    [Fact]
    public void GenomeStimulusExpression_RecordsSourceGeneInApplicationTrace()
    {
        int mappedChemical = GenePayloadCodec.StimulusChemicalToBiochemical(0);
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Gene(
                (int)GeneType.CREATUREGENE,
                (int)CreatureSubtype.G_STIMULUS,
                id: 91,
                payload: [77, 255, 0, 0, 0, 0, 255, 255, 0, 255, 0, 255, 0]));
        var creature = Creature.Creature.CreateFromGenome(
            genome,
            new Rng(61),
            new CreatureImportOptions(BiochemistryMode: BiochemistryCompatibilityMode.C3DS));

        StimulusApplicationTrace trace = StimulusTable.Apply(creature, 77);

        Assert.True(trace.UsedGenomeAuthoredDefinition);
        Assert.Equal(91, trace.SourceGene?.Id);
        Assert.Equal(1.0f, creature.GetChemical(mappedChemical), precision: 6);
    }
}

public sealed class OrganLifecycleParityTests
{
    [Fact]
    public void OrganParitySnapshot_ExposesAtpDamageAndLifeForceLoci()
    {
        var biochemistry = new Biochemistry.Biochemistry(BiochemistryCompatibilityMode.C3DS);
        var organ = new Organ();
        organ.SetOwner(biochemistry);
        organ.Init(clockRate: 1.0f, rateOfRepair: 0.0f, lifeForce: 1.0f, initialClock: 0.0f, damageDueToZeroEnergy: 128);
        float before = organ.CreateSnapshot(0).ShortTermLifeForce;

        organ.Update();
        organ.Update();
        OrganParitySnapshot snapshot = new(
            organ.CreateSnapshot(0),
            organ.CreateReactionParitySnapshots(),
            organ.CreateReceptorParitySnapshots(),
            organ.CreateEmitterParitySnapshots());

        Assert.True(snapshot.Snapshot.DamageDueToZeroEnergy > 0.0f);
        Assert.True(snapshot.Snapshot.ShortTermLifeForce < before);
    }
}

public sealed class NeuroEmitterParityTests
{
    [Fact]
    public void NeuroEmitterParitySnapshot_ExposesChemicalEmissions()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.GeneWithSwitch(
                (int)GeneType.BIOCHEMISTRYGENE,
                (int)BiochemSubtype.G_NEUROEMITTER,
                id: 92,
                switchOnAge: 2,
                payload: [0, 0, 0, 0, 0, 0, 255, (byte)ChemID.Fear, 128, 0, 0, 0, 0, 0, 0]));
        var creature = Creature.Creature.CreateFromGenome(genome, new Rng(62), new CreatureImportOptions(BiochemistryMode: BiochemistryCompatibilityMode.C3DS));

        creature.ApplyGeneExpressionStage(2);
        NeuroEmitterParitySnapshot snapshot = Assert.Single(creature.Biochemistry.CreateParityTrace().NeuroEmitters);

        Assert.Equal(1.0f, snapshot.BioTickRate, precision: 6);
        Assert.Contains(snapshot.Emissions, emission => emission.ChemicalId == ChemID.Fear && emission.Amount > 0.0f);
    }
}

public sealed class BrainBootParityTests
{
    [Fact]
    public void BrainBootParitySnapshot_ExposesLobeGeometryAndTractEndpoints()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ());
        var creature = Creature.Creature.CreateFromGenome(genome, new Rng(63));

        BrainBootParitySnapshot boot = creature.Brain.CreateParityTrace().Boot;

        Assert.Contains(boot.Lobes, lobe => lobe.TokenText == "driv" && lobe.Width == 4 && lobe.Height == 4);
        Assert.Contains(boot.Tracts, tract => tract.SourceTokenText == "driv" && tract.DestinationTokenText == "decn");
    }
}

public sealed class BrainSvRuleParityTests
{
    [Fact]
    public void SVRuleOpcodeInventory_DoesNotExposeSentinelValuesAsExecutableCases()
    {
        SVRuleParityCase inventory = SVRuleParityCase.CreateOpcodeInventory();

        Assert.DoesNotContain(nameof(SVRule.Op.NumOpCodes), inventory.Opcodes);
        Assert.DoesNotContain(nameof(SVRule.Operand.NumOperands), inventory.Operands);
    }
}
