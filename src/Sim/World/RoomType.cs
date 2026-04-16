namespace CreaturesReborn.Sim.World;

/// <summary>
/// Room types matching the c2e engine RTYP values.
/// These control how the Cellular Automata system treats each room
/// and what environmental behaviours apply.
/// </summary>
public enum RoomType
{
    /// <summary>Standard outdoor room — full CA diffusion, weather-affected.</summary>
    Outdoor = 0,

    /// <summary>Indoor wooden room — moderate insulation.</summary>
    IndoorWood = 1,

    /// <summary>Indoor concrete room — good insulation.</summary>
    IndoorConcrete = 2,

    /// <summary>Underwater room — creatures swim, nutrients diffuse faster.</summary>
    Underwater = 3,

    /// <summary>Soil/earth room — slow diffusion, supports plant roots.</summary>
    Soil = 4,

    /// <summary>Atmosphere/sky room — no floor, open air.</summary>
    Atmosphere = 5,

    /// <summary>Water surface room — interface between water and air.</summary>
    WaterSurface = 6,

    /// <summary>Indoor metal/spaceship room — near-perfect insulation (DS corridor/workshop).</summary>
    IndoorMetal = 7,
}
