using System.Collections.Generic;

namespace CreaturesReborn.Sim.Biochemistry;

public enum OrganHealthState
{
    Functioning,
    EnergyStarved,
    Failed
}

public sealed record ReactionDefinitionView(
    int Index,
    int Reactant1,
    float Reactant1Proportion,
    int Reactant2,
    float Reactant2Proportion,
    int Product1,
    float Product1Proportion,
    int Product2,
    float Product2Proportion,
    float StoredRate);

public sealed record OrganSnapshot(
    int Index,
    OrganHealthState HealthState,
    float InitialLifeForce,
    float ShortTermLifeForce,
    float LongTermLifeForce,
    float LongTermRateOfRepair,
    float EnergyCost,
    float DamageDueToZeroEnergy,
    int ReactionCount,
    int EmitterCount,
    int ReceptorCount);

public sealed record OrganDefinitionView(
    int Index,
    OrganSnapshot Snapshot,
    IReadOnlyList<ReactionDefinitionView> Reactions);
