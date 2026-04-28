using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using CreaturesReborn.Godot.Agents;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Godot.UI;

[GlobalClass]
public partial class AdvancedToolsOverlay : Control
{
    private const int BrainHistoryCapacity = 180;
    private readonly BrainMonitorHistory _history = new(BrainHistoryCapacity);

    private Func<CreatureNode?>? _selectedCreatureResolver;
    private Viewport? _viewport;
    private WorldNode? _world;
    private CreatureNode? _lastCreatureNode;
    private GenomeEditSession? _genomeSession;
    private int _selectedGeneIndex;
    private bool _paused;
    private float _sampleTimer;
    private float _sampleInterval = 0.20f;

    private Label _targetLabel = null!;
    private Label _statusLabel = null!;
    private TabContainer _tabs = null!;
    private BrainMapView _brainMap = null!;
    private RichTextLabel _brainTables = null!;
    private RichTextLabel _brainCharts = null!;
    private ItemList _geneList = null!;
    private OptionButton _familyFilter = null!;
    private LineEdit _geneSearch = null!;
    private CheckBox _mutableOnly = null!;
    private CheckBox _sexLinkedOnly = null!;
    private CheckBox _invalidOnly = null!;
    private SpinBox _idSpin = null!;
    private SpinBox _generationSpin = null!;
    private SpinBox _switchOnSpin = null!;
    private SpinBox _mutabilitySpin = null!;
    private SpinBox _variantSpin = null!;
    private CheckBox _flagMutable = null!;
    private CheckBox _flagDuplicate = null!;
    private CheckBox _flagCut = null!;
    private CheckBox _flagMale = null!;
    private CheckBox _flagFemale = null!;
    private TextEdit _typedEditor = null!;
    private TextEdit _rawEditor = null!;
    private LineEdit _typedFieldName = null!;
    private LineEdit _typedFieldValue = null!;
    private LineEdit _importPath = null!;
    private RichTextLabel _genomeSummary = null!;
    private RichTextLabel _validationSummary = null!;
    private RichTextLabel _diffSummary = null!;
    private SpinBox _mutationChance = null!;
    private SpinBox _mutationDegree = null!;

    public void Configure(WorldNode? world, Func<CreatureNode?> selectedCreatureResolver)
    {
        _world = world;
        _selectedCreatureResolver = selectedCreatureResolver;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        ResizeToViewport();
        _viewport = GetViewport();
        _viewport.SizeChanged += ResizeToViewport;

        var veil = new ColorRect
        {
            Color = new Color(0.012f, 0.015f, 0.020f, 0.98f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        var frame = new Panel();
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.OffsetLeft = 18;
        frame.OffsetTop = 18;
        frame.OffsetRight = -18;
        frame.OffsetBottom = -18;
        frame.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.035f, 0.042f, 0.055f, 1.0f)));
        AddChild(frame);

        var root = new VBoxContainer
        {
            Position = new Vector2(12, 12),
            Size = new Vector2(1220, 660),
        };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 12;
        root.OffsetTop = 12;
        root.OffsetRight = -12;
        root.OffsetBottom = -12;
        root.AddThemeConstantOverride("separation", 8);
        frame.AddChild(root);

