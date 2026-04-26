using System.Collections.Generic;

namespace CreaturesReborn.Sim.Brain;

public sealed record SVRuleEntrySnapshot(
    int Index,
    SVRule.Op Operation,
    SVRule.Operand Operand,
    int ArrayIndex,
    float FloatValue);

public static class SVRuleDisassembler
{
    public static IReadOnlyList<SVRuleEntrySnapshot> Disassemble(SVRule rule)
        => rule.DescribeEntries();
}
