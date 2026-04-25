using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Sim.Agent;

public readonly record struct AgentArchetype(
    string Id,
    AgentClassifier Classifier,
    int ObjectCategory,
    string Noun,
    bool IsEdible);

public static class AgentCatalog
{
    public static readonly AgentArchetype Fruit = new(
        "food.fruit",
        new AgentClassifier(AgentFamily.Simple, 2, 1),
        ObjectCategory.Fruit,
        "fruit",
        IsEdible: true);

    public static readonly AgentArchetype Seed = new(
        "food.seed",
        new AgentClassifier(AgentFamily.Simple, 2, 2),
        ObjectCategory.Seed,
        "seed",
        IsEdible: true);

    public static readonly AgentArchetype Food = new(
        "food.food",
        new AgentClassifier(AgentFamily.Simple, 2, 3),
        ObjectCategory.Food,
        "food",
        IsEdible: true);

    public static readonly AgentArchetype Plant = new(
        "plant.food_pod",
        new AgentClassifier(AgentFamily.Simple, 3, 1),
        ObjectCategory.Plant,
        "plant",
        IsEdible: false);

    public static readonly AgentArchetype Egg = new(
        "creature.egg",
        new AgentClassifier(AgentFamily.Simple, 4, 1),
        ObjectCategory.Egg,
        "egg",
        IsEdible: false);

    public static readonly AgentArchetype Norn = new(
        "creature.norn",
        new AgentClassifier(AgentFamily.Creature, 1, 1),
        ObjectCategory.Norn,
        "norn",
        IsEdible: false);

    public static readonly AgentArchetype Hand = new(
        "pointer.hand",
        new AgentClassifier(AgentFamily.Pointer, 1, 1),
        ObjectCategory.Hand,
        "hand",
        IsEdible: false);

    public static readonly AgentArchetype Elevator = new(
        "machine.elevator",
        new AgentClassifier(AgentFamily.Compound, 5, 1),
        ObjectCategory.Elevator,
        "elevator",
        IsEdible: false);

    public static readonly AgentArchetype EmpathicVendor = new(
        "machine.empathic_vendor",
        new AgentClassifier(AgentFamily.Compound, 6, 1),
        ObjectCategory.Dispenser,
        "dispenser",
        IsEdible: false);

    public static readonly AgentArchetype Toy = new(
        "toy.generic",
        new AgentClassifier(AgentFamily.Simple, 7, 1),
        ObjectCategory.Toy,
        "toy",
        IsEdible: false);

    public static readonly AgentArchetype Machine = new(
        "machine.generic",
        new AgentClassifier(AgentFamily.Compound, 6, 0),
        ObjectCategory.Machine,
        "machine",
        IsEdible: false);

    public static AgentArchetype ForFood(FoodKind kind) => kind switch
    {
        FoodKind.Seed => Seed,
        FoodKind.Food => Food,
        _ => Fruit,
    };
}
