using Godot;
using CreaturesReborn.Sim.Settings;

namespace CreaturesReborn.Godot.UI;

public static class GameSettingsApplier
{
    public static void Apply(GameSettings settings)
    {
        settings = settings.Normalize();

        DisplayServer.WindowSetFlag(
            DisplayServer.WindowFlags.Borderless,
            settings.WindowMode == GameWindowMode.Borderless);

        DisplayServer.WindowSetMode(settings.WindowMode == GameWindowMode.Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);

        DisplayServer.WindowSetVsyncMode(settings.VSync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);

        Engine.MaxFps = settings.FpsCap;

        int master = AudioServer.GetBusIndex("Master");
        if (master >= 0)
        {
            AudioServer.SetBusVolumeLinear(master, settings.MasterVolume);
            AudioServer.SetBusMute(master, settings.Mute);
        }
    }

    public static string HardwareStatus()
        => $"{RenderingServer.GetVideoAdapterName()} / {RenderingServer.GetRenderingDevice()?.GetDeviceName() ?? "default renderer"}";
}
