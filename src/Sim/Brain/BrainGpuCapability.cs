using System;
using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Brain;

public enum BrainGpuSupportStatus
{
    Unknown = 0,
    Supported = 1,
    CpuOnly = 2,
}

public enum BrainGpuComponentKind
{
    Lobe = 0,
    Tract = 1,
}

public sealed record BrainGpuSupportDecision(BrainGpuSupportStatus Status, string Reason);

public sealed record BrainGpuUnsupportedComponent(
    BrainGpuComponentKind Kind,
    int Index,
    string Name,
    string Reason);

public sealed record BrainGpuCapabilityReport(
    int TotalLobes,
    int EligibleLobes,
    int AcceleratedLobes,
    int CpuOnlyLobes,
    int TotalTracts,
    int EligibleTracts,
    int AcceleratedTracts,
    int CpuOnlyTracts,
    IReadOnlyList<BrainGpuUnsupportedComponent> UnsupportedReasons)
{
    public static BrainGpuCapabilityReport Empty { get; } = new(
        TotalLobes: 0,
        EligibleLobes: 0,
        AcceleratedLobes: 0,
        CpuOnlyLobes: 0,
        TotalTracts: 0,
        EligibleTracts: 0,
        AcceleratedTracts: 0,
        CpuOnlyTracts: 0,
        UnsupportedReasons: Array.Empty<BrainGpuUnsupportedComponent>());

    public int EligibleComponents => EligibleLobes + EligibleTracts;

    public bool CoverageCompleteForPromotion =>
        EligibleComponents > 0
        && AcceleratedLobes == EligibleLobes
        && AcceleratedTracts == EligibleTracts;

    public static BrainGpuCapabilityReport FromBrain(Brain brain, bool traceActive)
    {
        var unsupported = new List<BrainGpuUnsupportedComponent>();
        int eligibleLobes = 0;
        int cpuOnlyLobes = 0;
        for (int i = 0; i < brain.LobeCount; i++)
        {
            Lobe? lobe = brain.GetLobe(i);
            if (lobe == null)
                continue;

            if (lobe.CanRunOnAccelerator(out string? reason))
            {
                eligibleLobes++;
            }
            else
            {
                cpuOnlyLobes++;
                unsupported.Add(new(
                    BrainGpuComponentKind.Lobe,
                    i,
                    Brain.TokenToString(lobe.Token),
                    reason ?? "Lobe is not supported by the GPU backend."));
            }
        }

        int eligibleTracts = 0;
        int cpuOnlyTracts = 0;
        for (int i = 0; i < brain.TractCount; i++)
        {
            Tract? tract = brain.GetTract(i);
            if (tract == null)
                continue;

            string? reason = null;
            if (!traceActive && tract.CanRunOnAccelerator(out reason))
            {
                eligibleTracts++;
            }
            else
            {
                cpuOnlyTracts++;
                string description = DescribeTract(tract);
                unsupported.Add(new(
                    BrainGpuComponentKind.Tract,
                    i,
                    description,
                    traceActive ? "Learning trace capture is active." : reason ?? "Tract is not supported by the GPU backend."));
            }
        }

        return new(
            brain.LobeCount,
            eligibleLobes,
            AcceleratedLobes: 0,
            cpuOnlyLobes,
            brain.TractCount,
            eligibleTracts,
            AcceleratedTracts: 0,
            cpuOnlyTracts,
            unsupported);
    }

    public static BrainGpuCapabilityReport FromContext(BrainExecutionContext context)
    {
        var unsupported = new List<BrainGpuUnsupportedComponent>();
        int totalLobes = 0;
        int eligibleLobes = 0;
        int cpuOnlyLobes = 0;
        int totalTracts = 0;
        int eligibleTracts = 0;
        int cpuOnlyTracts = 0;

        foreach (BrainComponent component in context.Components)
        {
            if (component is Lobe lobe)
            {
                int index = totalLobes++;
                if (context.IsLobeTokenShadowed(lobe.Token))
                {
                    cpuOnlyLobes++;
                    unsupported.Add(new(
                        BrainGpuComponentKind.Lobe,
                        index,
                        Brain.TokenToString(lobe.Token),
                        "Lobe is shadowed by a brain module."));
                }
                else if (lobe.CanRunOnAccelerator(out string? reason))
                {
                    eligibleLobes++;
                }
                else
                {
                    cpuOnlyLobes++;
                    unsupported.Add(new(
                        BrainGpuComponentKind.Lobe,
                        index,
                        Brain.TokenToString(lobe.Token),
                        reason ?? "Lobe is not supported by the GPU backend."));
                }
            }
            else if (component is Tract tract)
            {
                int index = totalTracts++;
                string? reason = null;
                if (context.Trace == null && tract.CanRunOnAccelerator(out reason))
                {
                    eligibleTracts++;
                }
                else
                {
                    cpuOnlyTracts++;
                    unsupported.Add(new(
                        BrainGpuComponentKind.Tract,
                        index,
                        DescribeTract(tract),
                        context.Trace != null ? "Learning trace capture is active." : reason ?? "Tract is not supported by the GPU backend."));
                }
            }
        }

        return new(
            totalLobes,
            eligibleLobes,
            AcceleratedLobes: 0,
            cpuOnlyLobes,
            totalTracts,
            eligibleTracts,
            AcceleratedTracts: 0,
            cpuOnlyTracts,
            unsupported);
    }

    public BrainGpuCapabilityReport WithAcceleratedCounts(int acceleratedLobes, int acceleratedTracts)
        => this with
        {
            AcceleratedLobes = acceleratedLobes,
            AcceleratedTracts = acceleratedTracts,
        };

    private static string DescribeTract(Tract tract)
    {
        TractAcceleratorState state = tract.CreateAcceleratorState();
        return $"{Brain.TokenToString(state.SourceToken)}->{Brain.TokenToString(state.DestinationToken)}";
    }
}

