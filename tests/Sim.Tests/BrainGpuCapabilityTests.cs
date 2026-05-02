using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using Xunit;
using B = CreaturesReborn.Sim.Brain.Brain;

namespace CreaturesReborn.Sim.Tests;

public sealed class BrainGpuCapabilityTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void SVRuleSupport_ClassifiesEveryOpcodeAndOperand()
    {
        foreach (SVRule.Op op in Enum.GetValues<SVRule.Op>().Where(op => op != SVRule.Op.NumOpCodes))
        {
            BrainGpuSupportDecision decision = BrainGpuSvRuleSupport.Describe(op);

            Assert.NotEqual(BrainGpuSupportStatus.Unknown, decision.Status);
            Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
        }

        foreach (SVRule.Operand operand in Enum.GetValues<SVRule.Operand>().Where(operand => operand != SVRule.Operand.NumOperands))
        {
            BrainGpuSupportDecision decision = BrainGpuSvRuleSupport.Describe(operand);

            Assert.NotEqual(BrainGpuSupportStatus.Unknown, decision.Status);
            Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
        }
    }

    [Fact]
    public void SVRuleSupport_ExplicitlyKeepsRngAndChemicalReinforcementCpuOnlyUntilParityExists()
    {
        Assert.Equal(BrainGpuSupportStatus.CpuOnly, BrainGpuSvRuleSupport.Describe(SVRule.Operand.Random).Status);
        Assert.Equal(BrainGpuSupportStatus.CpuOnly, BrainGpuSvRuleSupport.Describe(SVRule.Op.DoSignalNoise).Status);
        Assert.Equal(BrainGpuSupportStatus.CpuOnly, BrainGpuSvRuleSupport.Describe(SVRule.Op.SetRewardChemicalIndex).Status);
        Assert.Equal(BrainGpuSupportStatus.CpuOnly, BrainGpuSvRuleSupport.Describe(SVRule.Op.SetPunishmentChemicalIndex).Status);
        Assert.Equal(BrainGpuSupportStatus.CpuOnly, BrainGpuSvRuleSupport.Describe(SVRule.Op.PreserveVariable).Status);
        Assert.Equal(BrainGpuSupportStatus.CpuOnly, BrainGpuSvRuleSupport.Describe(SVRule.Op.RestoreVariable).Status);
        Assert.Equal(BrainGpuSupportStatus.CpuOnly, BrainGpuSvRuleSupport.Describe(SVRule.Op.PreserveSpareVariable).Status);
        Assert.Equal(BrainGpuSupportStatus.CpuOnly, BrainGpuSvRuleSupport.Describe(SVRule.Op.RestoreSpareVariable).Status);
    }

    [Theory]
    [InlineData(SVRule.Op.IfEqualTo)]
    [InlineData(SVRule.Op.IfNotEqualTo)]
    [InlineData(SVRule.Op.IfGreaterThan)]
    [InlineData(SVRule.Op.IfLessThan)]
    [InlineData(SVRule.Op.IfGreaterThanOrEqualTo)]
    [InlineData(SVRule.Op.IfLessThanOrEqualTo)]
    [InlineData(SVRule.Op.IfZero)]
    [InlineData(SVRule.Op.IfNonZero)]
    [InlineData(SVRule.Op.IfPositive)]
    [InlineData(SVRule.Op.IfNegative)]
    [InlineData(SVRule.Op.IfNonNegative)]
    [InlineData(SVRule.Op.IfNonPositive)]
    [InlineData(SVRule.Op.DoWinnerTakesAll)]
    [InlineData(SVRule.Op.IfZeroStop)]
    [InlineData(SVRule.Op.IfNZeroStop)]
    [InlineData(SVRule.Op.IfZeroGoto)]
    [InlineData(SVRule.Op.IfNZeroGoto)]
    [InlineData(SVRule.Op.DivideBy)]
    [InlineData(SVRule.Op.DivideInto)]
    [InlineData(SVRule.Op.DoNominalThreshold)]
    [InlineData(SVRule.Op.IfLessThanStop)]
    [InlineData(SVRule.Op.IfGreaterThanStop)]
    [InlineData(SVRule.Op.IfLessThanOrEqualStop)]
    [InlineData(SVRule.Op.IfGreaterThanOrEqualStop)]
    [InlineData(SVRule.Op.IfNegativeGoto)]
    [InlineData(SVRule.Op.IfPositiveGoto)]
    public void SVRuleSupport_KeepsDenormalSensitiveBranchingOpcodesCpuOnlyUntilParityExists(SVRule.Op operation)
    {
        Assert.Equal(BrainGpuSupportStatus.CpuOnly, BrainGpuSvRuleSupport.Describe(operation).Status);
    }

    [Fact]
    public void CapabilityReport_SeparatesEligibleAcceleratedAndCpuOnlyComponents()
    {
        B brain = LoadBrain(seed: 400);

        BrainGpuCapabilityReport report = BrainGpuCapabilityReport.FromBrain(brain, traceActive: false);

        Assert.Equal(brain.LobeCount, report.TotalLobes);
        Assert.Equal(brain.TractCount, report.TotalTracts);
        Assert.True(report.EligibleLobes > 0);
        Assert.True(report.EligibleTracts > 0);
        Assert.True(report.CpuOnlyLobes > 0);
        Assert.True(report.CpuOnlyTracts > 0);
        Assert.NotEmpty(report.UnsupportedReasons);
        Assert.False(report.CoverageCompleteForPromotion);
    }

    private static B LoadBrain(int seed)
    {
        var genome = GenomeReader.LoadNew(new Rng(seed), Path.GetFullPath(StarterGenomePath));
        var brain = new B();
        brain.ReadFromGenome(genome, new Rng(seed));
        brain.RegisterBiochemistry(new float[256]);
        return brain;
    }
}
