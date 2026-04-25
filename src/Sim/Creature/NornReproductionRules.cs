using CreaturesReborn.Sim.Genome;

namespace CreaturesReborn.Sim.Creature;

public static class NornReproductionRules
{
    public const float DefaultMateRadius = 3.0f;

    public static bool CanLayEgg(
        Creature first,
        Creature second,
        float distance,
        float cooldownSeconds,
        float mateRadius = DefaultMateRadius)
    {
        if (ReferenceEquals(first, second)) return false;
        if (cooldownSeconds > 0) return false;
        if (distance > mateRadius) return false;
        if (!CreatureAge.IsReproductive(first.Genome.Age)) return false;
        if (!CreatureAge.IsReproductive(second.Genome.Age)) return false;

        return IsFemaleMalePair(first, second);
    }

    public static Creature? ResolveMother(Creature first, Creature second)
    {
        if (first.Genome.Sex == GeneConstants.FEMALE && second.Genome.Sex == GeneConstants.MALE)
            return first;
        if (second.Genome.Sex == GeneConstants.FEMALE && first.Genome.Sex == GeneConstants.MALE)
            return second;
        return null;
    }

    public static Creature? ResolveFather(Creature first, Creature second)
    {
        if (first.Genome.Sex == GeneConstants.MALE && second.Genome.Sex == GeneConstants.FEMALE)
            return first;
        if (second.Genome.Sex == GeneConstants.MALE && first.Genome.Sex == GeneConstants.FEMALE)
            return second;
        return null;
    }

    private static bool IsFemaleMalePair(Creature first, Creature second)
        => ResolveMother(first, second) != null && ResolveFather(first, second) != null;
}
