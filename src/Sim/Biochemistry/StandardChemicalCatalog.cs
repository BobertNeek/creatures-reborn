using System;
using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Biochemistry;

public sealed record ChemicalAlias(string Name, string Source);

public sealed record StandardChemicalDefinition(
    int Id,
    string Token,
    string DisplayName,
    ChemicalCategory Category,
    string Source,
    IReadOnlyList<ChemicalAlias> Aliases,
    bool IsKnownC3DsChemical);

public static class StandardChemicalCatalog
{
    public const string CreaturesWikiC3ChemicalList = "https://creatures.wiki/C3_Chemical_List";
    private static readonly StandardChemicalDefinition[] Definitions = BuildDefinitions();

    public static IReadOnlyList<StandardChemicalDefinition> All => Definitions;

    public static StandardChemicalDefinition Get(int id)
    {
        if ((uint)id >= (uint)Definitions.Length)
            throw new ArgumentOutOfRangeException(nameof(id), id, $"Chemical id must be in 0..{Definitions.Length - 1}.");
        return Definitions[id];
    }

    private static StandardChemicalDefinition[] BuildDefinitions()
    {
        var definitions = new StandardChemicalDefinition[BiochemConst.NUMCHEM];
        for (int i = 0; i < definitions.Length; i++)
            definitions[i] = Unknown(i);

        Set(definitions, 0, "none", "None", ChemicalCategory.Unknown);
        Set(definitions, 1, "lactate", "Lactate", ChemicalCategory.Energy);
        Set(definitions, 2, "pyruvate", "Pyruvate", ChemicalCategory.Energy);
        Set(definitions, 3, "glucose", "Glucose", ChemicalCategory.Energy);
        Set(definitions, 4, "glycogen", "Glycogen", ChemicalCategory.Storage);
        Set(definitions, 5, "starch", "Starch", ChemicalCategory.Storage);
        Set(definitions, 6, "fatty_acid", "Fatty Acid", ChemicalCategory.Energy);
        Set(definitions, 7, "cholesterol", "Cholesterol", ChemicalCategory.Storage);
        Set(definitions, 8, "triglyceride", "Triglyceride", ChemicalCategory.Storage);
        Set(definitions, 9, "adipose_tissue", "Adipose Tissue", ChemicalCategory.Storage);
        Set(definitions, 10, "fat", "Fat", ChemicalCategory.Storage);
        Set(definitions, 11, "muscle_tissue", "Muscle Tissue", ChemicalCategory.Storage);
        Set(definitions, 12, "protein", "Protein", ChemicalCategory.Storage);
        Set(definitions, 13, "amino_acid", "Amino Acid", ChemicalCategory.Energy);
        Set(definitions, 17, "downatrophin", "Downatrophin", ChemicalCategory.Drive);
        Set(definitions, 18, "upatrophin", "Upatrophin", ChemicalCategory.Drive);
        Set(definitions, 24, "dissolved_carbon_dioxide", "Dissolved Carbon Dioxide", ChemicalCategory.Environment);
        Set(definitions, 25, "urea", "Urea", ChemicalCategory.Toxin);
        Set(definitions, 26, "ammonia", "Ammonia", ChemicalCategory.Toxin);
        Set(definitions, 29, "air", "Air", ChemicalCategory.Environment);
        Set(definitions, 30, "oxygen", "Oxygen", ChemicalCategory.Energy);
        Set(definitions, 33, "water", "Water", ChemicalCategory.Environment);
        Set(definitions, 34, "energy", "Energy", ChemicalCategory.Energy);
        Set(definitions, 35, "atp", "ATP", ChemicalCategory.Energy);
        Set(definitions, 36, "adp", "ADP", ChemicalCategory.Energy);
        Set(definitions, 39, "arousal_potential", "Arousal Potential", ChemicalCategory.Hormone);
        Set(definitions, 40, "libido_lowerer", "Libido Lowerer", ChemicalCategory.Hormone);
        Set(definitions, 41, "opposite_sex_pheromone", "Opposite Sex Pheromone", ChemicalCategory.Hormone);
        Set(definitions, 46, "oestrogen", "Oestrogen", ChemicalCategory.Hormone);
        Set(definitions, 48, "progesterone", "Progesterone", ChemicalCategory.Hormone);
        Set(definitions, 53, "testosterone", "Testosterone", ChemicalCategory.Hormone);
        Set(definitions, 54, "inhibin", "Inhibin", ChemicalCategory.Hormone);

        Set(definitions, 66, "heavy_metals", "Heavy Metals", ChemicalCategory.Toxin);
        Set(definitions, 67, "cyanide", "Cyanide", ChemicalCategory.Toxin);
        Set(definitions, 68, "belladonna", "Belladonna", ChemicalCategory.Toxin);
        Set(definitions, 69, "geddonase", "Geddonase", ChemicalCategory.Toxin);
        Set(definitions, 70, "glycotoxin", "Glycotoxin", ChemicalCategory.Toxin);
        Set(definitions, 71, "sleep_toxin", "Sleep Toxin", ChemicalCategory.Toxin);
        Set(definitions, 72, "fever_toxin", "Fever Toxin", ChemicalCategory.Toxin);
        Set(definitions, 73, "histamine_a", "Histamine A", ChemicalCategory.Toxin);
        Set(definitions, 74, "histamine_b", "Histamine B", ChemicalCategory.Toxin);
        Set(definitions, 75, "alcohol", "Alcohol", ChemicalCategory.Toxin);
        Set(definitions, 78, "atp_decoupler", "ATP Decoupler", ChemicalCategory.Toxin);
        Set(definitions, 79, "carbon_monoxide", "Carbon Monoxide", ChemicalCategory.Toxin);
        Set(definitions, 80, "fear_toxin", "Fear Toxin", ChemicalCategory.Toxin);
        Set(definitions, 81, "muscle_toxin", "Muscle Toxin", ChemicalCategory.Toxin);

        for (int i = 0; i < 8; i++)
            Set(definitions, 82 + i, $"antigen_{i}", $"Antigen {i}", ChemicalCategory.Immune);
        Set(definitions, 90, "wounded", "Wounded", ChemicalCategory.Injury);

        Set(definitions, 92, "medicine_one", "Medicine One", ChemicalCategory.Immune);
        Set(definitions, 93, "anti_oxidant", "Anti-Oxidant", ChemicalCategory.Immune);
        Set(definitions, 94, "prostaglandin", "Prostaglandin", ChemicalCategory.Immune);
        Set(definitions, 95, "edta", "EDTA", ChemicalCategory.Immune);
        Set(definitions, 96, "sodium_thiosulphate", "Sodium Thiosulphate", ChemicalCategory.Immune);
        Set(definitions, 97, "arnica", "Arnica", ChemicalCategory.Immune);
        Set(definitions, 98, "vitamin_e", "Vitamin E", ChemicalCategory.Immune);
        Set(definitions, 99, "vitamin_c", "Vitamin C", ChemicalCategory.Immune);
        Set(definitions, 100, "antihistamine", "Antihistamine", ChemicalCategory.Immune);
        for (int i = 0; i < 8; i++)
            Set(definitions, 102 + i, $"antibody_{i}", $"Antibody {i}", ChemicalCategory.Immune);

        Set(definitions, 112, "anabolic_steroid", "Anabolic Steroid", ChemicalCategory.Hormone);
        Set(definitions, 113, "pistle", "Pistle", ChemicalCategory.Hormone);
        Set(definitions, 114, "insulin", "Insulin", ChemicalCategory.Hormone);
        Set(definitions, 115, "glycolase", "Glycolase", ChemicalCategory.Hormone);
        Set(definitions, 116, "dehydrogenase", "Dehydrogenase", ChemicalCategory.Hormone);
        Set(definitions, 117, "adrenaline", "Adrenaline", ChemicalCategory.Hormone);
        Set(definitions, 118, "grendel_nitrate", "Grendel Nitrate", ChemicalCategory.Hormone);
        Set(definitions, 119, "ettin_nitrate", "Ettin Nitrate", ChemicalCategory.Hormone);
        Set(definitions, 121, "protease", "Protease", ChemicalCategory.Hormone);
        Set(definitions, 124, "activase", "Activase", ChemicalCategory.Hormone);
        Set(definitions, 125, "life", "Life", ChemicalCategory.Organ);
        Set(definitions, 127, "injury", "Injury", ChemicalCategory.Injury);
        Set(definitions, 128, "stress", "Stress", ChemicalCategory.Drive);
        Set(definitions, 129, "sleepase", "Sleepase", ChemicalCategory.Hormone);
        Set(definitions, 130, "tryptamine", "Tryptamine", ChemicalCategory.Unknown);

        string[] backups =
        [
            "Pain Backup", "Hunger For Protein Backup", "Hunger For Carb Backup", "Hunger For Fat Backup",
            "Coldness Backup", "Hotness Backup", "Tiredness Backup", "Sleepiness Backup", "Loneliness Backup",
            "Crowdedness Backup", "Fear Backup", "Boredom Backup", "Anger Backup", "Sex Drive Backup", "Comfort Drive Backup"
        ];
        for (int i = 0; i < backups.Length; i++)
            Set(definitions, 131 + i, Token(backups[i]), backups[i], ChemicalCategory.Drive);

        string[] drives =
        [
            "Pain", "Hunger For Protein", "Hunger For Carb", "Hunger For Fat", "Coldness", "Hotness",
            "Tiredness", "Sleepiness", "Loneliness", "Crowded", "Fear", "Boredom", "Anger", "Sex Drive", "Comfort"
        ];
        for (int i = 0; i < drives.Length; i++)
            Set(definitions, 148 + i, Token(drives[i]), drives[i], ChemicalCategory.Drive);

        string[] smells =
        [
            "CA Sound", "CA Light", "CA Heat", "CA Water From Sky", "CA Nutrient Plants", "CA Water Bodies",
            "CA Protein", "CA Carbohydrate", "CA Fat", "CA Flowers", "CA Machinery", "CA Creature Egg",
            "CA Norn Smell", "CA Grendel Smell", "CA Ettin Smell", "CA Norn Home Smell",
            "CA Grendel Home Smell", "CA Ettin Home Smell", "CA Gadget Smell", "CA Smell 19"
        ];
        for (int i = 0; i < smells.Length; i++)
            Set(definitions, 165 + i, Token(smells[i]), smells[i], ChemicalCategory.Smell);

        string[] stress =
        [
            "Stress High Hunger For Carbohydrate", "Stress High Hunger For Protein", "Stress High Hunger For Fat",
            "Stress High Anger", "Stress High Fear", "Stress High Pain", "Stress High Tiredness",
            "Stress High Sleepiness", "Stress High Crowdedness"
        ];
        for (int i = 0; i < stress.Length; i++)
            Set(definitions, 187 + i, Token(stress[i]), stress[i], ChemicalCategory.Drive);

        Set(definitions, 198, "disappointment", "Disappointment", ChemicalCategory.Reinforcement);
        Set(definitions, 199, "up", "Up", ChemicalCategory.Reinforcement);
        Set(definitions, 200, "down", "Down", ChemicalCategory.Reinforcement);
        Set(definitions, 201, "exit", "Exit", ChemicalCategory.Reinforcement);
        Set(definitions, 202, "enter", "Enter", ChemicalCategory.Reinforcement);
        Set(definitions, 203, "wait", "Wait", ChemicalCategory.Reinforcement);
        Set(definitions, 204, "reward", "Reward", ChemicalCategory.Reinforcement);
        Set(definitions, 205, "punishment", "Punishment", ChemicalCategory.Reinforcement);
        for (int i = 9; i <= 14; i++)
            Set(definitions, 198 + i - 1, $"brain_chemical_{i}", $"Brain Chemical {i}", ChemicalCategory.Reinforcement);
        Set(definitions, 212, "pre_rem", "Pre-REM", ChemicalCategory.Reinforcement);
        Set(definitions, 213, "rem", "REM", ChemicalCategory.Reinforcement);

        return definitions;
    }

    private static StandardChemicalDefinition Unknown(int id)
        => new(id, $"chemical_{id:D3}", "Unknownase", ChemicalCategory.Unknown, CreaturesWikiC3ChemicalList, [], false);

    private static void Set(StandardChemicalDefinition[] definitions, int id, string token, string name, ChemicalCategory category)
        => definitions[id] = new(id, token, name, category, CreaturesWikiC3ChemicalList, [], true);

    private static string Token(string name)
        => name.ToLowerInvariant()
            .Replace("-", "")
            .Replace(" ", "_")
            .Replace("(", "")
            .Replace(")", "");
}
