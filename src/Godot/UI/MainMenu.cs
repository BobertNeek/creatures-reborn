using System;
using System.Collections.Generic;
using Godot;
using CreaturesReborn.Sim.Save;
using CreaturesReborn.Sim.Settings;

namespace CreaturesReborn.Godot.UI;

[GlobalClass]
public partial class MainMenu : Control
{
    private const string BackgroundPath = "res://art/metaroom/imported/ig_08c5f1f9f59ddea70169ed69df04348199aeebee1a267cc1df.png";
    private FileDialog? _loadMetaroomDialog;
    private Control? _activeOverlay;
    private GameSettings _settings = GameSettings.Defaults;

    public override void _Ready()
    {
        AddChild(new DebugScreenshot { Name = "DebugScreenshot" });
        _settings = GameSettingsStore.Load();
        GameSettingsApplier.Apply(_settings);
        BuildMenu();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape } && _activeOverlay != null)
            CloseOverlay();
    }

    private void BuildMenu()
    {
        AddChild(BuildBackground());

        var panel = new Panel
        {
            AnchorLeft = 0.08f,
            AnchorTop = 0.13f,
            AnchorRight = 0.08f,
            AnchorBottom = 0.90f,
            OffsetLeft = 0,
            OffsetRight = 430,
        };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.035f, 0.032f, 0.046f, 0.90f)));
        AddChild(panel);

        var vbox = new VBoxContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 30,
            OffsetTop = 26,
            OffsetRight = -30,
            OffsetBottom = -26,
        };
        vbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(vbox);

        var title = new Label
        {
            Text = "Creatures Reborn",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        title.AddThemeFontSizeOverride("font_size", ScaledFont(34));
        title.Modulate = new Color(0.98f, 0.96f, 0.88f);
        vbox.AddChild(title);
        vbox.AddChild(MakeSeparator());

        Button newGame = MakeMenuButton("New Game", () => GameLaunchSession.StartBundledWorld(GetTree()), primary: true);
        vbox.AddChild(newGame);
        vbox.AddChild(MakeMenuButton("Load Game", ShowSaveSlotBrowser));
        vbox.AddChild(MakeMenuButton("Edit MetaRoom", () =>
        {
            GameLaunchSession.Clear();
            GetTree().ChangeSceneToFile("res://scenes/MetaroomEditor.tscn");
        }));
        vbox.AddChild(MakeMenuButton("Load Metaroom", ShowLoadMetaroomDialog));
        vbox.AddChild(MakeMenuButton("Settings", ShowSharedSettingsOverlay));
        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(1, 14) });
        vbox.AddChild(MakeMenuButton("Exit Game", () => GetTree().Quit(), danger: true));

        BuildLoadDialog();
        newGame.GrabFocus();
    }

    private Control BuildBackground()
    {
        var root = new Control
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        Texture2D? texture = LoadTexture(BackgroundPath)
            ?? LoadTexture("res://art/metaroom/metaroom.png")
            ?? LoadTexture("res://art/metaroom/metaroom-right-connector-v2.png");

        if (texture != null)
        {
            root.AddChild(new TextureRect
            {
                Texture = texture,
                AnchorRight = 1,
                AnchorBottom = 1,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }

        root.AddChild(new ColorRect
        {
            Color = new Color(0.0f, 0.0f, 0.0f, 0.34f),
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        root.AddChild(new ColorRect
        {
            Color = new Color(0.02f, 0.018f, 0.026f, 0.30f),
            AnchorRight = 0.46f,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        return root;
    }

    private void BuildLoadDialog()
    {
        _loadMetaroomDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "Load Metaroom JSON",
            InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
        };
        _loadMetaroomDialog.Filters = new[] { "*.json ; Metaroom JSON" };
        _loadMetaroomDialog.FileSelected += path => GameLaunchSession.StartCustomMetaroom(GetTree(), path);
        AddChild(_loadMetaroomDialog);
    }

    private void ShowLoadMetaroomDialog()
        => _loadMetaroomDialog?.PopupCenteredRatio(0.62f);

    private void ShowSaveSlotBrowser()
    {
        CloseOverlay();
        var panel = OverlayPanel("Load Game", new Vector2(560, 500));
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 8);
        panel.AddChild(list);

        var service = new GameSaveService(ProjectSettings.GlobalizePath("user://saves"));
        IReadOnlyList<SaveSlotSummary> slots = service.ListSlots();
        if (slots.Count == 0)
            list.AddChild(MakeOverlayLabel("No saved games found.", 16));

        foreach (SaveSlotSummary slot in slots)
        {
            string text = slot.IsValid
                ? $"Slot {slot.Slot}: {slot.SlotName}  {slot.WorldLabel}  Creatures: {slot.CreatureCount}"
                : $"{slot.SlotName}: invalid save";
            Button button = MakeOverlayButton(text, () => GameLaunchSession.StartSavedGame(GetTree(), slot.Path));
            button.Disabled = !slot.IsValid;
            list.AddChild(button);
        }

        list.AddChild(new Control { CustomMinimumSize = new Vector2(1, 12) });
        list.AddChild(MakeOverlayButton("Cancel", CloseOverlay));
    }

    private void ShowSharedSettingsOverlay()
    {
        CloseOverlay();
        SettingsOverlay overlay = SettingsOverlay.Create(
            _settings,
            settings => _settings = settings,
            CloseOverlay);
        _activeOverlay = overlay;
        AddChild(overlay);
    }

    private VBoxContainer OverlayPanel(string title, Vector2 size)
    {
        var root = new Control
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Stop,
        };
        root.AddChild(new ColorRect
        {
            Color = new Color(0, 0, 0, 0.50f),
            AnchorRight = 1,
            AnchorBottom = 1,
        });

        var panel = new Panel
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -size.X * 0.5f,
            OffsetTop = -size.Y * 0.5f,
            OffsetRight = size.X * 0.5f,
            OffsetBottom = size.Y * 0.5f,
        };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.045f, 0.041f, 0.060f, 0.96f)));
        root.AddChild(panel);

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

        var heading = MakeOverlayLabel(title, 24);
        heading.HorizontalAlignment = HorizontalAlignment.Left;
        vbox.AddChild(heading);
        _activeOverlay = root;
        AddChild(root);
        return vbox;
    }

    private void CloseOverlay()
    {
        if (_activeOverlay == null)
            return;
        RemoveChild(_activeOverlay);
        _activeOverlay.QueueFree();
        _activeOverlay = null;
    }

    private Button MakeMenuButton(string text, Action onClick, bool primary = false, bool danger = false)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(340, 52),
            FocusMode = FocusModeEnum.All,
        };
        button.AddThemeFontSizeOverride("font_size", ScaledFont(19));
        button.AddThemeStyleboxOverride("normal", ButtonStyle(primary, danger, hover: false));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(primary, danger, hover: true));
        button.AddThemeStyleboxOverride("focus", FocusStyle());
        button.Pressed += onClick;
        return button;
    }

    private static Button MakeOverlayButton(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(160, 42),
            FocusMode = FocusModeEnum.All,
        };
        button.Pressed += onClick;
        return button;
    }

    private static Label MakeOverlayLabel(string text, int size)
    {
        var label = new Label
        {
            Text = text,
            Modulate = new Color(0.94f, 0.94f, 0.90f),
        };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private int ScaledFont(int baseSize)
        => Math.Max(10, (int)MathF.Round(baseSize * _settings.TextScale));

    private static HSeparator MakeSeparator()
        => new() { CustomMinimumSize = new Vector2(1, 8) };

    private static StyleBoxFlat PanelStyle(Color bg)
        => new()
        {
            BgColor = bg,
            BorderColor = new Color(0.56f, 0.42f, 0.22f, 0.95f),
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 16,
            ContentMarginBottom = 16,
        };

    private static StyleBoxFlat ButtonStyle(bool primary, bool danger, bool hover)
        => new()
        {
            BgColor = danger
                ? new Color(0.18f, 0.08f, 0.08f, hover ? 0.98f : 0.86f)
                : primary
                    ? new Color(0.26f, 0.20f, 0.10f, hover ? 0.98f : 0.90f)
                    : new Color(0.08f, 0.075f, 0.095f, hover ? 0.96f : 0.82f),
            BorderColor = primary
                ? new Color(0.93f, 0.71f, 0.32f, 0.95f)
                : new Color(0.36f, 0.31f, 0.23f, 0.85f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
        };

    private static StyleBoxFlat FocusStyle()
        => new()
        {
            BgColor = new Color(0.33f, 0.25f, 0.12f, 0.98f),
            BorderColor = new Color(1.0f, 0.90f, 0.62f, 1.0f),
            BorderWidthBottom = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
        };

    private static Texture2D? LoadTexture(string path)
    {
        if (ResourceLoader.Exists(path))
            return GD.Load<Texture2D>(path);

        string localPath = ProjectSettings.GlobalizePath(path);
        if (!System.IO.File.Exists(localPath))
            return null;

        Image image = Image.LoadFromFile(localPath);
        return image == null ? null : ImageTexture.CreateFromImage(image);
    }
}
