using System;
using Godot;
using CreaturesReborn.Sim.Settings;

namespace CreaturesReborn.Godot.UI;

public partial class SettingsOverlay : Control
{
    private Action<GameSettings>? _onApplied;
    private Action? _onClosed;

    private OptionButton? _windowMode;
    private CheckButton? _vsync;
    private SpinBox? _fpsCap;
    private HSlider? _uiScale;
    private HSlider? _textScale;
    private CheckButton? _highContrast;
    private CheckButton? _reducedMotion;
    private HSlider? _masterVolume;
    private CheckButton? _mute;
    private SpinBox? _maxCreatures;
    private SpinBox? _breedingLimit;
    private SpinBox? _simulationSpeed;
    private SpinBox? _gravityStrength;

    public static SettingsOverlay Create(GameSettings initial, Action<GameSettings> onApplied, Action onClosed)
    {
        var overlay = new SettingsOverlay();
        overlay.Build(initial.Normalize(), onApplied, onClosed);
        return overlay;
    }

    private void Build(GameSettings initial, Action<GameSettings> onApplied, Action onClosed)
    {
        _onApplied = onApplied;
        _onClosed = onClosed;
        ProcessMode = ProcessModeEnum.Always;
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Stop;

        AddChild(new ColorRect
        {
            Color = new Color(0, 0, 0, 0.54f),
            AnchorRight = 1,
            AnchorBottom = 1,
        });

        var panel = new Panel
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -360,
            OffsetTop = -310,
            OffsetRight = 360,
            OffsetBottom = 310,
        };
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        AddChild(panel);

        var vbox = new VBoxContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 24,
            OffsetTop = 20,
            OffsetRight = -24,
            OffsetBottom = -20,
        };
        vbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(vbox);

        vbox.AddChild(MakeLabel("Settings", 24));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 9);
        vbox.AddChild(grid);

        _windowMode = new OptionButton();
        _windowMode.AddItem("Windowed", (int)GameWindowMode.Windowed);
        _windowMode.AddItem("Fullscreen", (int)GameWindowMode.Fullscreen);
        _windowMode.AddItem("Borderless", (int)GameWindowMode.Borderless);
        _windowMode.Selected = (int)initial.WindowMode;
        AddSetting(grid, "Display Mode", _windowMode);

        _vsync = Check(initial.VSync);
        AddSetting(grid, "VSync", _vsync);
        _fpsCap = Spin(initial.FpsCap, 0, 240, 1);
        AddSetting(grid, "FPS Cap", _fpsCap);
        _uiScale = Slider(initial.UiScale, 0.75, 2.0, 0.05);
        AddSetting(grid, "UI Scale", _uiScale);
        _textScale = Slider(initial.TextScale, 0.85, 2.0, 0.05);
        AddSetting(grid, "Text Scale", _textScale);
        _highContrast = Check(initial.HighContrast);
        AddSetting(grid, "High Contrast", _highContrast);
        _reducedMotion = Check(initial.ReducedMotion);
        AddSetting(grid, "Reduced Motion", _reducedMotion);
        _masterVolume = Slider(initial.MasterVolume, 0, 1, 0.01);
        AddSetting(grid, "Master Volume", _masterVolume);
        _mute = Check(initial.Mute);
        AddSetting(grid, "Mute", _mute);
        _maxCreatures = Spin(initial.MaxCreatures, 1, 128, 1);
        AddSetting(grid, "Max Creatures", _maxCreatures);
        _breedingLimit = Spin(initial.BreedingLimit, 0, 64, 1);
        AddSetting(grid, "Breeding Limit", _breedingLimit);
        _simulationSpeed = Spin(initial.SimulationSpeed, 1, 120, 1);
        AddSetting(grid, "Simulation Speed", _simulationSpeed);
        _gravityStrength = Spin(initial.GravityStrength, 0, 80, 0.5);
        AddSetting(grid, "Gravity Strength", _gravityStrength);

        var hardware = MakeLabel(
            $"Hardware Acceleration: {GameSettingsApplier.HardwareStatus()}\nRendering backend changes require relaunch/project configuration.",
            12);
        hardware.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(hardware);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(buttons);
        buttons.AddChild(MakeButton("Save", SaveDraft));
        buttons.AddChild(MakeButton("Reset", ResetDraft));
        buttons.AddChild(MakeButton("OK", () =>
        {
            SaveDraft();
            Close();
        }));
        buttons.AddChild(MakeButton("Cancel", Close));
    }

    private void SaveDraft()
    {
        GameSettings settings = ReadDraft();
        GameSettingsStore.Save(settings);
        GameSettingsApplier.Apply(settings);
        _onApplied?.Invoke(settings);
    }

    private void ResetDraft()
    {
        ClearChildren();
        Build(GameSettings.Defaults, _onApplied ?? (_ => { }), _onClosed ?? (() => { }));
    }

    private void Close()
    {
        _onClosed?.Invoke();
    }

    private GameSettings ReadDraft()
        => new GameSettings
        {
            WindowMode = (GameWindowMode)(_windowMode?.Selected ?? 0),
            VSync = _vsync?.ButtonPressed ?? true,
            FpsCap = (int)(_fpsCap?.Value ?? 0),
            UiScale = (float)(_uiScale?.Value ?? 1.0),
            TextScale = (float)(_textScale?.Value ?? 1.0),
            HighContrast = _highContrast?.ButtonPressed ?? false,
            ReducedMotion = _reducedMotion?.ButtonPressed ?? false,
            MasterVolume = (float)(_masterVolume?.Value ?? 1.0),
            Mute = _mute?.ButtonPressed ?? false,
            MaxCreatures = (int)(_maxCreatures?.Value ?? 16),
            BreedingLimit = (int)(_breedingLimit?.Value ?? 6),
            SimulationSpeed = (float)(_simulationSpeed?.Value ?? 20.0),
            GravityStrength = (float)(_gravityStrength?.Value ?? 18.0),
        }.Normalize();

    private void ClearChildren()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
    }

    private static void AddSetting(GridContainer grid, string label, Control control)
    {
        grid.AddChild(MakeLabel(label, 13));
        control.CustomMinimumSize = new Vector2(280, 34);
        grid.AddChild(control);
    }

    private static CheckButton Check(bool value) => new() { ButtonPressed = value };

    private static HSlider Slider(double value, double min, double max, double step)
        => new()
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

    private static SpinBox Spin(double value, double min, double max, double step)
        => new()
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

    private static Label MakeLabel(string text, int size)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.Modulate = new Color(0.94f, 0.94f, 0.90f);
        return label;
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(138, 40),
            FocusMode = FocusModeEnum.All,
        };
        button.Pressed += onClick;
        return button;
    }

    private static StyleBoxFlat PanelStyle()
        => new()
        {
            BgColor = new Color(0.045f, 0.041f, 0.060f, 0.97f),
            BorderColor = new Color(0.56f, 0.42f, 0.22f, 0.95f),
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
        };
}
