using System;
using Godot;

namespace CreaturesReborn.Godot.UI;

[GlobalClass]
public partial class MainMenu : Control
{
    private FileDialog? _loadDialog;

    public override void _Ready()
    {
        BuildMenu();
    }

    private void BuildMenu()
    {
        var background = new ColorRect
        {
            Color = new Color(0.04f, 0.035f, 0.055f),
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        AddChild(background);

        var panel = new Panel
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -210,
            OffsetTop = -170,
            OffsetRight = 210,
            OffsetBottom = 170,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.075f, 0.11f, 0.92f),
            BorderColor = new Color(0.34f, 0.28f, 0.18f),
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
        });
        AddChild(panel);

        var vbox = new VBoxContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 28,
            OffsetTop = 24,
            OffsetRight = -28,
            OffsetBottom = -24,
        };
        vbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(vbox);

        var title = new Label
        {
            Text = "Creatures Reborn",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 26);
        vbox.AddChild(title);

        vbox.AddChild(MakeButton("Play Treehouse", () =>
            GetTree().ChangeSceneToFile("res://scenes/Treehouse.tscn")));
        vbox.AddChild(MakeButton("Metaroom Editor", () =>
            GetTree().ChangeSceneToFile("res://scenes/MetaroomEditor.tscn")));
        vbox.AddChild(MakeButton("Load Metaroom", ShowLoadDialog));
        vbox.AddChild(MakeButton("Quit", () => GetTree().Quit()));

        _loadDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "Load Metaroom JSON",
            InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
        };
        _loadDialog.Filters = new[] { "*.json ; Metaroom JSON" };
        _loadDialog.FileSelected += path =>
        {
            MetaroomEditorSession.SetDefinitionPaths(path);
            GetTree().ChangeSceneToFile("res://scenes/MetaroomWorld.tscn");
        };
        AddChild(_loadDialog);
    }

    private void ShowLoadDialog()
    {
        _loadDialog?.PopupCenteredRatio(0.6f);
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(260, 40),
        };
        button.Pressed += onClick;
        return button;
    }
}
