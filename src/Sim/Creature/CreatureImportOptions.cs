using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;

namespace CreaturesReborn.Sim.Creature;

public sealed record CreatureImportOptions(
    int Sex = GeneConstants.MALE,
    byte Age = 0,
    int Variant = 0,
    string Moniker = "",
    BiochemistryCompatibilityMode BiochemistryMode = BiochemistryCompatibilityMode.C3DS);
