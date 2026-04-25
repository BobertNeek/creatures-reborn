namespace CreaturesReborn.Sim.Creature;

/// <summary>
/// C3/DS-style edible categories. These are gameplay categories, not specific
/// sprite names: many agents can be fruit, seeds, or food.
/// </summary>
public enum FoodKind
{
    Fruit = 0,
    Seed = 1,
    Food = 2,
}

public readonly record struct FoodNutrition(
    FoodKind Kind,
    string DisplayName,
    int StimulusId,
    float ATPAmount)
{
    public static FoodNutrition ForKind(FoodKind kind) => kind switch
    {
        FoodKind.Seed => new FoodNutrition(
            FoodKind.Seed,
            "Seed",
            global::CreaturesReborn.Sim.Creature.StimulusId.AteFat,
            0.12f),

        FoodKind.Food => new FoodNutrition(
            FoodKind.Food,
            "Food",
            global::CreaturesReborn.Sim.Creature.StimulusId.AteProtein,
            0.18f),

        _ => new FoodNutrition(
            FoodKind.Fruit,
            "Fruit",
            global::CreaturesReborn.Sim.Creature.StimulusId.AteFruit,
            0.20f),
    };
}
