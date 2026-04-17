using System;
using Godot;
using CreaturesReborn.Godot;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using C = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Godot.UI;

/// <summary>
/// Debug HUD: drive bars, WTA decision, top-10 chemical concentrations,
/// and a simple lobe activation heatmap.
/// Built fully in code; attach to a CanvasLayer in the scene.
/// </summary>
[GlobalClass]
public partial class DebugHud : Control
{
    private static readonly string[] DriveNames =
    {
        "Pain", "Hunger:P", "Hunger:C", "Hunger:F",
        "Cold", "Hot", "Tired", "Sleep",
        "Lonely", "Crowd", "Fear", "Bored",
        "Anger", "Sex", "Comfort", "Up",
        "Down", "Exit", "Enter", "Wait",
    };

    private static readonly string[] VerbNames =
    {
        "Default", "Activate1", "Activate2", "Deactivate",
        "Approach", "Retreat", "Get", "Drop",
        "ExpressNeed", "Rest", "WalkW", "WalkE",
        "Eat", "Hit",
    };

    // -------------------------------------------------------------------------
    // Widgets
    // -------------------------------------------------------------------------
    private readonly ProgressBar[] _driveBars  = new ProgressBar[DriveId.NumDrives];
    private Label  _wtaLabel   = null!;
    private Label  _chemLabel  = null!;
    private Label  _lobeLabel  = null!;

    // The creature we're watching (set from the scene root or by script)
    private C? _target;

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------
    public void SetTarget(C? creature) => _target = creature;

    public override void _Ready()
    {
        // Semi-transparent panel background
        var panel = new Panel();
        panel.SetAnchorsPreset(LayoutPreset.TopLeft);
        panel.Size = new Vector2(260, 680);
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0, 0, 0, 0.55f);
        style.ContentMarginLeft = style.ContentMarginTop =
            style.ContentMarginRight = style.ContentMarginBottom = 6;
        panel.AddThemeStyleboxOverride("panel", style);
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.Size = new Vector2(248, 668);
        vbox.Position = new Vector2(6, 6);
        panel.AddChild(vbox);

        // Drive bars
        var drivesHeader = MakeLabel("─── Drives ───", bold: true);
        vbox.AddChild(drivesHeader);

        for (int i = 0; i < DriveId.NumDrives; i++)
        {
            var row = new HBoxContainer();
            var lbl = MakeLabel(DriveNames[i].PadRight(10), size: 9);
            lbl.CustomMinimumSize = new Vector2(70, 0);
            row.AddChild(lbl);

            var bar = new ProgressBar();
            bar.MinValue         = 0;
            bar.MaxValue         = 1;
            bar.Value            = 0;
            bar.CustomMinimumSize = new Vector2(160, 12);
            bar.ShowPercentage   = false;
            row.AddChild(bar);
            _driveBars[i] = bar;
            vbox.AddChild(row);
        }

        // WTA decision
        vbox.AddChild(MakeLabel("─── Decision ───", bold: true));
        _wtaLabel = MakeLabel("Verb: — | Noun: —");
        vbox.AddChild(_wtaLabel);

        // Chemical concentrations
        vbox.AddChild(MakeLabel("─── Chems (top 10) ───", bold: true));
        _chemLabel = MakeLabel("—");
        _chemLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(_chemLabel);

