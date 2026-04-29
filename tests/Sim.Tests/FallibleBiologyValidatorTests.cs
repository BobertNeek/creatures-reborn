using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class FallibleBiologyValidatorTests
{
    [Fact]
    public void Validate_NoDeathPath_IsHardInvalid()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.Glucose, ChemID.Glycogen));

        FallibleBiologyReport report = FallibleBiologyValidator.Validate(GeneDecoder.Decode(genome));

        Assert.True(report.HasHardInvalid);
        Assert.False(report.LifeSupport.HasDeathOrInjuryRoute);
        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.NoFallibleLifeSupport);
    }

    [Fact]
    public void Validate_NoAtpDependency_IsHardInvalid()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.Glucose, ChemID.Glycogen),
            C3DsBiologyParityTests.Receptor(ChemID.Injury, 3, 0));

        FallibleBiologyReport report = FallibleBiologyValidator.Validate(GeneDecoder.Decode(genome));

        Assert.True(report.HasHardInvalid);
        Assert.False(report.LifeSupport.HasEnergyDependency);
        Assert.Contains(report.Issues, issue => issue.Message.Contains("ATP/ADP"));
    }

    [Fact]
    public void Validate_NoOrganHost_IsHardInvalid()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP));

        FallibleBiologyReport report = FallibleBiologyValidator.Validate(GeneDecoder.Decode(genome));

        Assert.True(report.HasHardInvalid);
        Assert.False(report.OrganFailure.HasOrgan);
        Assert.Contains(report.Issues, issue => issue.Message.Contains("organ host"));
    }

    [Fact]
    public void Validate_LongLivedButFallibleOrganism_IsAllowed()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP),
            C3DsBiologyParityTests.Receptor(ChemID.Injury, 3, 0));

        FallibleBiologyReport report = FallibleBiologyValidator.Validate(GeneDecoder.Decode(genome));

        Assert.False(report.HasHardInvalid);
        Assert.True(report.LifeSupport.HasOrgan);
        Assert.True(report.LifeSupport.HasEnergyDependency);
        Assert.True(report.LifeSupport.HasDeathOrInjuryRoute);
    }

    [Fact]
    public void GenomeSimulationSafetyValidator_UsesFallibleBiologyReport()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.Glucose, ChemID.Glycogen));

        GenomeSimulationSafetyReport report = GenomeSimulationSafetyValidator.Validate(genome);

        Assert.True(report.HasHardInvalid);
        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.NoFallibleLifeSupport);
    }
}
