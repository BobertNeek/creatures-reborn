using System;
using System.Linq;
using Godot;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Save;
using CreaturesReborn.Sim.Settings;
using C = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Godot.UI;

/// <summary>
/// Main game GUI overlay — combines creature status, world info, and controls.
/// Port of DS's GUI system:
///   - Top-left: world time/population info
///   - Top-right: selected creature status (drives, health)
///   - Bottom: action buttons (tickle, slap, pick up, inject egg)
///   - Options menu
///
/// Built entirely in code, no scene file needed.
/// </summary>
[GlobalClass]
public partial class GameGui : Control
{
    // ── Panels ──────────────────────────────────────────────────────────────
    private Panel?       _worldPanel;
    private Panel?       _creaturePanel;
    private Panel?       _actionBar;
    private Label?       _worldLabel;
    private Label?       _creatureName;
    private Label?       _creatureInfo;
    private ProgressBar? _healthBar;
    private ProgressBar? _hungerBar;
    private ProgressBar? _sleepBar;
    private ProgressBar? _happyBar;
    private Label?       _verbLabel;
    private Label?       _populationLabel;
    private LineEdit?    _speechInput;
    private Label?       _speechLog;
    private Control?     _pauseOverlay;
    private VBoxContainer? _pauseContent;
    private SettingsOverlay? _settingsOverlay;
    private AdvancedToolsOverlay? _advancedToolsOverlay;

    // ── State ───────────────────────────────────────────────────────────────
    private PointerAgent? _pointer;
    private WorldNode?    _world;
    private int _creatureIndex;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        BuildWorldInfoPanel();
        BuildCreaturePanel();
        BuildActionBar();

