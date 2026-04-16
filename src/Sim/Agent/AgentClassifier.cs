namespace CreaturesReborn.Sim.Agent;

/// <summary>
/// Every agent in c2e is identified by a 3-part classifier: Family, Genus, Species.
/// Scripts are dispatched based on (family, genus, species, event#).
///
/// Well-known families in c2e:
///   1 = SimpleAgent (gadgets, plants, etc.)
///   2 = CompoundAgent (multi-part objects, machines)
///   3 = PointerAgent (the hand)
///   4 = CreatureAgent (norns, ettins, grendels)
/// </summary>
public readonly struct AgentClassifier
{
    public readonly int Family;
    public readonly int Genus;
    public readonly int Species;

    public AgentClassifier(int family, int genus, int species)
    {
        Family  = family;
        Genus   = genus;
        Species = species;
    }

    /// <summary>
    /// Check whether this classifier matches a query.
    /// A query value of 0 means "any" (wildcard).
    /// </summary>
    public bool Matches(int family, int genus, int species)
    {
        if (family != 0 && family != Family) return false;
        if (genus  != 0 && genus  != Genus)  return false;
        if (species != 0 && species != Species) return false;
        return true;
    }

    public override string ToString() => $"{Family} {Genus} {Species}";
    public override int GetHashCode() => (Family << 20) ^ (Genus << 10) ^ Species;
    public override bool Equals(object? obj)
        => obj is AgentClassifier ac && ac.Family == Family && ac.Genus == Genus && ac.Species == Species;

    public static bool operator ==(AgentClassifier a, AgentClassifier b) => a.Equals(b);
    public static bool operator !=(AgentClassifier a, AgentClassifier b) => !a.Equals(b);
}

/// <summary>
/// Well-known agent families matching the c2e convention.
/// </summary>
public static class AgentFamily
{
    public const int Simple    = 1;
    public const int Compound  = 2;
    public const int Pointer   = 3;
    public const int Creature  = 4;
}
