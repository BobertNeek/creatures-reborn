using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class StillbornHatchTests
{
    [Fact]
    public void AttemptHatch_HardInvalidGenome_ReturnsStillbornRecordWithoutLivingCreature()
    {
        byte[] rawGenome = C3DsBiologyParityTests.RawGenome(
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP));
        var payload = new EggGenomePayload(rawGenome, Sex: GeneConstants.MALE, Variant: 0, Moniker: "bad-child");
        var context = new HatchAttemptContext(
            ChildMoniker: "bad-child",
            MotherMoniker: "mother",
            FatherMoniker: "father",
            BirthTick: 120,
            Generation: 2);

        HatchResult result = CreatureHatchService.AttemptHatch(payload, context, new Rng(5));

        Assert.Equal(HatchOutcome.StillbornCreature, result.Outcome);
        Assert.Null(result.Creature);
        Assert.NotNull(result.Stillborn);
        Assert.Equal("bad-child", result.Stillborn!.ChildMoniker);
        Assert.Equal("mother", result.Stillborn.MotherMoniker);
        Assert.Equal("father", result.Stillborn.FatherMoniker);
        Assert.Equal(rawGenome, result.Stillborn.GenomeBytes);
        Assert.Contains(result.Stillborn.SafetyReport.Issues, issue => issue.Code == GenomeSimulationSafetyCode.MissingBrainInterface);
    }

    [Fact]
    public void AttemptHatch_WeakButSimulatableGenome_ReturnsLivingCreature()
    {
        byte[] rawGenome = C3DsBiologyParityTests.RawGenome(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Lobe("verb", 1),
            C3DsBiologyParityTests.Lobe("noun", 1),
            C3DsBiologyParityTests.Lobe("attn", 1),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP),
            C3DsBiologyParityTests.Receptor(ChemID.Injury, 3, 0));
        var payload = new EggGenomePayload(rawGenome, Sex: GeneConstants.FEMALE, Variant: 0, Moniker: "weak-child");
        var context = new HatchAttemptContext("weak-child", "mother", "father", BirthTick: 30, Generation: 1);

        HatchResult result = CreatureHatchService.AttemptHatch(payload, context, new Rng(8));

        Assert.Equal(HatchOutcome.LivingCreature, result.Outcome);
        Assert.NotNull(result.Creature);
        Assert.Null(result.Stillborn);
        Assert.Equal("weak-child", result.Creature!.Genome.Moniker);
        Assert.Contains(result.SafetyReport.Issues, issue => issue.Severity == GenomeSimulationSafetySeverity.WeakButLiving);
    }

    [Fact]
    public void AttemptHatch_QuarantineOnlyGenome_CanBeBlockedOrAllowedByOptions()
    {
        byte[] rawGenome = C3DsBiologyParityTests.RawGenome(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Lobe("verb", 4),
            C3DsBiologyParityTests.Lobe("noun", 4),
            C3DsBiologyParityTests.Lobe("attn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP),
            C3DsBiologyParityTests.Receptor(ChemID.Injury, 3, 0));
        var payload = new EggGenomePayload(rawGenome, Sex: GeneConstants.MALE, Variant: 0, Moniker: "lab-child");
        var context = new HatchAttemptContext("lab-child", null, null, BirthTick: 1, Generation: 0);
        var quarantineIssue = new GenomeSimulationSafetyIssue(
            GenomeSimulationSafetySeverity.QuarantineOnly,
            GenomeSimulationSafetyCode.NoFallibleLifeSupport,
            "Injected lab-only quarantine issue.");
        var report = new GenomeSimulationSafetyReport([quarantineIssue]);

        HatchResult blocked = CreatureHatchService.CreateResultFromSafetyReport(payload, context, report, new Rng(1));
        HatchResult allowed = CreatureHatchService.CreateResultFromSafetyReport(
            payload,
            context,
            report,
            new Rng(1),
            new GenomeSimulationSafetyOptions(AllowQuarantineOnlyToHatch: true));

        Assert.Equal(HatchOutcome.StillbornCreature, blocked.Outcome);
        Assert.Equal(HatchOutcome.LivingCreature, allowed.Outcome);
    }
}