        // Lobe heatmap (text)
        vbox.AddChild(MakeLabel("─── Lobes ───", bold: true));
        _lobeLabel = MakeLabel("—");
        _lobeLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(_lobeLabel);
    }

    // -------------------------------------------------------------------------
    // Per-frame update
    // -------------------------------------------------------------------------
    public override void _Process(double delta)
    {
        // Auto-discover target from scene on first frame
        if (_target == null)
        {
            // Try both old and new scene structures
            var sceneRoot = GetTree().Root.GetNodeOrNull<Node>("VerticalSlice")
                         ?? GetTree().Root.GetNodeOrNull<Node>("NornColony")
                         ?? GetTree().Root.GetNodeOrNull<Node>("Colony");
            var cn = sceneRoot?.GetNodeOrNull<CreatureNode>("Norn");
            if (cn?.Creature != null) _target = cn.Creature;
            return;
        }

        UpdateDrives();
        UpdateDecision();
        UpdateChems();
        UpdateLobes();
    }

    private void UpdateDrives()
    {
        for (int i = 0; i < DriveId.NumDrives; i++)
        {
            float v = _target!.GetDriveLevel(i);
            _driveBars[i].Value = Math.Clamp(v, 0, 1);

            // Tint bar colour: green → yellow → red
            var bar = _driveBars[i];
            var fill = new StyleBoxFlat();
            var r = (float)Math.Clamp(v * 2.0, 0, 1);
            var g = (float)Math.Clamp(2.0 - v * 2.0, 0, 1);
            fill.BgColor = new Color(r, g, 0.1f, 0.85f);
            bar.AddThemeStyleboxOverride("fill", fill);
        }
    }

    private void UpdateDecision()
    {
        int verb = _target!.Motor.CurrentVerb;
        int noun = _target.Motor.CurrentNoun;
        string vName = (verb >= 0 && verb < VerbNames.Length) ? VerbNames[verb] : verb.ToString();
        _wtaLabel.Text = $"Verb: {vName} ({verb}) | Noun: {noun}";
    }

    private void UpdateChems()
    {
        // Show top-10 by concentration, skip chem 0 (null chemical)
        var span = _target!.Biochemistry.GetChemicalConcs();
        Span<(float v, int i)> top = stackalloc (float, int)[10];
        int filled = 0;

        for (int c = 1; c < 256; c++)
        {
            float v = span[c];
            if (v < 0.005f) continue;
            if (filled < 10)
            {
                top[filled++] = (v, c);
            }
            else
            {
                // Replace smallest
                int minIdx = 0;
                for (int k = 1; k < 10; k++)
                    if (top[k].v < top[minIdx].v) minIdx = k;
                if (v > top[minIdx].v)
                    top[minIdx] = (v, c);
            }
        }

        // Sort descending
        for (int a = 0; a < filled - 1; a++)
            for (int b = a + 1; b < filled; b++)
                if (top[b].v > top[a].v) { var tmp = top[a]; top[a] = top[b]; top[b] = tmp; }

        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < filled; k++)
            sb.Append($"[{top[k].i}]={top[k].v:F3}  ");

        _chemLabel.Text = filled > 0 ? sb.ToString() : "(all zero)";
    }

    private void UpdateLobes()
    {
        var brain = _target!.Brain;
        if (brain.LobeCount == 0) { _lobeLabel.Text = "(no lobes)"; return; }

        var sb = new System.Text.StringBuilder();
        for (int l = 0; l < Math.Min(brain.LobeCount, 8); l++)
        {
            var lobe = brain.GetLobe(l);
            if (lobe == null) continue;
            int n = lobe.GetNoOfNeurons();

            float maxAct = 0;
            for (int i = 0; i < n; i++)
            {
                float s = lobe.GetNeuronState(i, 0);   // state variable 0 = output
                if (s > maxAct) maxAct = s;
            }

            // Render token as 4-char ASCII
            int tok = lobe.Token;
            char c0 = (char)(tok & 0xFF), c1 = (char)((tok >> 8) & 0xFF),
                 c2 = (char)((tok >> 16) & 0xFF), c3 = (char)((tok >> 24) & 0xFF);
            string name = $"{c0}{c1}{c2}{c3}";

            // Bar: 8 █ chars
            int bars = (int)(maxAct * 8.0f);
            string bar = new string('█', bars) + new string('░', 8 - bars);
            sb.AppendLine($"{name} [{n,3}] {bar} {maxAct:F2}");
        }
        _lobeLabel.Text = sb.ToString();
    }

    // -------------------------------------------------------------------------
    private static Label MakeLabel(string text, bool bold = false, int size = 10)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", size);
        if (bold)
        {
            // No bold font override available without a FontFile; use ALL_CAPS as visual substitute
            lbl.Text = text.ToUpperInvariant();
        }
        lbl.Modulate = new Color(0.9f, 0.95f, 1.0f);
        return lbl;
    }
}
