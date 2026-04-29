using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class GenomeSimulationSafetyTests
{
    [Fact]
    public void Validate_MalformedGenome_IsHardInvalid()
    {
        GenomeSimulationSafetyReport report = GenomeSimulationSafetyValidator.ValidateRaw([(byte)'b', (byte)'a', (byte)'d']);

        Assert.True(report.HasHardInvalid);
        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.MalformedGenome);
    }

    [Fact]
    public void Validate_MissingBrain_IsHardInvalid()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP));

        GenomeSimulationSafetyReport report = GenomeSimulationSafetyValidator.Validate(genome);

        Assert.True(report.HasHardInvalid);
        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.MissingBrainInterface);
    }

    [Fact]
    public void Validate_ZeroDecisionLobe_IsHardInvalid()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 0),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP));

        GenomeSimulationSafetyReport report = GenomeSimulationSafetyValidator.Validate(genome);

        Assert.True(report.HasHardInvalid);
        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.ZeroSizedRequiredLobe);
    }

    [Fact]
    public void Validate_ZeroDriveLobe_IsHardInvalid()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 0),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP));

        GenomeSimulationSafetyReport report = GenomeSimulationSafetyValidator.Validate(genome);

        Assert.True(report.HasHardInvalid);
        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.ZeroSizedRequiredLobe);
    }

    [Fact]
    public void Validate_NoFallibleLifeSupport_IsHardInvalid()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Lobe("verb", 4),
            C3DsBiologyParityTests.Lobe("noun", 4),
            C3DsBiologyParityTests.Lobe("attn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"));

        GenomeSimulationSafetyReport report = GenomeSimulationSafetyValidator.Validate(genome);

        Assert.True(report.HasHardInvalid);
        Assert.Contains(report.Issues, issue => issue.Code == GenomeSimulationSafetyCode.NoFallibleLifeSupport);
    }

    [Fact]
    public void Validate_WeakButSimulatableGenome_IsAllowedWithWeakIssue()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Lobe("verb", 1),
            C3DsBiologyParityTests.Lobe("noun", 1),
            C3DsBiologyParityTests.Lobe("attn", 1),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP),
            C3DsBiologyParityTests.Receptor(ChemID.Injury, 3, 0));

        GenomeSimulationSafetyReport report = GenomeSimulationSafetyValidator.Validate(genome);

        Assert.False(report.HasHardInvalid);
        Assert.True(report.CanHatch);
        Assert.Contains(report.Issues, issue => issue.Severity == GenomeSimulationSafetySeverity.WeakButLiving);
    }

    [Fact]
    public void Validate_DoesNotMoveRuntimeGenomeCursor()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP));

        Assert.True(genome.GetGeneType((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, BrainSubtypeInfo.NUMBRAINSUBTYPES));
        genome.Store();
        int firstPayloadByte = genome.GetByte();
        genome.Restore();

        _ = GenomeSimulationSafetyValidator.Validate(genome);

        int secondPayloadByte = genome.GetByte();
        Assert.Equal(firstPayloadByte, secondPayloadByte);
    }
}
