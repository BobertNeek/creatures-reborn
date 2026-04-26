using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Biochemistry;

public enum ChemicalDeltaSource
{
    DirectSet = 0,
    DirectAdd,
    DirectSubtract,
    HalfLifeDecay,
    NeuroEmitter,
    Emitter,
    Reaction,
    Receptor,
    OrganInjury,
    OrganEnergy,
    Stimulus,
    CreatureInjection,
    Metabolism,
    Fatigue,
    InjuryRecovery,
    Environment,
    Respiration,
    Immune,
    Toxin
}

public sealed record ChemicalDelta(
    int ChemicalId,
    ChemicalDefinition Chemical,
    float Before,
    float Amount,
    float After,
    ChemicalDeltaSource Source,
    string? Detail);

public sealed class BiochemistryTrace
{
    private readonly List<ChemicalDelta> _deltas = new();

    public IReadOnlyList<ChemicalDelta> Deltas => _deltas;

    public bool IsEmpty => _deltas.Count == 0;

    public void Record(
        int chemicalId,
        float before,
        float amount,
        float after,
        ChemicalDeltaSource source,
        string? detail = null)
    {
        if (chemicalId == ChemID.None)
            return;
        if (before == after && amount == 0.0f)
            return;

        _deltas.Add(new(
            chemicalId,
            ChemicalCatalog.Get(chemicalId),
            before,
            amount,
            after,
            source,
            detail));
    }

    public IReadOnlyList<ChemicalDelta> ForChemical(int chemicalId)
        => _deltas.Where(delta => delta.ChemicalId == chemicalId).ToList();
}
