using System;
using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Biochemistry;

public enum ChemicalReinforcementDomain
{
    Energy = 0,
    Hunger,
    Pain,
    FearStress,
    Comfort,
    Fatigue,
    Social,
    Learning,
    Health
}

public enum ChemicalReinforcementValence
{
    Positive = 0,
    Negative = 1
}

public sealed record ChemicalDeltaWindow(IReadOnlyList<ChemicalDelta> Deltas)
{
    public static ChemicalDeltaWindow FromSnapshots(IReadOnlyList<float> before, IReadOnlyList<float> after)
    {
        int count = Math.Min(Math.Min(before.Count, after.Count), BiochemConst.NUMCHEM);
        var deltas = new List<ChemicalDelta>();
        for (int i = 0; i < count; i++)
        {
            float beforeValue = before[i];
            float afterValue = after[i];
            float amount = afterValue - beforeValue;
            if (Math.Abs(amount) <= 0.000001f)
                continue;

            deltas.Add(new ChemicalDelta(
                i,
                ChemicalCatalog.Get(i),
                beforeValue,
                amount,
                afterValue,
                ChemicalDeltaSource.DirectSet,
                "snapshot-delta"));
        }

        return new ChemicalDeltaWindow(deltas);
    }

    public static ChemicalDeltaWindow FromTrace(BiochemistryTrace trace)
        => new(trace.Deltas);
}

public sealed record ChemicalReinforcementSignal(
    int ChemicalId,
    ChemicalDefinition Chemical,
    ChemicalReinforcementDomain Domain,
    ChemicalReinforcementValence Valence,
    float Delta,
    float Strength,
    string Reason);

public sealed record ChemicalReinforcementTrace(IReadOnlyList<ChemicalReinforcementSignal> Signals)
{
    public ChemicalReinforcementSignal? StrongestPositive
        => Signals
            .Where(signal => signal.Valence == ChemicalReinforcementValence.Positive)
            .OrderByDescending(signal => signal.Strength)
            .FirstOrDefault();

    public ChemicalReinforcementSignal? StrongestNegative
        => Signals
            .Where(signal => signal.Valence == ChemicalReinforcementValence.Negative)
            .OrderByDescending(signal => signal.Strength)
            .FirstOrDefault();
}

public sealed class ChemicalReinforcementProfile
{
    private readonly IReadOnlyDictionary<ChemicalReinforcementDomain, float> _domainWeights;

    public static ChemicalReinforcementProfile Default { get; } = new();

    public ChemicalReinforcementProfile(IReadOnlyDictionary<ChemicalReinforcementDomain, float>? domainWeights = null)
    {
        var weights = new Dictionary<ChemicalReinforcementDomain, float>();
        foreach (ChemicalReinforcementDomain domain in Enum.GetValues<ChemicalReinforcementDomain>())
            weights[domain] = 1.0f;

        if (domainWeights != null)
        {
            foreach ((ChemicalReinforcementDomain domain, float weight) in domainWeights)
                weights[domain] = Math.Clamp(weight, 0.0f, 16.0f);
        }

        _domainWeights = weights;
    }

    public float WeightFor(ChemicalReinforcementDomain domain)
        => _domainWeights.TryGetValue(domain, out float weight) ? weight : 1.0f;

    public ChemicalReinforcementProfile WithDomainWeight(ChemicalReinforcementDomain domain, float weight)
    {
        var weights = new Dictionary<ChemicalReinforcementDomain, float>(_domainWeights)
        {
            [domain] = Math.Clamp(weight, 0.0f, 16.0f)
        };
        return new ChemicalReinforcementProfile(weights);
    }
}

public static class ChemicalReinforcementBus
{
    public static ChemicalReinforcementTrace Evaluate(BiochemistryTrace trace, ChemicalReinforcementProfile profile)
        => Evaluate(ChemicalDeltaWindow.FromTrace(trace), profile);

    public static ChemicalReinforcementTrace Evaluate(ChemicalDeltaWindow window, ChemicalReinforcementProfile profile)
    {
        var signals = new List<ChemicalReinforcementSignal>();
        foreach (ChemicalDelta delta in window.Deltas)
        {
            if (!TryClassify(delta.ChemicalId, delta.Amount, out ChemicalReinforcementDomain domain, out ChemicalReinforcementValence valence, out string reason))
                continue;

            float strength = Math.Abs(delta.Amount) * profile.WeightFor(domain);
            if (strength <= 0.000001f)
                continue;

            signals.Add(new ChemicalReinforcementSignal(
                delta.ChemicalId,
                delta.Chemical,
                domain,
                valence,
                delta.Amount,
                strength,
                reason));
        }

        return new ChemicalReinforcementTrace(signals);
    }

