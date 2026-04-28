using System;
using Godot;
using CreaturesReborn.Sim.Settings;

namespace CreaturesReborn.Godot.UI;

public static class GameSettingsStore
{
    private const string SettingsPath = "user://settings.cfg";

    public static GameSettings Load()
    {
        var config = new ConfigFile();
        Error err = config.Load(SettingsPath);
        if (err != Error.Ok)
            return GameSettings.Defaults;

        return new GameSettings
        {
            WindowMode = ParseWindowMode(config.GetValue("graphics", "window_mode", GameWindowMode.Windowed.ToString()).AsString()),
            VSync = config.GetValue("graphics", "vsync", true).AsBool(),
            FpsCap = config.GetValue("graphics", "fps_cap", 0).AsInt32(),
            UiScale = (float)config.GetValue("graphics", "ui_scale", 1.0f).AsDouble(),
            TextScale = (float)config.GetValue("graphics", "text_scale", 1.0f).AsDouble(),
            HighContrast = config.GetValue("graphics", "high_contrast", false).AsBool(),
            ReducedMotion = config.GetValue("graphics", "reduced_motion", false).AsBool(),
            MasterVolume = (float)config.GetValue("sound", "master_volume", 1.0f).AsDouble(),
            Mute = config.GetValue("sound", "mute", false).AsBool(),
            MaxCreatures = config.GetValue("simulation", "max_creatures", 16).AsInt32(),
            BreedingLimit = config.GetValue("simulation", "breeding_limit", 6).AsInt32(),
            SimulationSpeed = (float)config.GetValue("simulation", "simulation_speed", 20.0f).AsDouble(),
            GravityStrength = (float)config.GetValue("simulation", "gravity_strength", 18.0f).AsDouble(),
        }.Normalize();
    }

    public static void Save(GameSettings settings)
    {
        settings = settings.Normalize();
        var config = new ConfigFile();
        config.SetValue("graphics", "window_mode", settings.WindowMode.ToString());
        config.SetValue("graphics", "vsync", settings.VSync);
        config.SetValue("graphics", "fps_cap", settings.FpsCap);
        config.SetValue("graphics", "ui_scale", settings.UiScale);
        config.SetValue("graphics", "text_scale", settings.TextScale);
        config.SetValue("graphics", "high_contrast", settings.HighContrast);
        config.SetValue("graphics", "reduced_motion", settings.ReducedMotion);
        config.SetValue("sound", "master_volume", settings.MasterVolume);
        config.SetValue("sound", "mute", settings.Mute);
        config.SetValue("simulation", "max_creatures", settings.MaxCreatures);
        config.SetValue("simulation", "breeding_limit", settings.BreedingLimit);
        config.SetValue("simulation", "simulation_speed", settings.SimulationSpeed);
        config.SetValue("simulation", "gravity_strength", settings.GravityStrength);
        config.Save(SettingsPath);
    }

    private static GameWindowMode ParseWindowMode(string value)
        => Enum.TryParse(value, ignoreCase: true, out GameWindowMode mode)
            ? mode
            : GameWindowMode.Windowed;
}
