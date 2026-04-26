using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.World;

public enum CaChannelCategory
{
    Physical,
    Weather,
    Ecology,
    Danger,
    Scent,
    Unknown,
}

public sealed record CaChannelDefinition(
    int Index,
    string Token,
    string DisplayName,
    CaChannelCategory Category,
    int? SourceChannelIndex,
    int? OutputChannelIndex,
    string Description)
{
    public bool IsSource => OutputChannelIndex.HasValue;
}

public static class CaChannelCatalog
{
    private static readonly CaChannelDefinition[] Definitions =
    {
        new(CaIndex.Temperature, "temperature", "Temperature", CaChannelCategory.Physical, CaIndex.HeatSource, null,
            "Ambient room heat used by creature comfort and environmental systems."),
        new(CaIndex.HeatSource, "heat_source", "Heat Source", CaChannelCategory.Physical, null, CaIndex.Temperature,
            "Source channel that raises room temperature during CA ticks."),
        new(CaIndex.Light, "light", "Light", CaChannelCategory.Physical, CaIndex.LightSource, null,
            "Ambient room light available for perception and plant-style systems."),
        new(CaIndex.LightSource, "light_source", "Light Source", CaChannelCategory.Physical, null, CaIndex.Light,
            "Source channel that raises room light during CA ticks."),
        new(CaIndex.Rainfall, "rainfall", "Rainfall", CaChannelCategory.Weather, CaIndex.RainSource, null,
            "Room rainfall or wetness signal."),
        new(CaIndex.RainSource, "rain_source", "Rain Source", CaChannelCategory.Weather, null, CaIndex.Rainfall,
            "Source channel that raises rainfall during CA ticks."),
        new(CaIndex.NutrientWater, "nutrient_water", "Nutrient Water", CaChannelCategory.Ecology, CaIndex.NutrientSource, null,
            "Ecology channel for water-borne nutrients."),
        new(CaIndex.NutrientSource, "nutrient_source", "Nutrient Source", CaChannelCategory.Ecology, null, CaIndex.NutrientWater,
            "Source channel that raises nutrient water during CA ticks."),
        new(CaIndex.Minerals, "minerals", "Minerals", CaChannelCategory.Ecology, CaIndex.MineralSource, null,
            "Ecology channel for room mineral availability."),
        new(CaIndex.MineralSource, "mineral_source", "Mineral Source", CaChannelCategory.Ecology, null, CaIndex.Minerals,
            "Source channel that raises minerals during CA ticks."),
        new(CaIndex.Radiation, "radiation", "Radiation", CaChannelCategory.Danger, CaIndex.RadiationSource, null,
            "Danger channel for radiation or future environmental toxins."),
        new(CaIndex.RadiationSource, "radiation_source", "Radiation Source", CaChannelCategory.Danger, null, CaIndex.Radiation,
            "Source channel that raises radiation during CA ticks."),
        new(CaIndex.Scent0, "scent_0", "Scent 0", CaChannelCategory.Scent, null, null,
            "Agent or room smell channel for navigation and object search."),
        new(CaIndex.Scent1, "scent_1", "Scent 1", CaChannelCategory.Scent, null, null,
            "Agent or room smell channel for navigation and object search."),
        new(CaIndex.Scent2, "scent_2", "Scent 2", CaChannelCategory.Scent, null, null,
            "Agent or room smell channel for navigation and object search."),
        new(CaIndex.Scent3, "scent_3", "Scent 3", CaChannelCategory.Scent, null, null,
            "Agent or room smell channel for navigation and object search."),
        new(CaIndex.Scent4, "scent_4", "Scent 4", CaChannelCategory.Scent, null, null,
            "Agent or room smell channel for navigation and object search."),
        new(CaIndex.Scent5, "scent_5", "Scent 5", CaChannelCategory.Scent, null, null,
            "Agent or room smell channel for navigation and object search."),
        new(CaIndex.Scent6, "scent_6", "Scent 6", CaChannelCategory.Scent, null, null,
            "Agent or room smell channel for navigation and object search."),
        new(CaIndex.Scent7, "scent_7", "Scent 7", CaChannelCategory.Scent, null, null,
            "Agent or room smell channel for navigation and object search."),
    };

    public static IReadOnlyList<CaChannelDefinition> All => Definitions;

    public static CaChannelDefinition Get(int index)
    {
        if ((uint)index >= CaIndex.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "CA channel index is outside the current catalog.");

        return Definitions[index];
    }
}