        root.AddChild(BuildToolbar());
        root.AddChild(BuildTabs());
        SelectStartupTab();
        _statusLabel = Label("Ready.", 10);
        root.AddChild(_statusLabel);
    }

    public override void _ExitTree()
    {
        if (_viewport != null)
            _viewport.SizeChanged -= ResizeToViewport;
    }

    private void ResizeToViewport()
    {
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
    }

    public override void _Process(double delta)
    {
        CreatureNode? node = _selectedCreatureResolver?.Invoke();
        if (node != _lastCreatureNode)
        {
            _lastCreatureNode = node;
            LoadGenomeSession(node);
        }

        _targetLabel.Text = node?.Creature == null
            ? "Selected: none"
            : $"Selected: {node.Name}  moniker={node.Creature.Genome.Moniker}  age={node.Creature.Genome.Age}";

        if (_paused || node?.Creature == null)
            return;

        _sampleTimer += (float)delta;
        if (_sampleTimer < _sampleInterval)
            return;

        _sampleTimer = 0;
        BrainMonitorFrame brainFrame = BrainMonitorFrame.Create(
            node.Creature,
            new BrainMonitorOptions(MaxNeuronsPerLobe: 20, MaxDendritesPerTract: 12));
        _history.Record(brainFrame);
        UpdateBrainMonitor(brainFrame);
    }

    private Control BuildToolbar()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        _targetLabel = Label("Selected: none", 12);
        _targetLabel.CustomMinimumSize = new Vector2(270, 34);
        row.AddChild(ToolbarCell("CREATURE", _targetLabel, new Vector2(280, 46)));
        row.AddChild(Button("PAUSE", TogglePause, new Vector2(78, 38), accent: true));
        row.AddChild(Button("RESUME", TogglePause, new Vector2(82, 38)));
        row.AddChild(ToolbarCell("SAMPLING RATE", Label("5 Hz", 11), new Vector2(94, 46)));
        row.AddChild(ToolbarCell("BUFFER", Label($"{BrainHistoryCapacity}", 11), new Vector2(88, 46)));
        row.AddChild(Button("EXPORT", ExportWorkingGenome, new Vector2(82, 38)));
        row.AddChild(Button("HATCH EGG", HatchEditedEgg, new Vector2(100, 38)));
        row.AddChild(Button("LIVE APPLY", LiveApply, new Vector2(100, 38), warning: true));
        row.AddChild(Button("X", Close, new Vector2(32, 38)));
        return row;
    }

    private Control BuildTabs()
    {
        var shell = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        shell.AddThemeConstantOverride("separation", 6);

        _tabs = new TabContainer { Visible = false };
        _tabs.AddChild(new Control { Name = "Brain Monitor" });
        _tabs.AddChild(new Control { Name = "Genetics Kit" });
        shell.AddChild(_tabs);

        var workbench = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        workbench.AddThemeConstantOverride("separation", 8);
        workbench.AddChild(Section("Brain Monitor", BuildBrainWorkbench(), new Vector2(450, 560)));
        workbench.AddChild(Section("Genetics Kit", BuildGeneticsWorkbench(), new Vector2(520, 560)));
        workbench.AddChild(Section("Validation", BuildRightRail(), new Vector2(220, 560)));
        shell.AddChild(workbench);
        return shell;
    }

    private Control BuildBrainWorkbench()
    {
        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        box.AddThemeConstantOverride("separation", 6);
        box.AddChild(SegmentedHeader("Lobes", "Tracts"));

        var mapRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        mapRow.AddThemeConstantOverride("separation", 6);
        _brainMap = new BrainMapView
        {
            CustomMinimumSize = new Vector2(330, 300),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        mapRow.AddChild(_brainMap);
        var legend = RichText(new Vector2(88, 300));
        legend.Text = "[b]ACTIVATION[/b]\n[color=#24d9ff]1.00[/color] High\n[color=#1caac9]0.50[/color]\n[color=#145f74]0.00[/color] Low\n\n[b]TRACT OVERLAY[/b]\n[color=#51d46d]Excitatory[/color]\n[color=#ffcf3a]Selected[/color]\n[color=#38aee8]All[/color]\n\n[b]DISPLAY[/b]\nLobe labels\nHeat map\nWinning neuron";
        mapRow.AddChild(legend);
        box.AddChild(mapRow);

        var lower = new HBoxContainer();
        lower.AddThemeConstantOverride("separation", 6);
        _brainCharts = RichText(new Vector2(240, 190));
        _brainTables = RichText(new Vector2(180, 190));
        lower.AddChild(_brainCharts);
        lower.AddChild(_brainTables);
        box.AddChild(lower);
        return box;
    }

    private Control BuildGeneticsWorkbench()
    {
        var box = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        box.AddThemeConstantOverride("separation", 8);

        var genePane = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(225, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        genePane.AddThemeConstantOverride("separation", 6);
        genePane.AddChild(SegmentedHeader("Genes", "Typed Editor", "Raw Payload"));
        genePane.AddChild(BuildGeneFilters());
        _geneList = new ItemList
        {
            CustomMinimumSize = new Vector2(230, 330),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _geneList.ItemSelected += OnGeneSelected;
        StyleList(_geneList);
        genePane.AddChild(_geneList);
        genePane.AddChild(BuildGenomeActions());
        box.AddChild(genePane);

        var editorPane = BuildGeneEditor();
        editorPane.CustomMinimumSize = new Vector2(210, 0);
        editorPane.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.AddChild(editorPane);
        return box;
    }

    private Control BuildRightRail()
    {
        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        box.AddThemeConstantOverride("separation", 8);

        _validationSummary = RichText(new Vector2(240, 128));
        box.AddChild(_validationSummary);
        box.AddChild(Label("PHENOTYPE SUMMARY", 10, bold: true));
        _genomeSummary = RichText(new Vector2(240, 210));
        box.AddChild(_genomeSummary);
        box.AddChild(Label("DIFF", 10, bold: true));
        _diffSummary = RichText(new Vector2(240, 160));
        box.AddChild(_diffSummary);
        return box;
    }

    private Control BuildBrainTab()
    {
        var split = new HSplitContainer
        {
            Name = "Brain Monitor",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SplitOffset = 650,
        };

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 6);
        _brainMap = new BrainMapView
        {
            CustomMinimumSize = new Vector2(620, 330),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        left.AddChild(_brainMap);
        _brainCharts = RichText(new Vector2(620, 190));
        left.AddChild(_brainCharts);

        _brainTables = RichText(new Vector2(520, 540));
        split.AddChild(left);
        split.AddChild(_brainTables);
        return split;
    }

    private Control BuildGeneticsTab()
    {
        var split = new HSplitContainer
        {
            Name = "Genetics Kit",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SplitOffset = 360,
        };

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 6);
        left.AddChild(BuildGeneFilters());
        _geneList = new ItemList
        {
            CustomMinimumSize = new Vector2(340, 430),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _geneList.ItemSelected += OnGeneSelected;
        left.AddChild(_geneList);
        left.AddChild(BuildGenomeActions());
        split.AddChild(left);

        var right = new HSplitContainer { SplitOffset = 470, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(BuildGeneEditor());
        right.AddChild(BuildSummaryPanel());
        split.AddChild(right);
        return split;
    }

    private Control BuildGeneFilters()
    {
        var box = new VBoxContainer();
        var row = new HBoxContainer();
        _familyFilter = new OptionButton();
        foreach (string label in new[] { "All", "Brain", "Biochemistry", "Creature", "Organ" })
            _familyFilter.AddItem(label);
        _familyFilter.CustomMinimumSize = new Vector2(84, 0);
        _familyFilter.ItemSelected += _ => RefreshGeneList();
        row.AddChild(_familyFilter);
        _geneSearch = new LineEdit { PlaceholderText = "search", CustomMinimumSize = new Vector2(118, 0) };
        _geneSearch.TextChanged += _ => RefreshGeneList();
        row.AddChild(_geneSearch);
        box.AddChild(row);

        var flags = new HBoxContainer();
        _mutableOnly = Check("Mutable", RefreshGeneList);
        _sexLinkedOnly = Check("Sex linked", RefreshGeneList);
        _invalidOnly = Check("Invalid", RefreshGeneList);
        flags.AddChild(_mutableOnly);
        flags.AddChild(_sexLinkedOnly);
        flags.AddChild(_invalidOnly);
        box.AddChild(flags);
        return box;
    }

    private Control BuildGenomeActions()
    {
        var box = new VBoxContainer();
        var importRow = new HBoxContainer();
        _importPath = new LineEdit { PlaceholderText = ".gen path", CustomMinimumSize = new Vector2(140, 0) };
        importRow.AddChild(_importPath);
        importRow.AddChild(Button("Import", ImportGenome));
        box.AddChild(importRow);

        var actionRow = new HBoxContainer();
        actionRow.AddChild(Button("Save Variant", SaveVariant));
        actionRow.AddChild(Button("Compare", RefreshSummaries));
        actionRow.AddChild(Button("Breed Pair", BreedWorldPair));
        box.AddChild(actionRow);

        var mutationRow = new HBoxContainer();
        mutationRow.AddChild(Label("Mut chance", 9));
        _mutationChance = Spin(0, 255, 4);
        mutationRow.AddChild(_mutationChance);
        mutationRow.AddChild(Label("degree", 9));
        _mutationDegree = Spin(0, 255, 4);
        mutationRow.AddChild(_mutationDegree);
        box.AddChild(mutationRow);
        return box;
    }

    private Control BuildGeneEditor()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);

        var headerGrid = new GridContainer { Columns = 2 };
        _idSpin = AddSpin(headerGrid, "Id", 0, 255, 0);
        _generationSpin = AddSpin(headerGrid, "Generation", 0, 255, 0);
        _switchOnSpin = AddSpin(headerGrid, "Switch", 0, 255, 0);
        _mutabilitySpin = AddSpin(headerGrid, "Mutability", 0, 255, 0);
        _variantSpin = AddSpin(headerGrid, "Variant", 0, GeneConstants.NUM_BEHAVIOUR_VARIANTS, 0);
        box.AddChild(headerGrid);

        var flagRow = new HBoxContainer();
        _flagMutable = Check("MUT", null);
        _flagDuplicate = Check("DUP", null);
        _flagCut = Check("CUT", null);
        _flagMale = Check("Male", null);
        _flagFemale = Check("Female", null);
        flagRow.AddChild(_flagMutable);
        flagRow.AddChild(_flagDuplicate);
        flagRow.AddChild(_flagCut);
        box.AddChild(flagRow);

        var sexRow = new HBoxContainer();
        sexRow.AddChild(_flagMale);
        sexRow.AddChild(_flagFemale);
        sexRow.AddChild(Button("Apply Header", ApplyHeaderEdit, new Vector2(104, 28)));
        box.AddChild(sexRow);

        _typedEditor = TextEditor(new Vector2(210, 145), readOnly: true);
        box.AddChild(Label("Typed Editor", 10, bold: true));
        box.AddChild(_typedEditor);

        var fieldRow = new HBoxContainer();
        _typedFieldName = new LineEdit { PlaceholderText = "field", CustomMinimumSize = new Vector2(82, 0) };
        _typedFieldValue = new LineEdit { PlaceholderText = "value", CustomMinimumSize = new Vector2(82, 0) };
        fieldRow.AddChild(_typedFieldName);
        fieldRow.AddChild(_typedFieldValue);
        fieldRow.AddChild(Button("Apply Typed Field", ApplyTypedFieldEdit));
        box.AddChild(fieldRow);

        box.AddChild(Label("Raw Payload", 10, bold: true));
        _rawEditor = TextEditor(new Vector2(210, 120), readOnly: false);
        box.AddChild(_rawEditor);

        var opRow = new GridContainer { Columns = 3 };
        opRow.AddChild(Button("Apply Raw", ApplyRawPayloadEdit));
        opRow.AddChild(Button("Duplicate", DuplicateSelectedGene));
        opRow.AddChild(Button("Delete", DeleteSelectedGene));
        opRow.AddChild(Button("Undo", Undo));
        opRow.AddChild(Button("Redo", Redo));
        box.AddChild(opRow);

        return box;
    }

    private Control BuildSummaryPanel()
    {
        var box = new VBoxContainer();
        box.AddChild(Label("Validation", 10, bold: true));
        _validationSummary = RichText(new Vector2(360, 120));
        box.AddChild(_validationSummary);
        box.AddChild(Label("Diff", 10, bold: true));
        _diffSummary = RichText(new Vector2(360, 150));
        box.AddChild(_diffSummary);
        box.AddChild(Label("Phenotype / Summary", 10, bold: true));
        _genomeSummary = RichText(new Vector2(360, 240));
        box.AddChild(_genomeSummary);
        return box;
    }

    private void LoadGenomeSession(CreatureNode? node)
    {
        if (node?.Creature == null)
        {
            _genomeSession = null;
            _geneList?.Clear();
            return;
        }

        _genomeSession = new GenomeEditSession(GenomeDocument.FromGenome(node.Creature.Genome, new Rng(100)), new Rng(101));
        _selectedGeneIndex = 0;
        RefreshGeneList();
        SelectGene(0);
        RefreshSummaries();
    }

    private void UpdateBrainMonitor(BrainMonitorFrame frame)
    {
        _brainMap.SetFrame(frame);

        var tables = new StringBuilder();
        tables.AppendLine("[b]Lobes[/b]");
        foreach (BrainLobeMonitorRow lobe in frame.Lobes.Take(14))
            tables.AppendLine($"{lobe.Index,2} {lobe.TokenText} pos=({lobe.X},{lobe.Y}) size={lobe.Width}x{lobe.Height} win={lobe.WinningNeuronId} act={lobe.Activation:0.00}");

        tables.AppendLine();
        tables.AppendLine("[b]Tracts[/b]");
        foreach (BrainTractMonitorRow tract in frame.Tracts.Take(18))
            tables.AppendLine($"{tract.Index,2} {tract.SourceTokenText}->{tract.DestinationTokenText} dend={tract.DendriteCount} stlt={tract.STtoLTRate:0.000}");

        tables.AppendLine();
        tables.AppendLine("[b]Ports / Motor[/b]");
        tables.AppendLine($"verb={frame.MotorVerb} noun={frame.MotorNoun} ports={frame.Ports.Count} modules={frame.Modules.Count}");
        _brainTables.Text = tables.ToString();

        var charts = new StringBuilder();
        charts.AppendLine("[b]Drive Rings[/b]");
        foreach (BrainMonitorSeries series in _history.DriveSeries.Take(8))
            charts.AppendLine($"{series.Name}: {Spark(series.Values)}");
        charts.AppendLine("[b]Chemical Rings[/b]");
        foreach (BrainMonitorSeries series in _history.ChemicalSeries)
            charts.AppendLine($"{series.Name}: {Spark(series.Values)}");
        _brainCharts.Text = charts.ToString();
    }

    private void RefreshGeneList()
    {
        if (_geneList == null || _genomeSession == null)
            return;

        _geneList.Clear();
        IReadOnlyList<GeneValidationIssue> issues = _genomeSession.Validate();
        HashSet<int> invalidOffsets = issues
            .Where(issue => issue.Severity == GeneValidationSeverity.Error)
            .Select(issue => issue.Offset)
            .ToHashSet();

        string family = _familyFilter.GetItemText(_familyFilter.Selected);
        string search = _geneSearch.Text.Trim();
        IReadOnlyList<GeneRecord> genes = _genomeSession.Document.WorkingRecords;
        for (int i = 0; i < genes.Count; i++)
        {
            GeneRecord gene = genes[i];
            if (family != "All" && !gene.DisplayName.StartsWith(family, StringComparison.OrdinalIgnoreCase))
                continue;
            if (search.Length > 0 && !gene.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) && !gene.Identity.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;
            if (_mutableOnly.ButtonPressed && !gene.Header.IsMutable)
                continue;
            if (_sexLinkedOnly.ButtonPressed && !gene.Header.MaleLinked && !gene.Header.FemaleLinked)
                continue;
            if (_invalidOnly.ButtonPressed && !invalidOffsets.Contains(gene.Offset))
                continue;

            _geneList.AddItem($"{i:D3} {gene.DisplayName} id={gene.Id} gen={gene.Generation} bytes={gene.Length}");
            _geneList.SetItemMetadata(_geneList.ItemCount - 1, i);
        }
    }

    private void OnGeneSelected(long itemIndex)
    {
        if (_geneList.GetItemMetadata((int)itemIndex).AsInt32() is int geneIndex)
            SelectGene(geneIndex);
    }

    private void SelectGene(int geneIndex)
    {
        if (_genomeSession == null || geneIndex < 0 || geneIndex >= _genomeSession.Document.WorkingRecords.Count)
            return;

        _selectedGeneIndex = geneIndex;
        GeneRecord record = _genomeSession.Document.WorkingRecords[geneIndex];
        _idSpin.Value = record.Id;
        _generationSpin.Value = record.Generation;
        _switchOnSpin.Value = record.SwitchOnAge;
        _mutabilitySpin.Value = record.Mutability;
        _variantSpin.Value = record.Variant;
        _flagMutable.ButtonPressed = record.Header.IsMutable;
        _flagDuplicate.ButtonPressed = record.Header.CanDuplicate;
        _flagCut.ButtonPressed = record.Header.CanCut;
        _flagMale.ButtonPressed = record.Header.MaleLinked;
        _flagFemale.ButtonPressed = record.Header.FemaleLinked;

        EditableGenePayload payload = GenePayloadCodec.Decode(record);
        _typedEditor.Text = FormatTypedPayload(payload);
        _rawEditor.Text = FormatHex(record.Payload.Bytes);
        _typedFieldName.Text = payload.Fields.FirstOrDefault(field => !payload.IsRawFallback && field.Kind != GenePayloadFieldKind.RawBytes)?.Name ?? "";
        _typedFieldValue.Text = "";
    }

    private void ApplyHeaderEdit()
    {
        if (_genomeSession == null) return;
        byte flags = 0;
        if (_flagMutable.ButtonPressed) flags |= (byte)MutFlags.MUT;
        if (_flagDuplicate.ButtonPressed) flags |= (byte)MutFlags.DUP;
        if (_flagCut.ButtonPressed) flags |= (byte)MutFlags.CUT;
        if (_flagMale.ButtonPressed) flags |= (byte)MutFlags.LINKMALE;
        if (_flagFemale.ButtonPressed) flags |= (byte)MutFlags.LINKFEMALE;

        _genomeSession.Apply(GenomeEditOperation.EditHeader(
            _selectedGeneIndex,
            GeneHeaderPatch.Create(
                id: (int)_idSpin.Value,
                generation: (int)_generationSpin.Value,
                switchOnAge: (int)_switchOnSpin.Value,
                flags: flags,
                mutability: (int)_mutabilitySpin.Value,
                variant: (int)_variantSpin.Value)));
        AfterEdit("Header applied.");
    }

    private void ApplyTypedFieldEdit()
    {
        if (_genomeSession == null) return;
        GeneRecord record = _genomeSession.Document.WorkingRecords[_selectedGeneIndex];
        EditableGenePayload payload = GenePayloadCodec.Decode(record);
        GenePayloadField? field = payload.Fields.FirstOrDefault(f => string.Equals(f.Name, _typedFieldName.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (field == null || payload.IsRawFallback)
        {
            SetStatus("Typed field not found for this gene.");
            return;
        }

        GeneFieldEdit edit = field.Kind == GenePayloadFieldKind.Token
            ? GeneFieldEdit.String(field.Name, _typedFieldValue.Text)
            : GeneFieldEdit.Int(field.Name, ParseInt(_typedFieldValue.Text, 0));
        _genomeSession.Apply(GenomeEditOperation.ReplacePayload(_selectedGeneIndex, GenePayloadCodec.Encode(record, [edit])));
        AfterEdit($"Typed field {field.Name} applied.");
    }

    private void ApplyRawPayloadEdit()
    {
        if (_genomeSession == null) return;
        try
        {
            _genomeSession.Apply(GenomeEditOperation.ReplacePayload(_selectedGeneIndex, ParseHex(_rawEditor.Text)));
            AfterEdit("Raw payload applied.");
        }
        catch (Exception ex)
        {
            SetStatus($"Raw edit refused: {ex.Message}");
        }
    }

    private void DuplicateSelectedGene()
    {
        if (_genomeSession == null) return;
        _genomeSession.Apply(GenomeEditOperation.DuplicateGene(_selectedGeneIndex));
        AfterEdit("Gene duplicated.");
    }

    private void DeleteSelectedGene()
    {
        if (_genomeSession == null) return;
        _genomeSession.Apply(GenomeEditOperation.DeleteGene(_selectedGeneIndex));
        _selectedGeneIndex = Math.Clamp(_selectedGeneIndex, 0, Math.Max(0, _genomeSession.Document.WorkingRecords.Count - 1));
        AfterEdit("Gene deleted.");
    }

    private void Undo()
    {
        _genomeSession?.Undo();
        AfterEdit("Undo.");
    }

    private void Redo()
    {
        _genomeSession?.Redo();
        AfterEdit("Redo.");
    }

    private void AfterEdit(string message)
    {
        RefreshGeneList();
        SelectGene(Math.Clamp(_selectedGeneIndex, 0, Math.Max(0, _genomeSession?.Document.WorkingRecords.Count - 1 ?? 0)));
        RefreshSummaries();
        SetStatus(message);
    }

    private void RefreshSummaries()
    {
        if (_genomeSession == null)
            return;

        IReadOnlyList<GeneValidationIssue> validation = _genomeSession.Validate();
        _validationSummary.Text = validation.Count == 0
            ? "[color=green]No validation issues.[/color]"
            : string.Join('\n', validation.Take(12).Select(issue => $"[color={(issue.Severity == GeneValidationSeverity.Error ? "red" : "yellow")}]{issue.Code}[/color] @{issue.Offset}: {issue.Message}"));

        GenomeDiff diff = _genomeSession.CreateDiff();
        _diffSummary.Text = diff.Records.Count == 0
            ? "No source/working differences."
            : string.Join('\n', diff.Records.Take(14).Select(record => $"{record.Kind}: {record.Description}"));

        GenomeSummary summary = GenomeSummary.Create(_genomeSession.Document.WorkingRecords);
        PhenotypeSummary phenotype = _genomeSession.CreatePhenotypeSummary();
        var sb = new StringBuilder();
        sb.AppendLine($"Genes {summary.TotalGenes}");
        foreach (KeyValuePair<string, int> count in summary.FamilyCounts())
            sb.AppendLine($"{count.Key}: {count.Value}");
        sb.AppendLine();
        foreach (PhenotypeSection section in phenotype.Sections.Values)
            sb.AppendLine($"{section.Name}: {string.Join("; ", section.Lines.Skip(1).Take(4))}");
        _genomeSummary.Text = sb.ToString();
    }

    private void ImportGenome()
    {
        string path = _importPath.Text.Trim('"', ' ');
        if (!File.Exists(path))
        {
            SetStatus("Import path does not exist.");
            return;
        }

        try
        {
            byte[] fileBytes = File.ReadAllBytes(path);
            _genomeSession = new GenomeEditSession(GenomeDocument.FromFileBytes(fileBytes, new Rng(110), moniker: Path.GetFileNameWithoutExtension(path)), new Rng(111));
            _selectedGeneIndex = 0;
            RefreshGeneList();
            SelectGene(0);
            RefreshSummaries();
            SetStatus($"Imported {Path.GetFileName(path)}.");
        }
        catch (Exception ex)
        {
            SetStatus($"Import failed: {ex.Message}");
        }
    }

    private void ExportWorkingGenome()
    {
        if (_genomeSession == null) return;
        string path = WriteWorkingGenome("export");
        SetStatus($"Exported {path}");
    }

    private void SaveVariant()
    {
        if (_genomeSession == null) return;
        string path = WriteWorkingGenome("variant");
        SetStatus($"Saved variant {path}");
    }

    private string WriteWorkingGenome(string prefix)
    {
        string dir = ProjectSettings.GlobalizePath("user://genetics-kit");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{prefix}_{Time.GetTicksMsec()}.gen");
        File.WriteAllBytes(path, _genomeSession!.ExportFileBytes());
        return path;
    }

    private void HatchEditedEgg()
    {
        if (_genomeSession == null || _world == null)
            return;

        string path = WriteWorkingGenome("egg");
        CreatureNode? selected = _selectedCreatureResolver?.Invoke();
        var egg = new EggNode
        {
            GenomePath = path,
            Sex = selected?.Creature?.Genome.Sex ?? GeneConstants.MALE,
            HatchTime = 4.0f,
            Position = selected?.Position + new Vector3(0.8f, 0.2f, 0) ?? Vector3.Zero,
        };
        _world.AddChild(egg);
        SetStatus("Hatch Egg queued from edited genome.");
    }

    private void LiveApply()
    {
        CreatureNode? node = _selectedCreatureResolver?.Invoke();
        if (node == null || _genomeSession == null)
            return;

        node.ReplaceCreatureGenome(_genomeSession.ExportFileBytes());
        SetStatus("Live Apply requested. Creature rebuilt from edited genome.");
        LoadGenomeSession(node);
    }

    private void BreedWorldPair()
    {
        if (_world == null)
            return;

        List<CreatureNode> creatures = _world.GetChildren().OfType<CreatureNode>().Where(node => node.Creature != null).ToList();
        CreatureNode? mum = creatures.FirstOrDefault(node => node.Creature!.Genome.Sex == GeneConstants.FEMALE);
        CreatureNode? dad = creatures.FirstOrDefault(node => node.Creature!.Genome.Sex == GeneConstants.MALE);
        if (mum?.Creature == null || dad?.Creature == null)
        {
            SetStatus("Breed Pair needs one female and one male creature in the world.");
            return;
        }

        _genomeSession = GenomeEditSession.FromCrossover(
            $"kit-child-{Time.GetTicksMsec()}",
            mum.Creature.Genome,
            dad.Creature.Genome,
            new Rng((int)(Time.GetTicksMsec() & 0x7fffffff)),
            (byte)_mutationChance.Value,
            (byte)_mutationDegree.Value,
            (byte)_mutationChance.Value,
            (byte)_mutationDegree.Value);
        _selectedGeneIndex = 0;
        RefreshGeneList();
        SelectGene(0);
        RefreshSummaries();
        SetStatus("Breed Pair created a staged child genome.");
    }

    private void TogglePause()
    {
        _paused = !_paused;
        SetStatus(_paused ? "Brain monitor paused." : "Brain monitor resumed.");
    }

    private void AdjustSampleInterval(float delta)
    {
        _sampleInterval = Math.Clamp(_sampleInterval + delta, 0.05f, 1.0f);
        SetStatus($"Sample interval {_sampleInterval:0.00}s.");
    }

    private void Close()
    {
        GetParent()?.RemoveChild(this);
        QueueFree();
    }

    private void SetStatus(string text)
    {
        if (_statusLabel != null)
            _statusLabel.Text = text;
    }

    private void SelectStartupTab()
    {
        foreach (string arg in OS.GetCmdlineArgs())
        {
            if (!arg.StartsWith("--advanced-tools-tab=", StringComparison.OrdinalIgnoreCase))
                continue;

            string tab = arg.Substring("--advanced-tools-tab=".Length);
            _tabs.CurrentTab = string.Equals(tab, "genetics", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            return;
        }
    }

    private static string FormatTypedPayload(EditableGenePayload payload)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{payload.Kind} {(payload.IsRawFallback ? "(raw fallback)" : "")}");
        foreach (GenePayloadField field in payload.Fields)
            sb.AppendLine($"{field.Name} [{field.Kind}] @{field.Offset}+{field.Length} = {field.DisplayValue}");
        return sb.ToString();
    }

    private static string FormatHex(byte[] bytes)
        => string.Join(' ', bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));

    private static byte[] ParseHex(string text)
    {
        string compact = new(text.Where(Uri.IsHexDigit).ToArray());
        if (compact.Length % 2 != 0)
            throw new FormatException("Hex payload must have an even number of digits.");

        byte[] bytes = new byte[compact.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = byte.Parse(compact.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return bytes;
    }

    private static int ParseInt(string text, int fallback)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

    private static string Spark(IReadOnlyList<float> values)
    {
        const string blocks = "▁▂▃▄▅▆▇█";
        if (values.Count == 0)
            return "";
        return new string(values.Select(value =>
        {
            int index = Math.Clamp((int)MathF.Round(Math.Clamp(value, 0, 1) * (blocks.Length - 1)), 0, blocks.Length - 1);
            return blocks[index];
        }).ToArray());
    }

    private static Label Label(string text, int size = 10, bool bold = false, bool accent = false)
    {
        var label = new Label
        {
            Text = bold ? text.ToUpperInvariant() : text,
            Modulate = accent ? new Color(0.18f, 0.90f, 1.0f) : new Color(0.88f, 0.93f, 0.98f),
        };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private static Control ToolbarCell(string title, Control content, Vector2 minSize)
    {
        var box = new VBoxContainer { CustomMinimumSize = minSize };
        box.AddThemeConstantOverride("separation", 2);
        box.AddChild(Label(title, 9, bold: true));
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.AddChild(content);
        return box;
    }

    private static Control Section(string title, Control body, Vector2 minSize)
    {
        var panel = new Panel
        {
            CustomMinimumSize = minSize,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.020f, 0.040f, 0.044f, 0.98f), border: true));

        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 8;
        box.OffsetTop = 6;
        box.OffsetRight = -8;
        box.OffsetBottom = -8;
        box.AddThemeConstantOverride("separation", 6);
        panel.AddChild(box);
        box.AddChild(Label(title, 16, bold: false, accent: true));
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        box.AddChild(body);
        return panel;
    }

    private static Control SegmentedHeader(params string[] labels)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 3);
        for (int i = 0; i < labels.Length; i++)
        {
            var label = Label(labels[i], 10);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.CustomMinimumSize = new Vector2(labels.Length > 2 ? 76 : 112, 24);
            label.AddThemeStyleboxOverride("normal", PanelStyle(
                i == 0 ? new Color(0.035f, 0.20f, 0.24f, 0.95f) : new Color(0.035f, 0.050f, 0.056f, 0.95f),
                border: true));
            row.AddChild(label);
        }

        return row;
    }

    private static void StyleList(ItemList list)
    {
        list.AddThemeFontSizeOverride("font_size", 11);
        list.AddThemeColorOverride("font_color", new Color(0.78f, 0.84f, 0.86f));
        list.AddThemeColorOverride("font_selected_color", new Color(0.92f, 1.00f, 1.00f));
        list.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.018f, 0.026f, 0.032f, 0.98f), border: true));
        list.AddThemeStyleboxOverride("selected", PanelStyle(new Color(0.035f, 0.18f, 0.23f, 0.90f)));
    }

    private static Button Button(string text, Action action, Vector2? minSize = null, bool accent = false, bool warning = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = minSize ?? new Vector2(74, 28) };
        button.Pressed += action;
        button.AddThemeFontSizeOverride("font_size", 11);
        Color normal = warning
            ? new Color(0.20f, 0.13f, 0.04f, 1.0f)
            : accent
                ? new Color(0.02f, 0.35f, 0.48f, 1.0f)
                : new Color(0.10f, 0.14f, 0.20f, 1.0f);
        button.AddThemeStyleboxOverride("normal", PanelStyle(normal, border: true));
        button.AddThemeStyleboxOverride("hover", PanelStyle(new Color(0.05f, 0.32f, 0.40f, 1.0f), border: true));
        button.AddThemeStyleboxOverride("pressed", PanelStyle(new Color(0.02f, 0.50f, 0.62f, 1.0f), border: true));
        return button;
    }

    private static CheckBox Check(string text, Action? changed)
    {
        var check = new CheckBox { Text = text };
        if (changed != null)
            check.Toggled += _ => changed();
        return check;
    }

    private static SpinBox Spin(double min, double max, double value)
        => new()
        {
            MinValue = min,
            MaxValue = max,
            Value = value,
            Step = 1,
            CustomMinimumSize = new Vector2(62, 0),
        };

    private static SpinBox AddSpin(GridContainer grid, string label, double min, double max, double value)
    {
        grid.AddChild(Label(label, 9));
        SpinBox spin = Spin(min, max, value);
        grid.AddChild(spin);
        return spin;
    }

    private static RichTextLabel RichText(Vector2 minSize)
    {
        var rich = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = true,
            FitContent = false,
            CustomMinimumSize = minSize,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        rich.AddThemeFontSizeOverride("normal_font_size", 11);
        rich.AddThemeFontSizeOverride("bold_font_size", 11);
        rich.AddThemeStyleboxOverride("normal", PanelStyle(new Color(0.018f, 0.026f, 0.032f, 0.96f)));
        return rich;
    }

    private static TextEdit TextEditor(Vector2 minSize, bool readOnly)
    {
        var editor = new TextEdit
        {
            CustomMinimumSize = minSize,
            Editable = !readOnly,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        editor.AddThemeFontSizeOverride("font_size", 11);
        editor.AddThemeStyleboxOverride("normal", PanelStyle(new Color(0.018f, 0.026f, 0.032f, 0.96f)));
        return editor;
    }

    private static StyleBoxFlat PanelStyle(Color color, bool border = false)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        if (border)
        {
            style.BorderColor = new Color(0.10f, 0.32f, 0.36f, 0.90f);
            style.BorderWidthBottom = 1;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthTop = 1;
        }

        return style;
    }

    private sealed partial class BrainMapView : Control
    {
        private BrainMonitorFrame? _frame;

        public void SetFrame(BrainMonitorFrame frame)
        {
            _frame = frame;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_frame == null || _frame.Lobes.Count == 0)
                return;

            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.018f, 0.026f, 0.032f, 0.96f), filled: true);
            int maxX = Math.Max(1, _frame.Lobes.Max(lobe => lobe.X + lobe.Width));
            int maxY = Math.Max(1, _frame.Lobes.Max(lobe => lobe.Y + lobe.Height));
            float xScale = Math.Max(1.0f, (Size.X - 40.0f) / (maxX + 2));
            float yScale = Math.Max(1.0f, (Size.Y - 40.0f) / (maxY + 2));
            var origin = new Vector2(12, 12);
            var centers = new Dictionary<int, Vector2>();

            foreach (BrainLobeMonitorRow lobe in _frame.Lobes)
            {
                float visualWidth = Math.Clamp(lobe.Width * xScale * 0.45f, 46.0f, 148.0f);
                float visualHeight = Math.Clamp(lobe.Height * yScale * 4.0f, 28.0f, 86.0f);
                var rect = new Rect2(
                    origin + new Vector2(lobe.X * xScale, lobe.Y * yScale),
                    new Vector2(visualWidth, visualHeight));
                float heat = Math.Clamp(lobe.Activation, 0, 1);
                DrawRect(rect, new Color(0.035f, 0.055f + heat * 0.12f, 0.060f + heat * 0.16f, 0.92f), filled: true);
                DrawRect(rect, new Color(0.45f, 0.92f, 0.95f, 0.75f), filled: false, width: 1.2f);
                DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(5, 13), lobe.TokenText, HorizontalAlignment.Left, -1, 10, new Color(0.86f, 0.94f, 0.95f));
                DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(5, 25), $"{lobe.Width}x{lobe.Height}", HorizontalAlignment.Left, -1, 8, new Color(0.60f, 0.72f, 0.74f));
                centers[lobe.Token] = rect.GetCenter();

                int cols = Math.Clamp(lobe.Width, 4, 12);
                int rows = Math.Clamp(lobe.Height, 3, 8);
                Vector2 cell = new(Math.Max(3, (rect.Size.X - 12) / cols), Math.Max(3, (rect.Size.Y - 34) / rows));
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        float pulse = ((x * 17 + y * 11 + lobe.WinningNeuronId) % 100) / 100.0f;
                        float alpha = Math.Clamp(0.18f + heat * 0.55f + pulse * 0.20f, 0.16f, 0.88f);
                        var cellRect = new Rect2(
                            rect.Position + new Vector2(6 + x * cell.X, 30 + y * cell.Y),
                            new Vector2(Math.Max(2, cell.X - 2), Math.Max(2, cell.Y - 2)));
                        if (cellRect.End.X < rect.End.X - 3 && cellRect.End.Y < rect.End.Y - 3)
                            DrawRect(cellRect, new Color(0.00f, 0.58f, 0.78f, alpha), filled: true);
                    }
                }

                if (lobe.NeuronCount > 0)
                {
                    int x = lobe.WinningNeuronId % Math.Max(1, lobe.Width);
                    int y = lobe.WinningNeuronId / Math.Max(1, lobe.Width);
                    Vector2 p = rect.Position + new Vector2(
                        Math.Clamp((x + 0.5f) / Math.Max(1, lobe.Width) * rect.Size.X, 7, rect.Size.X - 7),
                        Math.Clamp(30 + (y + 0.5f) / Math.Max(1, lobe.Height) * Math.Max(1, rect.Size.Y - 32), 7, rect.Size.Y - 7));
                    DrawRect(new Rect2(p - new Vector2(5, 5), new Vector2(10, 10)), new Color(1.0f, 0.82f, 0.18f, 0.95f), filled: false, width: 1.5f);
                }
            }

            foreach (BrainTractMonitorRow tract in _frame.Tracts.Take(90))
            {
                if (!centers.TryGetValue(tract.SourceToken, out Vector2 a) || !centers.TryGetValue(tract.DestinationToken, out Vector2 b))
                    continue;
                float alpha = Math.Clamp(0.10f + tract.DendriteCount / 440.0f, 0.15f, 0.62f);
                Color line = tract.Index % 3 == 0
                    ? new Color(0.40f, 0.95f, 0.50f, alpha)
                    : new Color(0.25f, 0.75f, 1.0f, alpha);
                DrawLine(a, b, line, tract.Index % 7 == 0 ? 1.6f : 1.0f);
            }
        }
    }
}
