using CreaturesReborn.Sim.Biochemistry;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class OrganReceptorEmitterFidelityTests
{
    [Fact]
    public void Receptors_AnalogAndDigitalFlagsWriteExpectedLocusStrength()
    {
        var analogBiochemistry = new Biochemistry.Biochemistry(BiochemistryCompatibilityMode.C3DS);
        var analogOrgan = HostedOrgan(analogBiochemistry);
        analogOrgan.ApplyReceptorGene([1, (byte)CreatureTissue.Drives, 0, (byte)ChemID.Pain, 0, 0, 128, 0]);
        analogBiochemistry.SetChemical(ChemID.ATP, 1.0f);
        analogBiochemistry.SetChemical(ChemID.Pain, 128 / 255f);

        TickUntilClock(analogOrgan);

        float analog = analogBiochemistry.GetCreatureLocus((int)CreatureTissue.Drives, 0).Value;

        var digitalBiochemistry = new Biochemistry.Biochemistry(BiochemistryCompatibilityMode.C3DS);
        var digitalOrgan = HostedOrgan(digitalBiochemistry);
        digitalOrgan.ApplyReceptorGene([1, (byte)CreatureTissue.Drives, 0, (byte)ChemID.Pain, 0, 0, 128, (byte)ReceptorFlags.RE_DIGITAL]);
        digitalBiochemistry.SetChemical(ChemID.ATP, 1.0f);
        digitalBiochemistry.SetChemical(ChemID.Pain, 128 / 255f);

        TickUntilClock(digitalOrgan);

        float digital = digitalBiochemistry.GetCreatureLocus((int)CreatureTissue.Drives, 0).Value;
        Assert.InRange(analog, 0.24f, 0.27f);
        Assert.InRange(digital, 0.49f, 0.51f);
        Assert.True(digital > analog);
    }

    [Fact]
    public void Emitter_InvertDigitalAndRemoveFlagsAffectEmissionAndSourceLocus()
    {
        var biochemistry = new Biochemistry.Biochemistry(BiochemistryCompatibilityMode.C3DS);
        var organ = HostedOrgan(biochemistry);
        biochemistry.GetCreatureLocus((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Asleep).Value = 0.0f;
        organ.ApplyEmitterGene([
            1,
            (byte)CreatureTissue.Sensorimotor,
            SensorimotorEmitterLocus.Asleep,
            (byte)ChemID.Reward,
            0,
            1,
            128,
            (byte)(EmitterFlags.EM_INVERT | EmitterFlags.EM_DIGITAL | EmitterFlags.EM_REMOVE)
        ]);
        biochemistry.SetChemical(ChemID.ATP, 1.0f);

        TickUntilClock(organ);

        Assert.InRange(biochemistry.GetChemical(ChemID.Reward), 0.49f, 0.51f);
        Assert.Equal(0.0f, biochemistry.GetCreatureLocus((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Asleep).Value);
    }

    [Fact]
    public void Receptor_CanBindReactionRateLocus()
    {
        var biochemistry = new Biochemistry.Biochemistry(BiochemistryCompatibilityMode.C3DS);
        var organ = HostedOrgan(biochemistry);
        organ.ApplyReactionGene([1, (byte)ChemID.Glucose, 1, 0, 1, (byte)ChemID.Glycogen, 1, 0, 255]);
        organ.ApplyReceptorGene([3, 0, 0, (byte)ChemID.Reward, 0, 255, 0, 0], currentReactionNo: 0);
        biochemistry.SetChemical(ChemID.ATP, 1.0f);
        biochemistry.SetChemical(ChemID.Reward, 1.0f);

        TickUntilClock(organ);

        ReactionDefinitionView reaction = Assert.Single(organ.GetReactionDefinitionViews());
        Assert.Equal(1.0f, reaction.StoredRate, precision: 5);
    }

    [Fact]
    public void DieReceptorAndLifeForceEmitterExposeDeadAndLifeStateLoci()
    {
        var biochemistry = new Biochemistry.Biochemistry(BiochemistryCompatibilityMode.C3DS);
        var organ = HostedOrgan(biochemistry);
        organ.ApplyReceptorGene([1, (byte)CreatureTissue.Immune, ImmuneLocus.Die, (byte)ChemID.Pain, 0, 0, 255, (byte)ReceptorFlags.RE_DIGITAL]);
        organ.ApplyEmitterGene([2, 0, OrganEmitterLocus.LifeForce, (byte)ChemID.Reward, 0, 1, 255, 0]);
        biochemistry.SetChemical(ChemID.ATP, 1.0f);
        biochemistry.SetChemical(ChemID.Pain, 1.0f);

        TickUntilClock(organ);

        Assert.Equal(1.0f, biochemistry.GetCreatureLocus((int)CreatureTissue.Immune, ImmuneLocus.Dead).Value);
        Assert.True(biochemistry.GetChemical(ChemID.Reward) > 0.0f);
        float lifeBefore = organ.LocLifeForce.Value;

        organ.Injure(organ.InitialLifeForce * 0.25f);

        Assert.True(organ.LocLifeForce.Value < lifeBefore);
    }

    private static Organ HostedOrgan(Biochemistry.Biochemistry biochemistry)
    {
        var organ = new Organ();
        organ.SetOwner(biochemistry);
        organ.Init(clockRate: 1.0f, rateOfRepair: 0.0f, lifeForce: 1.0f, initialClock: 0.0f, damageDueToZeroEnergy: 0.0f);
        return organ;
    }

    private static void TickUntilClock(Organ organ)
    {
        organ.Update();
        organ.Update();
    }
}
