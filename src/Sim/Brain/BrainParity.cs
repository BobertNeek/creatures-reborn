using System;
using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Brain;

public sealed record BrainBootParitySnapshot(
    int LobeCount,
    int TractCount,
    IReadOnlyList<LobeSnapshot> Lobes,
    IReadOnlyList<TractSnapshot> Tracts);

public sealed record InstinctProcessingTrace(
    int InstinctsRemaining,
    bool IsProcessingInstincts);

public sealed record BrainParityTrace(
    BrainBootParitySnapshot Boot,
    InstinctProcessingTrace Instincts,
    IReadOnlyList<string> ModuleNames);

public sealed record SVRuleParityCase(
    IReadOnlyList<string> Opcodes,
    IReadOnlyList<string> Operands)
{
    public static SVRuleParityCase CreateOpcodeInventory()
        => new(
            Enum.GetNames<SVRule.Op>().Where(name => name != nameof(SVRule.Op.NumOpCodes)).ToArray(),
            Enum.GetNames<SVRule.Operand>().Where(name => name != nameof(SVRule.Operand.NumOperands)).ToArray());
}