public static class BrainGpuSvRuleSupport
{
    public static BrainGpuSupportDecision Describe(SVRule.Op operation)
        => operation switch
        {
            SVRule.Op.NumOpCodes => new(BrainGpuSupportStatus.Unknown, "SVRule opcode sentinel is not executable."),
            SVRule.Op.DoSignalNoise => new(BrainGpuSupportStatus.CpuOnly, "Deterministic GPU signal-noise RNG parity is not implemented yet."),
            SVRule.Op.IfEqualTo
                or SVRule.Op.IfNotEqualTo
                or SVRule.Op.IfGreaterThan
                or SVRule.Op.IfLessThan
                or SVRule.Op.IfGreaterThanOrEqualTo
                or SVRule.Op.IfLessThanOrEqualTo
                or SVRule.Op.IfZero
                or SVRule.Op.IfNonZero
                or SVRule.Op.IfPositive
                or SVRule.Op.IfNegative
                or SVRule.Op.IfNonNegative
                or SVRule.Op.IfNonPositive
                or SVRule.Op.DivideBy
                or SVRule.Op.DivideInto
                or SVRule.Op.DoNominalThreshold
                or SVRule.Op.DoWinnerTakesAll
                or SVRule.Op.IfZeroStop
                or SVRule.Op.IfNZeroStop
                or SVRule.Op.IfZeroGoto
                or SVRule.Op.IfNZeroGoto
                or SVRule.Op.IfLessThanStop
                or SVRule.Op.IfGreaterThanStop
                or SVRule.Op.IfLessThanOrEqualStop
                or SVRule.Op.IfGreaterThanOrEqualStop
                or SVRule.Op.IfNegativeGoto
                or SVRule.Op.IfPositiveGoto => new(BrainGpuSupportStatus.CpuOnly, "Float branching stays CPU-only until GPU denormal comparison parity is implemented."),
            SVRule.Op.SetRewardThreshold
                or SVRule.Op.SetRewardRate
                or SVRule.Op.SetRewardChemicalIndex
                or SVRule.Op.SetPunishmentThreshold
                or SVRule.Op.SetPunishmentRate
                or SVRule.Op.SetPunishmentChemicalIndex => new(BrainGpuSupportStatus.CpuOnly, "Chemical reinforcement configuration stays CPU-only until trace parity is implemented."),
            SVRule.Op.PreserveVariable
                or SVRule.Op.RestoreVariable
                or SVRule.Op.PreserveSpareVariable
                or SVRule.Op.RestoreSpareVariable => new(BrainGpuSupportStatus.CpuOnly, "Preserve/restore variable opcodes stay CPU-only until long-run spare-variable parity is implemented."),
            _ => new(BrainGpuSupportStatus.Supported, "Supported by the current RenderingDevice SVRule shader subset."),
        };

    public static BrainGpuSupportDecision Describe(SVRule.Operand operand)
        => operand switch
        {
            SVRule.Operand.NumOperands => new(BrainGpuSupportStatus.Unknown, "SVRule operand sentinel is not executable."),
            SVRule.Operand.Random => new(BrainGpuSupportStatus.CpuOnly, "Deterministic GPU random operand parity is not implemented yet."),
            _ => new(BrainGpuSupportStatus.Supported, "Supported by the current RenderingDevice SVRule shader subset."),
        };

    public static bool CanRunRules(
        IEnumerable<SVRuleEntrySnapshot> initRule,
        IEnumerable<SVRuleEntrySnapshot> updateRule,
        out string? reason)
    {
        foreach (SVRuleEntrySnapshot entry in initRule.Concat(updateRule))
        {
            BrainGpuSupportDecision op = Describe(entry.Operation);
            if (op.Status != BrainGpuSupportStatus.Supported)
            {
                reason = $"{entry.Operation}: {op.Reason}";
                return false;
            }

            BrainGpuSupportDecision operand = Describe(entry.Operand);
            if (operand.Status != BrainGpuSupportStatus.Supported)
            {
                reason = $"{entry.Operand}: {operand.Reason}";
                return false;
            }
        }

        reason = null;
        return true;
    }
}
