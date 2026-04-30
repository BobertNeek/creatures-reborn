using System.Collections.Generic;

namespace CreaturesReborn.Sim.Biochemistry;

public sealed record ReactionParitySnapshot(
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

public sealed record ReceptorParitySnapshot(
    int Index,
    int OrganId,
    int TissueId,
    int LocusId,
    int ChemicalId,
    float Threshold,
    float Nominal,
    float Gain,
    int Effect,
    bool IsClockRateReceptor);

public sealed record EmitterParitySnapshot(
    int Index,
    int OrganId,
    int TissueId,
    int LocusId,
    int ChemicalId,
    float Threshold,
    float BioTickRate,
    float Gain,
    int Effect);

public sealed record NeuroEmitterParitySnapshot(
    int Index,
    float BioTickRate,
    IReadOnlyList<ChemicalEmissionParitySnapshot> Emissions);

public sealed record ChemicalEmissionParitySnapshot(int ChemicalId, float Amount);

public sealed record HalfLifeParitySnapshot(
    int ChemicalId,
    string ChemicalName,
    float DecayRate);

public sealed record OrganParitySnapshot(
    OrganSnapshot Snapshot,
    IReadOnlyList<ReactionParitySnapshot> Reactions,
    IReadOnlyList<ReceptorParitySnapshot> Receptors,
    IReadOnlyList<EmitterParitySnapshot> Emitters);

public sealed record BiochemistryParityTrace(
    BiochemistryCompatibilityMode CompatibilityMode,
    int ChemicalCount,
    int NonZeroChemicalCount,
    IReadOnlyList<HalfLifeParitySnapshot> HalfLives,
    IReadOnlyList<OrganParitySnapshot> Organs,
    IReadOnlyList<NeuroEmitterParitySnapshot> NeuroEmitters);
