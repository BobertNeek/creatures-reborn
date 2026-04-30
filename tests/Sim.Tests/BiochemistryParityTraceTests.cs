using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class BiochemistryParityTraceTests
{
    [Fact]
    public void CreateParityTrace_ExposesC3DsChemicalsOrgansReceptorsEmittersReactionsAndNeuroEmitters()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP),
            C3DsBiologyParityTests.Receptor(ChemID.Injury, 3, 0),
            C3DsBiologyParityTests.Gene(
                (int)GeneType.BIOCHEMISTRYGENE,
                (int)BiochemSubtype.G_EMITTER,
                id: 84,
                payload: [1, (byte)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Const, (byte)ChemID.Reward, 0, 1, 128, 0]),
            C3DsBiologyParityTests.GeneWithSwitch(
                (int)GeneType.BIOCHEMISTRYGENE,
                (int)BiochemSubtype.G_NEUROEMITTER,
                id: 85,
                switchOnAge: 2,
                payload: [0, 0, 0, 0, 0, 0, 255, (byte)ChemID.Punishment, 64, 0, 0, 0, 0, 0, 0]));
        var creature = Creature.Creature.CreateFromGenome(
            genome,
            new Rng(41),
            new CreatureImportOptions(BiochemistryMode: BiochemistryCompatibilityMode.C3DS));
        creature.SetChemical(ChemID.ATP, 1.0f);
        creature.ApplyGeneExpressionStage(2);

        BiochemistryParityTrace trace = creature.Biochemistry.CreateParityTrace();

        Assert.Equal(BiochemistryCompatibilityMode.C3DS, trace.CompatibilityMode);
        Assert.Equal(BiochemConst.NUMCHEM, trace.ChemicalCount);
        Assert.True(trace.NonZeroChemicalCount > 0);
        OrganParitySnapshot organ = Assert.Single(trace.Organs.Where(organ => organ.Reactions.Count > 0));
        Assert.NotEmpty(organ.Reactions);
        Assert.NotEmpty(organ.Receptors);
        Assert.NotEmpty(organ.Emitters);
        Assert.Equal(ChemID.ATP, organ.Reactions[0].Reactant1);
        Assert.Equal(ChemID.Injury, organ.Receptors[0].ChemicalId);
        Assert.Equal(ChemID.Reward, organ.Emitters[0].ChemicalId);
        Assert.NotEmpty(trace.NeuroEmitters);
        Assert.Contains(trace.HalfLives, halfLife => halfLife.ChemicalId == ChemID.ATP);
    }
}
