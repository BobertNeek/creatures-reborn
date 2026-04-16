namespace CreaturesReborn.Sim.World;

/// <summary>
/// Cellular Automata channel indices, matching c2e's 20-channel CA system.
/// Each room has CA_COUNT float values that diffuse to neighbouring rooms
/// each tick, simulating environmental spread of heat, light, nutrients, etc.
/// </summary>
public static class CaIndex
{
    public const int Temperature     = 0;
    public const int HeatSource      = 1;
    public const int Light           = 2;
    public const int LightSource     = 3;
    public const int Rainfall        = 4;
    public const int RainSource      = 5;
    public const int NutrientWater   = 6;
    public const int NutrientSource  = 7;
    public const int Minerals        = 8;
    public const int MineralSource   = 9;
    public const int Radiation       = 10;
    public const int RadiationSource = 11;
    public const int Scent0          = 12;   // navigational smell (e.g. home)
    public const int Scent1          = 13;
    public const int Scent2          = 14;
    public const int Scent3          = 15;
    public const int Scent4          = 16;
    public const int Scent5          = 17;
    public const int Scent6          = 18;
    public const int Scent7          = 19;

    public const int Count = 20;
}