        if (ShouldOpenAdvancedToolsOnStartup())
            GetTree().CreateTimer(0.25).Timeout += ShowAdvancedToolsOverlay;
    }

    public override void _Process(double delta)
    {
        // Auto-discover world + pointer
        if (_world == null)
        {
            _world = FindWorldNode();
            _pointer = FindPointerAgent();
        }

        UpdateWorldInfo();
        UpdateCreaturePanel();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Tab cycles through creatures
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Tab)
        {
            CycleCreature();
        }

        if (@event is InputEventKey toolsKey && toolsKey.Pressed && !toolsKey.Echo && toolsKey.Keycode == Key.F2)
        {
            ShowAdvancedToolsOverlay();
            GetViewport().SetInputAsHandled();
        }

        if (@event is InputEventKey menuKey && menuKey.Pressed && menuKey.Keycode == Key.Escape)
        {
            if (_settingsOverlay != null)
                CloseSettingsOverlay();
            else if (_advancedToolsOverlay != null)
                CloseAdvancedToolsOverlay();
            else if (_pauseOverlay == null)
                ShowPauseOverlay();
            else
                ClosePauseOverlay();
        }
    }

    // ── World Info Panel (top-left) ─────────────────────────────────────────
    private void BuildWorldInfoPanel()
    {
        _worldPanel = MakePanel(new Vector2(10, 10), new Vector2(200, 80));
        AddChild(_worldPanel);

        var vbox = new VBoxContainer();
        vbox.Position = new Vector2(8, 8);
        _worldPanel.AddChild(vbox);

        _worldLabel = MakeLabel("World: --");
        vbox.AddChild(_worldLabel);
        _populationLabel = MakeLabel("Population: 0");
        vbox.AddChild(_populationLabel);
    }

    private void UpdateWorldInfo()
    {
        if (_world == null || _worldLabel == null) return;

        string tod = _world.TimeOfDay < 0.25f ? "Night" :
                     _world.TimeOfDay < 0.5f  ? "Morning" :
                     _world.TimeOfDay < 0.75f ? "Afternoon" : "Evening";

        _worldLabel.Text = $"{_world.SeasonName} Day {_world.Day + 1} - {tod}";
        _populationLabel!.Text = $"Population: {_world.CreatureCount}  Tick: {_world.TotalTicks}";
    }

    // ── Creature Panel (top-right) ──────────────────────────────────────────
    private void BuildCreaturePanel()
    {
        _creaturePanel = MakePanel(new Vector2(-280, 10), new Vector2(270, 220));
        // Anchor to top-right
        _creaturePanel.SetAnchorsPreset(LayoutPreset.TopRight);
        _creaturePanel.Position = new Vector2(-280, 10);
        _creaturePanel.Size = new Vector2(270, 220);
        AddChild(_creaturePanel);

        var vbox = new VBoxContainer();
        vbox.Position = new Vector2(8, 8);
        _creaturePanel.AddChild(vbox);

        _creatureName = MakeLabel("No creature selected", 12);
        vbox.AddChild(_creatureName);

        vbox.AddChild(MakeLabel(""));  // spacer

        // Drive bars
        var hungerRow = MakeBarRow("Hunger", out _hungerBar);
        vbox.AddChild(hungerRow);

        var sleepRow = MakeBarRow("Tiredness", out _sleepBar);
        vbox.AddChild(sleepRow);

        var healthRow = MakeBarRow("Health", out _healthBar);
        vbox.AddChild(healthRow);

        var happyRow = MakeBarRow("Happiness", out _happyBar);
        vbox.AddChild(happyRow);

        vbox.AddChild(MakeLabel(""));

        _verbLabel = MakeLabel("Action: --");
        vbox.AddChild(_verbLabel);

        _creatureInfo = MakeLabel("Info: --", 9);
        _creatureInfo.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(_creatureInfo);
    }

    private void UpdateCreaturePanel()
    {
        C? creature = GetSelectedCreature();
        if (creature == null)
        {
            if (_creatureName != null) _creatureName.Text = "[Tab] to select a creature";
            return;
        }

        if (_creatureName != null)
            _creatureName.Text = $"Norn #{_creatureIndex + 1}";

        // Hunger = average of the 3 hunger drives
        float hunger = (creature.GetDriveLevel(DriveId.HungerForProtein)
                      + creature.GetDriveLevel(DriveId.HungerForCarb)
                      + creature.GetDriveLevel(DriveId.HungerForFat)) / 3f;
        SetBar(_hungerBar, hunger);

        // Tiredness
        SetBar(_sleepBar, creature.GetDriveLevel(DriveId.Tiredness));

        // Health = 1 - pain
        float health = 1.0f - creature.GetDriveLevel(DriveId.Pain);
        SetBar(_healthBar, health, true);

        // Happiness = 1 - average(boredom, loneliness, fear)
        float unhappy = (creature.GetDriveLevel(DriveId.Boredom)
                       + creature.GetDriveLevel(DriveId.Loneliness)
                       + creature.GetDriveLevel(DriveId.Fear)) / 3f;
        SetBar(_happyBar, 1.0f - unhappy, true);

        // Current verb
        int verb = creature.Motor.CurrentVerb;
        string[] verbNames = {
            "Idle", "Push", "Pull", "Stop", "Approach", "Retreat",
            "Get", "Drop", "Express", "Rest", "Walk W", "Walk E", "Eat", "Hit" };
        string vName = verb >= 0 && verb < verbNames.Length ? verbNames[verb] : $"#{verb}";
        if (_verbLabel != null) _verbLabel.Text = $"Action: {vName}";

        // Key chemicals
        float glyc = creature.GetChemical(ChemID.Glycogen);
        float atp  = creature.GetChemical(ChemID.ATP);
        if (_creatureInfo != null)
            _creatureInfo.Text = $"ATP: {atp:F2}  Glyc: {glyc:F2}";
    }

    // ── Action Bar (bottom-center) ──────────────────────────────────────────
    private void BuildActionBar()
    {
        _actionBar = MakePanel(new Vector2(0, 0), new Vector2(400, 50));
        _actionBar.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        _actionBar.OffsetLeft = 0;
        _actionBar.OffsetRight = 0;
        _actionBar.OffsetTop = -55;
        _actionBar.OffsetBottom = -5;
        AddChild(_actionBar);

        var hbox = new HBoxContainer();
        hbox.Position = new Vector2(8, 8);
        hbox.SetAnchorsPreset(LayoutPreset.FullRect);
        hbox.AddThemeConstantOverride("separation", 10);
        _actionBar.AddChild(hbox);

        hbox.AddChild(MakeButton("Tickle", () => DoTickle()));
        hbox.AddChild(MakeButton("Slap", () => DoSlap()));
        hbox.AddChild(MakeButton("Hatch Egg", () => DoHatchEgg()));
        hbox.AddChild(MakeButton("Feed", () => DoFeed()));
        hbox.AddChild(MakeButton("Breed", () => DoBreed()));
        hbox.AddChild(MakeButton("Tools", ShowAdvancedToolsOverlay));
        hbox.AddChild(MakeButton("Save", ShowSaveOverlay));
        hbox.AddChild(MakeButton("[Tab] Next", () => CycleCreature()));
        _speechInput = MakeSpeechInput();
        hbox.AddChild(_speechInput);
        _speechLog = MakeLabel("", 10);
        _speechLog.CustomMinimumSize = new Vector2(260, 32);
        hbox.AddChild(_speechLog);
    }

    // ── Actions ─────────────────────────────────────────────────────────────
    private void DoTickle()
    {
        var c = GetSelectedCreatureNode();
        if (c?.Creature != null)
        {
            StimulusTable.Apply(c.Creature, StimulusId.PatOnBack);
            GD.Print("[GUI] Tickled creature!");
        }
    }

    private void DoSlap()
    {
        var c = GetSelectedCreatureNode();
        if (c?.Creature != null)
        {
            StimulusTable.Apply(c.Creature, StimulusId.Slap);
            GD.Print("[GUI] Slapped creature!");
        }
    }

    private void DoHatchEgg()
    {
        // Find incubator and inject
        var parent = _world ?? (Node?)GetTree().Root.GetChild(0);
        if (parent == null) return;

        foreach (Node n in parent.GetChildren())
        {
            if (n is Agents.IncubatorNode inc)
            {
                inc.Activate();
                return;
            }
        }

        // No incubator — just spawn a norn directly
        var nornScene = GD.Load<PackedScene>("res://scenes/Norn.tscn");
        if (nornScene != null && parent != null)
        {
            var norn = (CreatureNode)nornScene.Instantiate();
            norn.Position = new Vector3(0, 0, 0);
            parent.AddChild(norn);
            GD.Print("[GUI] Hatched a new norn!");
        }
    }

    private void DoFeed()
    {
        var c = GetSelectedCreatureNode();
        if (c == null) return;

        var food = new FoodNode { FoodKind = FoodKind.Food, GlycogenAmount = 0.4f, ATPAmount = 0.2f };
        food.Position = c.Position + new Vector3(0.5f, 0.18f, 0);
        c.GetParent()?.AddChild(food);
        GD.Print("[GUI] Dropped food near creature.");
    }

    /// <summary>
    /// Force-breed: runs Genome.Cross between the selected creature and its
    /// nearest peer immediately, bypassing the biochem Progesterone threshold
    /// and proximity gate in TryLayEgg. Also applies ItMated stimulus to both
    /// (Reward +0.5, SexDrive -0.8) so the biochem state reflects the event.
    /// This is the test shortcut for the breeding pipeline — real mating will
    /// eventually come from a decision-lobe verb + proximity script.
    /// </summary>
    private void DoBreed()
    {
        var a = GetSelectedCreatureNode();
        if (a?.Creature == null)
        {
            GD.Print("[GUI] Breed: no creature selected.");
            return;
        }

        // Find nearest other creature in the same parent
        var parent = a.GetParent();
        if (parent == null) return;

        CreatureNode? b = null;
        float nearest = float.MaxValue;
        foreach (Node n in parent.GetChildren())
        {
            if (n is CreatureNode cn && cn != a && cn.Creature != null)
            {
                float d = a.Position.DistanceTo(cn.Position);
                if (d < nearest) { nearest = d; b = cn; }
            }
        }

        if (b?.Creature == null)
        {
            GD.Print("[GUI] Breed: need at least 2 creatures in the scene.");
            return;
        }

        // Apply ItMated to both (Reward + SexDrive drop) so the stimulus record matches
        StimulusTable.Apply(a.Creature, StimulusId.ItMated);
        StimulusTable.Apply(b.Creature, StimulusId.ItMated);

        // Directly invoke the egg-laying pipeline — skips Progesterone/proximity gates
        a.LayEggWith(b);
        GD.Print($"[GUI] Forced breed: {a.Name} x {b.Name} ({nearest:F1}m apart).");
    }

    private void SubmitSpeechSuggestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _pointer ??= FindPointerAgent();
        Vector3 handPosition = _pointer?.Position ?? GetSelectedCreatureNode()?.Position ?? Vector3.Zero;
        BroadcastSpeechSuggestion(text, handPosition);

        if (_speechInput != null)
        {
            _speechInput.Text = string.Empty;
            _speechInput.ReleaseFocus();
        }
    }

    private void BroadcastSpeechSuggestion(string text, Vector3 handPosition)
    {
        WorldNode? world = _world ?? FindWorldNode();
        if (world == null)
            return;

        const float Earshot = 8.0f;
        var responses = new System.Collections.Generic.List<string>();
        foreach (Node node in world.GetChildren())
        {
            if (node is not CreatureNode creature || creature.Creature == null)
                continue;
            if (creature.Position.DistanceTo(handPosition) > Earshot)
                continue;

            CreatureSpeechSuggestion parsed = CreatureSpeechParser.Parse(text, creature.Creature.Vocabulary);
            if (!creature.MatchesSpeechSubject(parsed.SubjectWord))
                continue;

            CreatureSpeechSuggestion applied = creature.ApplySpeechSuggestion(text, handPosition);
            if (!string.IsNullOrWhiteSpace(creature.LastSpeechResponse))
                responses.Add(creature.LastSpeechResponse);
            else if (applied.IsRecognized)
                responses.Add($"{creature.Name}: heard {applied.Intent}");
        }

        if (_speechLog != null)
            _speechLog.Text = responses.Count == 0
                ? "No creature heard that."
                : string.Join("  ", responses.Take(2));
    }

    private void CycleCreature()
    {
        var parent = _world ?? (Node?)GetTree().Root.GetChild(0);
        if (parent == null) return;

        var creatures = new System.Collections.Generic.List<CreatureNode>();
        foreach (Node n in parent.GetChildren())
            if (n is CreatureNode cn && cn.Creature != null)
                creatures.Add(cn);

        if (creatures.Count == 0) return;
        _creatureIndex = (_creatureIndex + 1) % creatures.Count;
    }

    private void ShowAdvancedToolsOverlay()
    {
        if (_advancedToolsOverlay != null)
        {
            _advancedToolsOverlay.MoveToFront();
            return;
        }

        WorldNode? world = _world ?? FindWorldNode();
        _world = world;

        var overlay = new AdvancedToolsOverlay
        {
            ZIndex = 100,
        };
        overlay.Configure(world, GetSelectedCreatureNode);
        overlay.TreeExited += () =>
        {
            if (ReferenceEquals(_advancedToolsOverlay, overlay))
                _advancedToolsOverlay = null;
        };

        _advancedToolsOverlay = overlay;
        AddChild(overlay);
        overlay.MoveToFront();
    }

    private void CloseAdvancedToolsOverlay()
    {
        if (_advancedToolsOverlay == null)
            return;

        AdvancedToolsOverlay overlay = _advancedToolsOverlay;
        _advancedToolsOverlay = null;
        RemoveChild(overlay);
        overlay.QueueFree();
    }

    private void ShowPauseOverlay()
    {
        if (_pauseOverlay != null)
        {
            ShowPauseMainMenu();
            return;
        }

        GetTree().Paused = true;

        var root = new Control
        {
            ProcessMode = ProcessModeEnum.Always,
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Stop,
        };
        root.AddChild(new ColorRect
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            Color = new Color(0.0f, 0.0f, 0.0f, 0.50f),
        });

        var panel = new Panel
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -220,
            OffsetTop = -190,
            OffsetRight = 220,
            OffsetBottom = 190,
            MouseFilter = MouseFilterEnum.Stop,
        };
        panel.AddThemeStyleboxOverride("panel", PausePanelStyle());
        root.AddChild(panel);

        _pauseContent = new VBoxContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 24,
            OffsetTop = 22,
            OffsetRight = -24,
            OffsetBottom = -22,
        };
        _pauseContent.AddThemeConstantOverride("separation", 10);
        panel.AddChild(_pauseContent);

        _pauseOverlay = root;
        AddChild(root);
        ShowPauseMainMenu();
    }

    private void ClosePauseOverlay()
    {
        CloseSettingsOverlay();

        if (_pauseOverlay != null)
        {
            RemoveChild(_pauseOverlay);
            _pauseOverlay.QueueFree();
            _pauseOverlay = null;
            _pauseContent = null;
        }

        GetTree().Paused = false;
    }

    private void ShowPauseMainMenu()
    {
        if (_pauseOverlay == null)
            ShowPauseOverlay();
        if (_pauseContent == null)
            return;

        ClearChildren(_pauseContent);
        _pauseContent.AddChild(MakePauseHeading("Paused"));
        _pauseContent.AddChild(MakeButton("Resume", ClosePauseOverlay));
        _pauseContent.AddChild(MakeButton("Save Game", ShowSaveOverlay));
        _pauseContent.AddChild(MakeButton("Load Game", ShowLoadOverlay));
        _pauseContent.AddChild(MakeButton("Settings", ShowPauseSettingsOverlay));
    }

    private void ShowSaveOverlay()
    {
        if (_pauseOverlay == null)
            ShowPauseOverlay();
        if (_pauseContent == null)
            return;

        ClearChildren(_pauseContent);
        _pauseContent.AddChild(MakePauseHeading("Save Game"));

        for (int slot = 1; slot <= 5; slot++)
        {
            int captured = slot;
            _pauseContent.AddChild(MakeButton($"Save Slot {slot}", () => SaveToSlot(captured)));
        }

        _pauseContent.AddChild(MakeButton("Back", ShowPauseMainMenu));
    }

    private void ShowLoadOverlay()
    {
        if (_pauseOverlay == null)
            ShowPauseOverlay();
        if (_pauseContent == null)
            return;

        ClearChildren(_pauseContent);
        _pauseContent.AddChild(MakePauseHeading("Load Game"));

        var service = new GameSaveService(ProjectSettings.GlobalizePath("user://saves"));
        var slots = service.ListSlots();
        if (slots.Count == 0)
            _pauseContent.AddChild(MakeLabel("No saved games found.", 12));

        foreach (SaveSlotSummary slot in slots)
        {
            string label = slot.IsValid
                ? $"Slot {slot.Slot}: {slot.SlotName}  Creatures: {slot.CreatureCount}"
                : $"{slot.SlotName}: invalid save";
            Button button = MakeButton(label, () => LoadFromSlot(slot.Path));
            button.Disabled = !slot.IsValid;
            _pauseContent.AddChild(button);
        }

        _pauseContent.AddChild(MakeButton("Back", ShowPauseMainMenu));
    }

    private void ShowPauseSettingsOverlay()
    {
        CloseSettingsOverlay();
        var overlay = SettingsOverlay.Create(
            GameSettingsStore.Load(),
            settings => (_world ?? FindWorldNode())?.ApplySettings(settings),
            CloseSettingsOverlay);
        _settingsOverlay = overlay;
        AddChild(overlay);
    }

    private void CloseSettingsOverlay()
    {
        if (_settingsOverlay == null)
            return;

        RemoveChild(_settingsOverlay);
        _settingsOverlay.QueueFree();
        _settingsOverlay = null;
    }

    private void SaveToSlot(int slot)
    {
        WorldNode? world = _world ?? FindWorldNode();
        if (world == null)
            return;

        var service = new GameSaveService(ProjectSettings.GlobalizePath("user://saves"));
        GameSaveData save = world.CreateSaveData(slot, $"Slot {slot}");
        service.Save(save);
        GD.Print($"[GUI] Saved game to slot {slot}.");
        ShowPauseMainMenu();
    }

    private void LoadFromSlot(string savePath)
    {
        GetTree().Paused = false;
        GameLaunchSession.StartSavedGame(GetTree(), savePath);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private C? GetSelectedCreature() => GetSelectedCreatureNode()?.Creature;

    private CreatureNode? GetSelectedCreatureNode()
    {
        var parent = _world ?? (Node?)GetTree().Root.GetChild(0);
        if (parent == null) return null;

        int idx = 0;
        foreach (Node n in parent.GetChildren())
        {
            if (n is CreatureNode cn && cn.Creature != null)
            {
                if (idx == _creatureIndex) return cn;
                idx++;
            }
        }
        return null;
    }

    private WorldNode? FindWorldNode()
    {
        var root = GetTree().Root;
        foreach (Node n in root.GetChildren())
        {
            if (n is WorldNode w) return w;
            var w2 = n.GetNodeOrNull<WorldNode>(".");
            if (w2 != null) return w2;
            // Check if root scene itself
            foreach (Node c in n.GetChildren())
                if (c is WorldNode wn) return wn;
        }
        return null;
    }

    private PointerAgent? FindPointerAgent()
    {
        var root = GetTree().Root;
        foreach (Node n in root.GetChildren())
        {
            foreach (Node c in n.GetChildren())
                if (c is PointerAgent pa) return pa;
        }
        return null;
    }

    private static Panel MakePanel(Vector2 pos, Vector2 size)
    {
        var panel = new Panel();
        panel.Position = pos;
        panel.Size = size;
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.10f, 0.75f),
            ContentMarginLeft = 6, ContentMarginTop = 6,
            ContentMarginRight = 6, ContentMarginBottom = 6,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        panel.MouseFilter = MouseFilterEnum.Ignore;
        return panel;
    }

    private static Label MakeLabel(string text, int size = 10)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", size);
        lbl.Modulate = new Color(0.90f, 0.92f, 0.98f);
        lbl.MouseFilter = MouseFilterEnum.Ignore;
        return lbl;
    }

    private static HBoxContainer MakeBarRow(string label, out ProgressBar bar)
    {
        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Ignore;
        var lbl = MakeLabel(label.PadRight(10), 9);
        lbl.CustomMinimumSize = new Vector2(70, 0);
        row.AddChild(lbl);

        bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Value = 0,
            CustomMinimumSize = new Vector2(160, 14),
            ShowPercentage = false,
        };
        bar.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(bar);
        return row;
    }

    private static void SetBar(ProgressBar? bar, float value, bool invertColor = false)
    {
        if (bar == null) return;
        value = Math.Clamp(value, 0, 1);
        bar.Value = value;

        var fill = new StyleBoxFlat();
        float r, g;
        if (invertColor)
        {
            // Green = good (high), Red = bad (low)
            r = (float)Math.Clamp((1 - value) * 2.0, 0, 1);
            g = (float)Math.Clamp(value * 2.0, 0, 1);
        }
        else
        {
            // Green = good (low), Red = bad (high)
            r = (float)Math.Clamp(value * 2.0, 0, 1);
            g = (float)Math.Clamp(2.0 - value * 2.0, 0, 1);
        }
        fill.BgColor = new Color(r, g, 0.1f, 0.85f);
        bar.AddThemeStyleboxOverride("fill", fill);
    }

    private static bool ShouldOpenAdvancedToolsOnStartup()
        => OS.GetCmdlineArgs().Any(arg => string.Equals(arg, "--open-advanced-tools", StringComparison.OrdinalIgnoreCase));

    private static Label MakePauseHeading(string text)
    {
        var label = MakeLabel(text, 18);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(1, 36);
        return label;
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(70, 32),
        };
        btn.Pressed += onClick;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.18f, 0.28f, 0.9f),
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            ContentMarginLeft = 8, ContentMarginRight = 8,
        };
        btn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.25f, 0.30f, 0.45f, 0.95f),
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            ContentMarginLeft = 8, ContentMarginRight = 8,
        };
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        return btn;
    }

    private LineEdit MakeSpeechInput()
    {
        var input = new LineEdit
        {
            PlaceholderText = "say: push food",
            CustomMinimumSize = new Vector2(190, 32),
            TooltipText = "Type suggestions such as 'come', 'push toy', or 'eat food'.",
        };
        input.TextSubmitted += SubmitSpeechSuggestion;
        return input;
    }

    private static StyleBoxFlat PausePanelStyle()
        => new()
        {
            BgColor = new Color(0.045f, 0.041f, 0.060f, 0.96f),
            BorderColor = new Color(0.56f, 0.42f, 0.22f, 0.95f),
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        };
}
