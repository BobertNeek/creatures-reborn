using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class BrainInterfaceValidatorTests
{
    [Fact]
    public void Validate_MissingRequiredLobe_IsHardInvalid()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(C3DsBiologyParityTests.Lobe("driv", 4));

        BrainInterfaceReport report = BrainInterfaceValidator.Validate(GeneDecoder.Decode(genome));

        Assert.True(report.HasHardInvalid);
        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.MissingRequiredLobe);
    }

    [Fact]
    public void Validate_ZeroSizedRequiredLobe_IsHardInvalid()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 0));

        BrainInterfaceReport report = BrainInterfaceValidator.Validate(GeneDecoder.Decode(genome));

        Assert.True(report.HasHardInvalid);
        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.ZeroSizedRequiredLobe);
    }

    [Fact]
    public void Validate_MissingRequiredRoute_IsHardInvalid()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4));
        var spec = new MinimumBrainInterfaceSpec(
            RequiredLobes: ["driv", "decn"],
            RequiredRoutes: [("driv", "decn")]);

        BrainInterfaceReport report = BrainInterfaceValidator.Validate(GeneDecoder.Decode(genome), spec);

        Assert.True(report.HasHardInvalid);
        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.MissingRequiredBrainRoute);
    }

    [Fact]
    public void Validate_BoundedWeakOptionalLobe_CanStillTick()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Lobe("verb", 1),
            C3DsBiologyParityTests.Tract("driv", "decn"));
        var spec = new MinimumBrainInterfaceSpec(
            RequiredLobes: ["driv", "decn"],
            RequiredRoutes: [("driv", "decn")]);

        BrainInterfaceReport report = BrainInterfaceValidator.Validate(GeneDecoder.Decode(genome), spec);

        Assert.False(report.HasHardInvalid);
        Assert.True(report.CanTick);
        Assert.Contains(report.Issues, issue => issue.Severity == GenomeSimulationSafetySeverity.WeakButLiving);
    }

    [Fact]
    public void GenomeSimulationSafetyValidator_UsesBrainInterfaceRoutes()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP));
        var options = new GenomeSimulationSafetyOptions(
            Brain: new MinimumBrainInterfaceSpec(
                RequiredLobes: ["driv", "decn"],
                RequiredRoutes: [("driv", "decn")]));

        GenomeSimulationSafetyReport report = GenomeSimulationSafetyValidator.Validate(genome, options);

        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.MissingRequiredBrainRoute);
        Assert.True(report.HasHardInvalid);
    }
}
