using System;

namespace CreaturesReborn.Sim.Settings;

public enum GameWindowMode
{
    Windowed,
    Fullscreen,
    Borderless,
}

public sealed record GameSettings
{
    public GameWindowMode WindowMode { get; init; } = GameWindowMode.Windowed;
    public bool VSync { get; init; } = true;
    public int FpsCap { get; init; } = 0;
    public float UiScale { get; init; } = 1.0f;
    public float TextScale { get; init; } = 1.0f;
    public bool HighContrast { get; init; }
    public bool ReducedMotion { get; init; }
    public float MasterVolume { get; init; } = 1.0f;
    public bool Mute { get; init; }
    public int MaxCreatures { get; init; } = 16;
    public int BreedingLimit { get; init; } = 6;
    public float SimulationSpeed { get; init; } = 20.0f;
    public float GravityStrength { get; init; } = 18.0f;

    public static GameSettings Defaults { get; } = new();

    public GameSettings Normalize()
        => this with
        {
            FpsCap = Math.Clamp(FpsCap, 0, 240),
            UiScale = Math.Clamp(UiScale, 0.75f, 2.0f),
            TextScale = Math.Clamp(TextScale, 0.85f, 2.0f),
            MasterVolume = Math.Clamp(MasterVolume, 0.0f, 1.0f),
            MaxCreatures = Math.Clamp(MaxCreatures, 1, 128),
            BreedingLimit = Math.Clamp(BreedingLimit, 0, 64),
            SimulationSpeed = Math.Clamp(SimulationSpeed, 1.0f, 120.0f),
            GravityStrength = Math.Clamp(GravityStrength, 0.0f, 80.0f),
        };
}
