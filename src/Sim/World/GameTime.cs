namespace CreaturesReborn.Sim.World;

/// <summary>
/// Tracks in-game time matching DS's day/season/year cycle.
/// Default settings from DS: 20-minute days, 4 days per season, 4 seasons per year.
/// </summary>
public sealed class GameTime
{
    // ── Configuration (from DS !DS_game variables.cos) ──────────────────────
    public float MinutesPerDay    { get; set; } = 20.0f;
    public int   DaysPerSeason    { get; set; } = 4;
    public int   SeasonsPerYear   { get; set; } = 4;

    // ── State ───────────────────────────────────────────────────────────────
    public int   WorldTick   { get; private set; }  // total ticks since world creation
    public int   Season      { get; private set; }  // 0=Spring, 1=Summer, 2=Autumn, 3=Winter
    public int   Day         { get; private set; }
    public int   Year        { get; private set; }
    public float TimeOfDay   { get; private set; }  // 0.0-1.0 (0=midnight, 0.5=noon)

    // Derived
    private float TicksPerDay    => MinutesPerDay * 60.0f * 20.0f;  // at 20 Hz
    private float TicksPerSeason => TicksPerDay * DaysPerSeason;
    private float TicksPerYear   => TicksPerSeason * SeasonsPerYear;

    public bool IsDay   => TimeOfDay >= 0.25f && TimeOfDay < 0.75f;
    public bool IsNight => !IsDay;

    public string SeasonName => Season switch
    {
        0 => "Spring",
        1 => "Summer",
        2 => "Autumn",
        3 => "Winter",
        _ => "Unknown"
    };

    public void Tick()
    {
        WorldTick++;
        float dayTicks = TicksPerDay;
        TimeOfDay = (WorldTick % (int)dayTicks) / dayTicks;
        Day       = (int)(WorldTick / dayTicks);
        Season    = (int)((WorldTick / TicksPerSeason) % SeasonsPerYear);
        Year      = (int)(WorldTick / TicksPerYear);
    }
}