    private static bool TryClassify(
        int chemicalId,
        float delta,
        out ChemicalReinforcementDomain domain,
        out ChemicalReinforcementValence valence,
        out string reason)
    {
        domain = ChemicalReinforcementDomain.Health;
        valence = ChemicalReinforcementValence.Positive;
        reason = string.Empty;

        if (delta == 0.0f)
            return false;

        switch (chemicalId)
        {
            case ChemID.ATP:
            case ChemID.Glucose:
            case ChemID.Glycogen:
            case ChemID.Adipose:
            case ChemID.Muscle:
            case ChemID.Oxygen:
                domain = ChemicalReinforcementDomain.Energy;
                valence = delta > 0 ? ChemicalReinforcementValence.Positive : ChemicalReinforcementValence.Negative;
                reason = delta > 0 ? "energy recovered" : "energy depleted";
                return true;

            case ChemID.ADP:
                domain = ChemicalReinforcementDomain.Energy;
                valence = delta > 0 ? ChemicalReinforcementValence.Negative : ChemicalReinforcementValence.Positive;
                reason = delta > 0 ? "spent energy rose" : "spent energy fell";
                return true;

            case ChemID.HungerForCarb:
            case ChemID.HungerForProtein:
            case ChemID.HungerForFat:
                domain = ChemicalReinforcementDomain.Hunger;
                valence = delta < 0 ? ChemicalReinforcementValence.Positive : ChemicalReinforcementValence.Negative;
                reason = delta < 0 ? "hunger decreased" : "hunger increased";
                return true;

            case ChemID.Pain:
            case ChemID.Injury:
            case ChemID.Wounded:
                domain = ChemicalReinforcementDomain.Pain;
                valence = delta < 0 ? ChemicalReinforcementValence.Positive : ChemicalReinforcementValence.Negative;
                reason = delta < 0 ? "pain or injury decreased" : "pain or injury increased";
                return true;

            case ChemID.Fear:
            case ChemID.Punishment:
                domain = chemicalId == ChemID.Punishment
                    ? ChemicalReinforcementDomain.Learning
                    : ChemicalReinforcementDomain.FearStress;
                valence = delta < 0 ? ChemicalReinforcementValence.Positive : ChemicalReinforcementValence.Negative;
                reason = delta < 0 ? "stress signal decreased" : "stress signal increased";
                return true;

            case ChemID.Reward:
                domain = ChemicalReinforcementDomain.Learning;
                valence = delta > 0 ? ChemicalReinforcementValence.Positive : ChemicalReinforcementValence.Negative;
                reason = delta > 0 ? "reward increased" : "reward decreased";
                return true;

            case ChemID.Coldness:
            case ChemID.Hotness:
                domain = ChemicalReinforcementDomain.Comfort;
                valence = delta < 0 ? ChemicalReinforcementValence.Positive : ChemicalReinforcementValence.Negative;
                reason = delta < 0 ? "temperature discomfort decreased" : "temperature discomfort increased";
                return true;

            case ChemID.Tiredness:
            case ChemID.Sleepiness:
                domain = ChemicalReinforcementDomain.Fatigue;
                valence = delta < 0 ? ChemicalReinforcementValence.Positive : ChemicalReinforcementValence.Negative;
                reason = delta < 0 ? "fatigue decreased" : "fatigue increased";
                return true;

            case ChemID.Loneliness:
            case ChemID.Crowdedness:
            case ChemID.SexDrive:
                domain = ChemicalReinforcementDomain.Social;
                valence = delta < 0 ? ChemicalReinforcementValence.Positive : ChemicalReinforcementValence.Negative;
                reason = delta < 0 ? "social drive pressure decreased" : "social drive pressure increased";
                return true;
        }

        if (chemicalId >= ChemID.FirstToxin && chemicalId <= ChemID.LastToxin)
        {
            domain = ChemicalReinforcementDomain.Health;
            valence = delta < 0 ? ChemicalReinforcementValence.Positive : ChemicalReinforcementValence.Negative;
            reason = delta < 0 ? "toxin decreased" : "toxin increased";
            return true;
        }

        if (chemicalId >= ChemID.FirstAntigen && chemicalId <= ChemID.LastAntigen)
        {
            domain = ChemicalReinforcementDomain.Health;
            valence = delta < 0 ? ChemicalReinforcementValence.Positive : ChemicalReinforcementValence.Negative;
            reason = delta < 0 ? "antigen decreased" : "antigen increased";
            return true;
        }

        return false;
    }
}
