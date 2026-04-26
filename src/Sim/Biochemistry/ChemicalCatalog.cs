using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.Biochemistry;

public enum ChemicalCategory
{
    Unknown = 0,
    Energy,
    Storage,
    Reinforcement,
    Drive,
    Hormone,
    Immune,
    Injury,
    Smell,
    Organ,
    Environment,
    Toxin
}

public readonly record struct ChemicalRange(float NormalMin, float NormalMax, float HardMin, float HardMax);

public sealed record ChemicalDefinition(
    int Id,
    string Token,
    string DisplayName,
    ChemicalCategory Category,
    ChemicalRange Range,
    string DebugColor);

public static class ChemicalCatalog
{
    private static readonly ChemicalDefinition[] Definitions = BuildDefinitions();

    public static IReadOnlyList<ChemicalDefinition> All => Definitions;

    public static ChemicalDefinition Get(int id)
    {
        if ((uint)id >= (uint)Definitions.Length)
            throw new ArgumentOutOfRangeException(nameof(id), id, $"Chemical id must be in 0..{Definitions.Length - 1}.");
        return Definitions[id];
    }

    private static ChemicalDefinition[] BuildDefinitions()
    {
        var definitions = new ChemicalDefinition[BiochemConst.NUMCHEM];
        for (int i = 0; i < definitions.Length; i++)
            definitions[i] = Fallback(i);

        Set(definitions, ChemID.None, "none", "None", ChemicalCategory.Unknown, "#808080");
        Set(definitions, ChemID.Glycogen, "glycogen", "Glycogen", ChemicalCategory.Storage, "#78a64b");
        Set(definitions, ChemID.Starch, "starch", "Starch", ChemicalCategory.Storage, "#a8a052");
        Set(definitions, ChemID.Glucose, "glucose", "Glucose", ChemicalCategory.Energy, "#e6bf4a");
        Set(definitions, ChemID.Adipose, "adipose", "Adipose", ChemicalCategory.Storage, "#d79b4b");
        Set(definitions, ChemID.Muscle, "muscle", "Muscle", ChemicalCategory.Storage, "#c75f5f");
        Set(definitions, ChemID.Reward, "reward", "Reward", ChemicalCategory.Reinforcement, "#63b36c");
        Set(definitions, ChemID.Punishment, "punishment", "Punishment", ChemicalCategory.Reinforcement, "#bf5a5a");
        Set(definitions, ChemID.ReinforcementBase, "reinforcement_base", "Reinforcement Base", ChemicalCategory.Reinforcement, "#7892d4");
        Set(definitions, ChemID.ATP, "atp", "ATP", ChemicalCategory.Energy, "#56a6d6");
        Set(definitions, ChemID.ADP, "adp", "ADP", ChemicalCategory.Energy, "#7793aa");
        Set(definitions, ChemID.Endorphin, "endorphin", "Endorphin", ChemicalCategory.Hormone, "#b772d8");
        Set(definitions, ChemID.Progesterone, "progesterone", "Progesterone", ChemicalCategory.Hormone, "#d97bb0");
        Set(definitions, ChemID.Pain, "pain", "Pain", ChemicalCategory.Drive, "#c84f4f");
        Set(definitions, ChemID.HungerForProtein, "hunger_for_protein", "Hunger For Protein", ChemicalCategory.Drive, "#bd7842");
        Set(definitions, ChemID.HungerForCarb, "hunger_for_carb", "Hunger For Carbohydrate", ChemicalCategory.Drive, "#c7a842");
        Set(definitions, ChemID.HungerForFat, "hunger_for_fat", "Hunger For Fat", ChemicalCategory.Drive, "#c18b48");
        Set(definitions, ChemID.Coldness, "coldness", "Coldness", ChemicalCategory.Drive, "#4f97d8");
        Set(definitions, ChemID.Hotness, "hotness", "Hotness", ChemicalCategory.Drive, "#d8664f");
        Set(definitions, ChemID.Loneliness, "loneliness", "Loneliness", ChemicalCategory.Drive, "#7f74ca");
        Set(definitions, ChemID.Crowdedness, "crowdedness", "Crowdedness", ChemicalCategory.Drive, "#a773bd");
        Set(definitions, ChemID.Fear, "fear", "Fear", ChemicalCategory.Drive, "#8a6f5d");
        Set(definitions, ChemID.Boredom, "boredom", "Boredom", ChemicalCategory.Drive, "#8e8e8e");
        Set(definitions, ChemID.Anger, "anger", "Anger", ChemicalCategory.Drive, "#b54c3d");
        Set(definitions, ChemID.SexDrive, "sex_drive", "Sex Drive", ChemicalCategory.Drive, "#c45f9d");
        Set(definitions, ChemID.Injury, "injury", "Injury", ChemicalCategory.Injury, "#9e2f2f");
        Set(definitions, ChemID.Tiredness, "tiredness", "Tiredness", ChemicalCategory.Drive, "#6f6f9e");
        Set(definitions, ChemID.Sleepiness, "sleepiness", "Sleepiness", ChemicalCategory.Drive, "#575791");

        for (int i = ChemID.FirstAntigen; i <= ChemID.LastAntigen; i++)
            Set(definitions, i, $"antigen{i - ChemID.FirstAntigen}", $"Antigen {i - ChemID.FirstAntigen}", ChemicalCategory.Immune, "#6aaf8d");

        for (int i = ChemID.FirstSmell; i < definitions.Length; i++)
            definitions[i] = definitions[i] with { Category = ChemicalCategory.Smell, DebugColor = "#68a86e" };

        return definitions;
    }

    private static ChemicalDefinition Fallback(int id)
        => new(id, $"chem{id}", $"Chemical {id}", ChemicalCategory.Unknown, UnitRange, "#808080");

    private static void Set(
        ChemicalDefinition[] definitions,
        int id,
        string token,
        string displayName,
        ChemicalCategory category,
        string debugColor)
        => definitions[id] = new(id, token, displayName, category, UnitRange, debugColor);

    private static ChemicalRange UnitRange => new(0.0f, 1.0f, 0.0f, 1.0f);
}

public sealed record ChemicalHalfLifeView(
    int ChemicalId,
    ChemicalDefinition Chemical,
    float DecayRate)
{
    public bool HasDecay => DecayRate > 0.0f && DecayRate < 1.0f;
}
