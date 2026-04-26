using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Godot.Editor;

[GlobalClass]
public partial class MetaroomEditorNode : Control
{
    private const string DefaultSavePath = "res://data/metarooms/editor-draft.json";

    private MetaroomDefinition _definition = CreateDefaultDefinition();
    private MetaroomPathDefinition? _activePath;
    private MetaroomPathKind? _lineTool;
    private MetaroomObjectKind? _objectTool;
    private int _selectedObject = -1;
    private int _selectedPath = -1;
    private int _selectedPoint = -1;
    private bool _dragging;

    private Control? _canvas;
    private TextureRect? _background;
    private Node2D? _overlay;
    private Label? _status;
    private Label? _inspectorTitle;
    private SpinBox? _scaleSpin;
    private SpinBox? _rotationSpin;
    private Label? _doorHeader;
    private Label? _doorTargetMetaroomLabel;
    private LineEdit? _doorTargetMetaroomEdit;
    private Label? _doorTargetDoorLabel;
    private LineEdit? _doorTargetDoorEdit;
    private CheckBox? _doorBidirectionalCheck;
    private Label? _doorCaptureRadiusLabel;
    private SpinBox? _doorCaptureRadiusSpin;
    private FileDialog? _loadDialog;
    private FileDialog? _saveDialog;
    private FileDialog? _backgroundDialog;

    public override void _Ready()
    {
        BuildUi();
        RefreshAll();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed)
            return;

        if (key.Keycode == Key.Delete)
            DeleteSelection();
        if (key.Keycode == Key.Escape)
            FinishPath();
        if (key.Keycode == Key.Plus || key.Keycode == Key.Equal)
            ResizeSelection(1.1f);
        if (key.Keycode == Key.Minus)
            ResizeSelection(0.9f);
    }

    private void BuildUi()
    {
        AddChild(new ColorRect
        {
            Color = new Color(0.035f, 0.035f, 0.05f),
            AnchorRight = 1,
            AnchorBottom = 1,
        });

        var topBar = new HBoxContainer
        {
            AnchorRight = 1,
            OffsetLeft = 8,
            OffsetTop = 8,
            OffsetRight = -8,
            OffsetBottom = 46,
        };
        topBar.AddThemeConstantOverride("separation", 8);
        AddChild(topBar);
        topBar.AddChild(MakeButton("Back", () => GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn")));
        topBar.AddChild(MakeButton("Import Background", ImportBackground));
        topBar.AddChild(MakeButton("Load", Load));
        topBar.AddChild(MakeButton("Save", Save));
        topBar.AddChild(MakeButton("Test Play", TestPlay));

        var left = new VBoxContainer
        {
            OffsetLeft = 8,
            OffsetTop = 56,
            OffsetRight = 178,
            AnchorBottom = 1,
            OffsetBottom = -8,
        };
        left.AddThemeConstantOverride("separation", 6);
        AddChild(left);
        left.AddChild(MakeToolButton("Select", () => SelectTool()));
        left.AddChild(MakeToolButton("Floor Line", () => BeginPath(MetaroomPathKind.Floor)));
        left.AddChild(MakeToolButton("Ramp Line", () => BeginPath(MetaroomPathKind.Ramp)));
        left.AddChild(MakeToolButton("Stair Line", () => BeginPath(MetaroomPathKind.Stair)));
        left.AddChild(MakeToolButton("Finish Path", FinishPath));
        left.AddChild(MakeSeparator());
        left.AddChild(MakeToolButton("Door", () => SelectObjectTool(MetaroomObjectKind.Door)));
        left.AddChild(MakeToolButton("Elevator", () => SelectObjectTool(MetaroomObjectKind.Elevator)));
        left.AddChild(MakeToolButton("Norn Home", () => SelectObjectTool(MetaroomObjectKind.Home)));
        left.AddChild(MakeToolButton("Food Spawn", () => SelectObjectTool(MetaroomObjectKind.FoodSpawn)));
        left.AddChild(MakeToolButton("Food Dispenser", () => SelectObjectTool(MetaroomObjectKind.FoodDispenser)));
        left.AddChild(MakeToolButton("Toy", () => SelectObjectTool(MetaroomObjectKind.Toy)));
        left.AddChild(MakeToolButton("Incubator", () => SelectObjectTool(MetaroomObjectKind.Incubator)));
        left.AddChild(MakeToolButton("Norn Spawn", () => SelectObjectTool(MetaroomObjectKind.NornSpawn)));

        var right = new VBoxContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -250,
            OffsetTop = 56,
            OffsetRight = -8,
            AnchorBottom = 1,
            OffsetBottom = -8,
        };
        right.AddThemeConstantOverride("separation", 6);
        AddChild(right);
        _inspectorTitle = MakeLabel("Inspector");
        right.AddChild(_inspectorTitle);
        right.AddChild(MakeLabel("Scale"));
        _scaleSpin = MakeSpin(0.15, 8, 0.05, 1, value => UpdateSelectedScale((float)value));
        right.AddChild(_scaleSpin);
        right.AddChild(MakeLabel("Rotation"));
        _rotationSpin = MakeSpin(-180, 180, 1, 0, value => UpdateSelectedRotation((float)value));
        right.AddChild(_rotationSpin);
        right.AddChild(MakeButton("Duplicate", DuplicateSelection));
        right.AddChild(MakeButton("Delete", DeleteSelection));
        right.AddChild(MakeButton("Snap To Floor", SnapSelectionToFloor));
        right.AddChild(MakeSeparator());
        _doorHeader = MakeLabel("Door Link");
        right.AddChild(_doorHeader);
        _doorTargetMetaroomLabel = MakeLabel("Target Metaroom");
        right.AddChild(_doorTargetMetaroomLabel);
        _doorTargetMetaroomEdit = MakeLineEdit("metaroom id", UpdateSelectedDoorTargetMetaroom);
        right.AddChild(_doorTargetMetaroomEdit);
        _doorTargetDoorLabel = MakeLabel("Target Door");
        right.AddChild(_doorTargetDoorLabel);
        _doorTargetDoorEdit = MakeLineEdit("door id", UpdateSelectedDoorTargetDoor);
        right.AddChild(_doorTargetDoorEdit);
        _doorBidirectionalCheck = new CheckBox { Text = "Bidirectional" };
        _doorBidirectionalCheck.Toggled += UpdateSelectedDoorBidirectional;
        right.AddChild(_doorBidirectionalCheck);
        _doorCaptureRadiusLabel = MakeLabel("Capture Radius");
        right.AddChild(_doorCaptureRadiusLabel);
        _doorCaptureRadiusSpin = MakeSpin(0.05, 5, 0.05, 0.55, value => UpdateSelectedDoorCaptureRadius((float)value));
        right.AddChild(_doorCaptureRadiusSpin);
        SetDoorInspectorVisible(false);

        _canvas = new Panel
        {
            Name = "EditorCanvas",
            OffsetLeft = 188,
            OffsetTop = 56,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetRight = -260,
            OffsetBottom = -36,
            MouseFilter = MouseFilterEnum.Stop,
        };
        _canvas.GuiInput += OnCanvasGuiInput;
        AddChild(_canvas);

        _background = new TextureRect
        {
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _canvas.AddChild(_background);

        _overlay = new Node2D { Name = "Line2DOverlay" };
        _canvas.AddChild(_overlay);

        _status = MakeLabel("Select a tool.");
        _status.AnchorTop = 1;
        _status.AnchorBottom = 1;
        _status.OffsetLeft = 188;
        _status.OffsetTop = -28;
        _status.AnchorRight = 1;
        _status.OffsetRight = -260;
        AddChild(_status);

        BuildDialogs();
    }

    private void BuildDialogs()
    {
        _loadDialog = MakeJsonDialog("Load Metaroom JSON", FileDialog.FileModeEnum.OpenFile);
        _loadDialog.FileSelected += path =>
        {
            _definition = MetaroomDefinitionJson.Load(path);
            ClearSelection();
            RefreshAll();
        };
        AddChild(_loadDialog);

        _saveDialog = MakeJsonDialog("Save Metaroom JSON", FileDialog.FileModeEnum.SaveFile);
        _saveDialog.FileSelected += path =>
        {
            MetaroomDefinitionJson.Save(path, _definition);
            SetStatus($"Saved {path}");
        };
        AddChild(_saveDialog);

        _backgroundDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "Import Background",
        };
        _backgroundDialog.Filters = new[]
        {
            "*.png ; PNG",
            "*.jpg, *.jpeg ; JPEG",
            "*.webp ; WebP",
        };
        _backgroundDialog.FileSelected += ImportBackgroundFile;
        AddChild(_backgroundDialog);
    }

    private void OnCanvasGuiInput(InputEvent @event)
    {
        if (_canvas == null)
            return;

        if (@event is InputEventMouseButton button)
        {
            Vector2 canvasPos = button.Position;
            MetaroomPoint world = CanvasToWorld(canvasPos);

            if (button.ButtonIndex == MouseButton.Left && button.Pressed)
            {
                if (_lineTool != null)
                    AddPathPoint(world);
                else if (_objectTool != null)
                    AddObject(_objectTool.Value, world);
                else
                    SelectAt(canvasPos);
            }

            if (button.ButtonIndex == MouseButton.Left && !button.Pressed)
                _dragging = false;

            if (button.ButtonIndex == MouseButton.Right && button.Pressed)
                FinishPath();
        }
        else if (@event is InputEventMouseMotion motion && _dragging)
        {
            MoveSelection(CanvasToWorld(motion.Position));
        }
    }

    private void BeginPath(MetaroomPathKind kind)
    {
        FinishPath();
        _lineTool = kind;
        _objectTool = null;
        _activePath = new MetaroomPathDefinition
        {
            Id = $"{kind.ToString().ToLowerInvariant()}-{_definition.Paths.Count + 1}",
            Name = $"{kind} {_definition.Paths.Count + 1}",
            Kind = kind,
        };
        _definition.Paths.Add(_activePath);
        _selectedPath = _definition.Paths.Count - 1;
        _selectedPoint = -1;
        SetStatus($"Drawing {kind}. Left-click points, right-click or Escape to finish.");
        RefreshAll();
    }

    private void FinishPath()
    {
        if (_activePath != null && _activePath.Points.Count < 2)
            _definition.Paths.Remove(_activePath);

        _activePath = null;
        _lineTool = null;
        SetStatus("Path finished.");
        RefreshAll();
    }

    private void AddPathPoint(MetaroomPoint point)
    {
        if (_activePath == null)
            return;

        _activePath.Points.Add(point);
        _selectedPath = _definition.Paths.IndexOf(_activePath);
        _selectedPoint = _activePath.Points.Count - 1;
        _selectedObject = -1;
        RefreshAll();
    }

    private void SelectObjectTool(MetaroomObjectKind kind)
    {
        FinishPath();
        _objectTool = kind;
        _lineTool = null;
        SetStatus($"Placing {kind}. Left-click the background.");
    }

    private void SelectTool()
    {
        FinishPath();
        _objectTool = null;
        _lineTool = null;
        SetStatus("Select, drag, resize, rotate, duplicate, or delete objects and path points.");
    }

    private void AddObject(MetaroomObjectKind kind, MetaroomPoint point)
    {
        var obj = new MetaroomObjectDefinition
        {
            Id = $"{kind.ToString().ToLowerInvariant()}-{_definition.Objects.Count + 1}",
            Name = kind.ToString(),
            Kind = kind,
            Position = point,
            Scale = 1,
        };

        if (kind == MetaroomObjectKind.Door)
        {
            obj.Door = new DoorDefinition
            {
                TargetMetaroomId = _definition.Id,
                TargetDoorId = obj.Id,
                TransitionMode = DoorTransitionMode.Portal,
            };
        }
        if (kind == MetaroomObjectKind.Elevator)
            obj.Elevator = new ElevatorDefinition { YLow = point.Y, YHigh = point.Y + 3 };
        if (kind == MetaroomObjectKind.Home)
            obj.Home = new HomeDefinition();
        if (kind == MetaroomObjectKind.FoodSpawn)
            obj.Food = new FoodDefinition();
        if (kind is MetaroomObjectKind.FoodDispenser or MetaroomObjectKind.Toy)
            obj.Gadget = new GadgetDefinition();

        _definition.Objects.Add(obj);
        _selectedObject = _definition.Objects.Count - 1;
        _selectedPath = -1;
        _selectedPoint = -1;
        RefreshAll();
    }

    private void SelectAt(Vector2 canvasPos)
    {
        _selectedObject = FindObjectAt(canvasPos);
        if (_selectedObject >= 0)
        {
            _selectedPath = -1;
            _selectedPoint = -1;
            _dragging = true;
            RefreshInspector();
            return;
        }

        (_selectedPath, _selectedPoint) = FindPointAt(canvasPos);
        _dragging = _selectedPath >= 0 && _selectedPoint >= 0;
        RefreshAll();
    }

    private int FindObjectAt(Vector2 canvasPos)
    {
        for (int i = _definition.Objects.Count - 1; i >= 0; i--)
        {
            Vector2 p = WorldToCanvas(_definition.Objects[i].Position);
            float radius = MathF.Max(8, _definition.Objects[i].Scale * 12);
            if (p.DistanceTo(canvasPos) <= radius)
                return i;
        }

        return -1;
    }

    private (int PathIndex, int PointIndex) FindPointAt(Vector2 canvasPos)
    {
        for (int i = 0; i < _definition.Paths.Count; i++)
        {
            MetaroomPathDefinition path = _definition.Paths[i];
            for (int j = 0; j < path.Points.Count; j++)
            {
                if (WorldToCanvas(path.Points[j]).DistanceTo(canvasPos) <= 8)
                    return (i, j);
            }
        }

        return (-1, -1);
    }

    private void MoveSelection(MetaroomPoint point)
    {
        if (_selectedObject >= 0)
            _definition.Objects[_selectedObject].Position = point;
        else if (_selectedPath >= 0 && _selectedPoint >= 0)
            _definition.Paths[_selectedPath].Points[_selectedPoint] = point;

        RefreshAll();
    }

    private void ResizeSelection(float factor)
    {
        if (_selectedObject < 0)
            return;

        MetaroomObjectDefinition obj = _definition.Objects[_selectedObject];
        obj.Scale = Math.Clamp(obj.Scale * factor, 0.15f, 8f);
        RefreshAll();
    }

    private void UpdateSelectedScale(float value)
    {
        if (_selectedObject < 0)
            return;

        _definition.Objects[_selectedObject].Scale = value;
        RefreshAll();
    }

    private void UpdateSelectedRotation(float value)
    {
        if (_selectedObject < 0)
            return;

        _definition.Objects[_selectedObject].RotationDegrees = value;
        RefreshAll();
    }

    private void UpdateSelectedDoorTargetMetaroom(string value)
    {
        if (!TryGetSelectedDoor(out MetaroomObjectDefinition? obj, out DoorDefinition? door))
            return;

        door!.TargetMetaroomId = value.Trim();
        SetStatus(ValidateDoorLink(obj!));
    }

    private void UpdateSelectedDoorTargetDoor(string value)
    {
        if (!TryGetSelectedDoor(out MetaroomObjectDefinition? obj, out DoorDefinition? door))
            return;

        door!.TargetDoorId = value.Trim();
        SetStatus(ValidateDoorLink(obj!));
    }

    private void UpdateSelectedDoorBidirectional(bool value)
    {
        if (!TryGetSelectedDoor(out MetaroomObjectDefinition? obj, out DoorDefinition? door))
            return;

        door!.Bidirectional = value;
        SetStatus(ValidateDoorLink(obj!));
    }

    private void UpdateSelectedDoorCaptureRadius(float value)
    {
        if (!TryGetSelectedDoor(out MetaroomObjectDefinition? obj, out DoorDefinition? door))
            return;

        door!.CaptureRadius = value;
        SetStatus(ValidateDoorLink(obj!));
    }

    private void DuplicateSelection()
    {
        if (_selectedObject < 0)
            return;

        MetaroomObjectDefinition source = _definition.Objects[_selectedObject];
        var copy = MetaroomDefinitionJson.Deserialize(MetaroomDefinitionJson.Serialize(new MetaroomDefinition
        {
            Id = "copy",
            Objects = { source },
        })).Objects[0];
        copy.Id = $"{source.Id}-copy-{_definition.Objects.Count + 1}";
        copy.Position = new MetaroomPoint(source.Position.X + 0.6f, source.Position.Y + 0.3f);
        _definition.Objects.Add(copy);
        _selectedObject = _definition.Objects.Count - 1;
        RefreshAll();
    }

    private void DeleteSelection()
    {
        if (_selectedObject >= 0)
        {
            _definition.Objects.RemoveAt(_selectedObject);
            ClearSelection();
            RefreshAll();
            return;
        }

        if (_selectedPath >= 0 && _selectedPoint >= 0)
        {
            MetaroomPathDefinition path = _definition.Paths[_selectedPath];
            path.Points.RemoveAt(_selectedPoint);
            if (path.Points.Count < 2)
                _definition.Paths.RemoveAt(_selectedPath);
            ClearSelection();
            RefreshAll();
        }
    }

    private void SnapSelectionToFloor()
    {
        if (_selectedObject < 0)
            return;

        MetaroomObjectDefinition obj = _definition.Objects[_selectedObject];
        float bestY = obj.Position.Y;
        float bestDistance = float.MaxValue;
        foreach (MetaroomPathDefinition path in _definition.Paths)
        {
            for (int i = 0; i < path.Points.Count - 1; i++)
            {
                if (TryProjectToSegment(obj.Position, path.Points[i], path.Points[i + 1], out MetaroomPoint projected, out float distance)
                    && distance < bestDistance)
                {
                    bestY = projected.Y;
                    bestDistance = distance;
                }
            }
        }

        obj.Position = new MetaroomPoint(obj.Position.X, bestY);
        RefreshAll();
    }

    private static bool TryProjectToSegment(
        MetaroomPoint point,
        MetaroomPoint a,
        MetaroomPoint b,
        out MetaroomPoint projected,
        out float distance)
    {
        projected = new MetaroomPoint(point.X, point.Y);
        distance = float.MaxValue;
        float minX = MathF.Min(a.X, b.X);
        float maxX = MathF.Max(a.X, b.X);
        if (point.X < minX || point.X > maxX || MathF.Abs(a.X - b.X) < 0.001f)
            return false;

        float t = (point.X - a.X) / (b.X - a.X);
        float y = a.Y + t * (b.Y - a.Y);
        projected = new MetaroomPoint(point.X, y);
        distance = MathF.Abs(point.Y - y);
        return true;
    }

    private void ImportBackground()
    {
        _backgroundDialog?.PopupCenteredRatio(0.75f);
    }

    private void ImportBackgroundFile(string sourcePath)
    {
        string importDirectory = ProjectSettings.GlobalizePath("res://art/metaroom/imported");
        Directory.CreateDirectory(importDirectory);
        string fileName = Path.GetFileName(sourcePath);
        string destination = Path.Combine(importDirectory, fileName);
        File.Copy(sourcePath, destination, overwrite: true);
        _definition.BackgroundPath = $"res://art/metaroom/imported/{fileName}";
        RefreshAll();
    }

    private void Load()
    {
        _loadDialog?.PopupCenteredRatio(0.75f);
    }

    private void Save()
    {
        _saveDialog?.PopupCenteredRatio(0.75f);
    }

    private void TestPlay()
    {
        string path = ProjectSettings.GlobalizePath("user://metaroom-editor-test.json");
        MetaroomDefinitionJson.Save(path, _definition);
        MetaroomEditorSession.SetDefinitionPaths(path);
        GetTree().ChangeSceneToFile("res://scenes/MetaroomWorld.tscn");
    }

    private void RefreshAll()
    {
        RefreshBackground();
        RefreshOverlay();
        RefreshInspector();
    }

    private void RefreshBackground()
    {
        if (_background == null)
            return;

        Texture2D? texture = LoadTexture(_definition.BackgroundPath);
        _background.Texture = texture;
    }

    private void RefreshOverlay()
    {
        if (_overlay == null)
            return;

        foreach (Node child in _overlay.GetChildren())
        {
            _overlay.RemoveChild(child);
            child.QueueFree();
        }

        for (int i = 0; i < _definition.Paths.Count; i++)
        {
            MetaroomPathDefinition path = _definition.Paths[i];
            var line = new Line2D
            {
                Name = $"Path_{path.Id}",
                Width = path.Kind == MetaroomPathKind.Floor ? 3 : 4,
                DefaultColor = PathColor(path.Kind),
            };
            foreach (MetaroomPoint point in path.Points)
                line.AddPoint(WorldToCanvas(point));
            _overlay.AddChild(line);

            for (int j = 0; j < path.Points.Count; j++)
                _overlay.AddChild(BuildPointHandle(path.Points[j], i == _selectedPath && j == _selectedPoint));
        }

        for (int i = 0; i < _definition.Objects.Count; i++)
            _overlay.AddChild(BuildObjectHandle(_definition.Objects[i], i == _selectedObject));
    }

    private void RefreshInspector()
    {
        if (_inspectorTitle == null || _scaleSpin == null || _rotationSpin == null)
            return;

        if (_selectedObject >= 0)
        {
            MetaroomObjectDefinition obj = _definition.Objects[_selectedObject];
            _inspectorTitle.Text = $"{obj.Kind}: {obj.Id}";
            _scaleSpin.Value = obj.Scale;
            _rotationSpin.Value = obj.RotationDegrees;
            bool isDoor = obj.Kind == MetaroomObjectKind.Door;
            SetDoorInspectorVisible(isDoor);
            if (isDoor)
            {
                DoorDefinition door = EnsureDoorDefinition(obj);
                if (_doorTargetMetaroomEdit != null)
                    _doorTargetMetaroomEdit.Text = door.TargetMetaroomId;
                if (_doorTargetDoorEdit != null)
                    _doorTargetDoorEdit.Text = door.TargetDoorId;
                if (_doorBidirectionalCheck != null)
                    _doorBidirectionalCheck.ButtonPressed = door.Bidirectional;
                if (_doorCaptureRadiusSpin != null)
                    _doorCaptureRadiusSpin.Value = door.CaptureRadius;
                SetStatus(ValidateDoorLink(obj));
            }
        }
        else if (_selectedPath >= 0 && _selectedPoint >= 0)
        {
            _inspectorTitle.Text = $"{_definition.Paths[_selectedPath].Kind} point {_selectedPoint + 1}";
            SetDoorInspectorVisible(false);
        }
        else
        {
            _inspectorTitle.Text = "Inspector";
            SetDoorInspectorVisible(false);
        }
    }

    private bool TryGetSelectedDoor(out MetaroomObjectDefinition? obj, out DoorDefinition? door)
    {
        obj = null;
        door = null;
        if (_selectedObject < 0 || _selectedObject >= _definition.Objects.Count)
            return false;

        obj = _definition.Objects[_selectedObject];
        if (obj.Kind != MetaroomObjectKind.Door)
            return false;

        door = EnsureDoorDefinition(obj);
        return true;
    }

    private static DoorDefinition EnsureDoorDefinition(MetaroomObjectDefinition obj)
    {
        obj.Door ??= new DoorDefinition
        {
            TargetMetaroomId = string.Empty,
            TargetDoorId = string.Empty,
            TransitionMode = DoorTransitionMode.Portal,
        };
        return obj.Door;
    }

    private string ValidateDoorLink(MetaroomObjectDefinition obj)
    {
        DoorDefinition door = EnsureDoorDefinition(obj);
        if (string.IsNullOrWhiteSpace(door.TargetMetaroomId))
            return "Door link missing target metaroom.";
        if (string.IsNullOrWhiteSpace(door.TargetDoorId))
            return "Door link missing target door.";

        if (door.TargetMetaroomId == _definition.Id)
        {
            bool localTargetExists = _definition.Objects.Exists(candidate =>
                candidate.Kind == MetaroomObjectKind.Door && candidate.Id == door.TargetDoorId);
            return localTargetExists
                ? "Door link valid."
                : $"Door link target '{door.TargetDoorId}' is missing in this metaroom.";
        }

        return $"Door links to {door.TargetMetaroomId}:{door.TargetDoorId}. Load paired metaroom to validate.";
    }

    private void SetDoorInspectorVisible(bool visible)
    {
        Control?[] controls =
        {
            _doorHeader,
            _doorTargetMetaroomLabel,
            _doorTargetMetaroomEdit,
            _doorTargetDoorLabel,
            _doorTargetDoorEdit,
            _doorBidirectionalCheck,
            _doorCaptureRadiusLabel,
            _doorCaptureRadiusSpin,
        };

        foreach (Control? control in controls)
        {
            if (control != null)
                control.Visible = visible;
        }
    }

    private Node2D BuildPointHandle(MetaroomPoint point, bool selected)
    {
        var line = new Line2D
        {
            Width = selected ? 8 : 5,
            DefaultColor = selected ? new Color(1f, 0.95f, 0.3f) : new Color(0.95f, 0.95f, 1f),
        };
        Vector2 p = WorldToCanvas(point);
        line.AddPoint(p + new Vector2(-4, 0));
        line.AddPoint(p + new Vector2(4, 0));
        return line;
    }

    private Control BuildObjectHandle(MetaroomObjectDefinition obj, bool selected)
    {
        Vector2 p = WorldToCanvas(obj.Position);
        float size = MathF.Max(18, obj.Scale * 24);
        var label = new Label
        {
            Text = ObjectGlyph(obj.Kind),
            Position = p - new Vector2(size * 0.5f, size * 0.5f),
            Size = new Vector2(size, size),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = selected ? new Color(1f, 0.92f, 0.35f) : new Color(0.8f, 0.95f, 1f),
        };
        label.AddThemeFontSizeOverride("font_size", (int)Math.Clamp(size, 14, 36));
        return label;
    }

    private Vector2 WorldToCanvas(MetaroomPoint point)
    {
        Vector2 size = _canvas?.Size ?? new Vector2(1024, 512);
        float x = (point.X + _definition.WorldWidth * 0.5f) / _definition.WorldWidth * size.X;
        float y = size.Y - point.Y / _definition.WorldHeight * size.Y;
        return new Vector2(x, y);
    }

    private MetaroomPoint CanvasToWorld(Vector2 point)
    {
        Vector2 size = _canvas?.Size ?? new Vector2(1024, 512);
        float x = point.X / MathF.Max(1, size.X) * _definition.WorldWidth - _definition.WorldWidth * 0.5f;
        float y = (size.Y - point.Y) / MathF.Max(1, size.Y) * _definition.WorldHeight;
        return new MetaroomPoint(x, y);
    }

    private void ClearSelection()
    {
        _selectedObject = -1;
        _selectedPath = -1;
        _selectedPoint = -1;
        _dragging = false;
    }

    private void SetStatus(string text)
    {
        if (_status != null)
            _status.Text = text;
    }

    private static Color PathColor(MetaroomPathKind kind) => kind switch
    {
        MetaroomPathKind.Ramp => new Color(0.4f, 1.0f, 0.6f),
        MetaroomPathKind.Stair => new Color(0.55f, 0.75f, 1.0f),
        _ => new Color(1.0f, 0.65f, 0.25f),
    };

    private static string ObjectGlyph(MetaroomObjectKind kind) => kind switch
    {
        MetaroomObjectKind.Door => "D",
        MetaroomObjectKind.Elevator => "E",
        MetaroomObjectKind.Home => "H",
        MetaroomObjectKind.FoodSpawn => "F",
        MetaroomObjectKind.FoodDispenser => "V",
        MetaroomObjectKind.Toy => "T",
        MetaroomObjectKind.Incubator => "I",
        MetaroomObjectKind.NornSpawn => "N",
        _ => "?",
    };

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(110, 32),
        };
        button.Pressed += onClick;
        return button;
    }

    private static Button MakeToolButton(string text, Action onClick)
        => MakeButton(text, onClick);

    private static Label MakeLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 12);
        return label;
    }

    private static Control MakeSeparator()
        => new HSeparator { CustomMinimumSize = new Vector2(1, 8) };

    private static SpinBox MakeSpin(double min, double max, double step, double value, Action<double> changed)
    {
        var spin = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
        };
        spin.ValueChanged += value => changed(value);
        return spin;
    }

    private static LineEdit MakeLineEdit(string placeholder, Action<string> changed)
    {
        var edit = new LineEdit
        {
            PlaceholderText = placeholder,
            CustomMinimumSize = new Vector2(180, 30),
        };
        edit.TextChanged += text => changed(text);
        return edit;
    }

    private static FileDialog MakeJsonDialog(string title, FileDialog.FileModeEnum mode)
    {
        var dialog = new FileDialog
        {
            FileMode = mode,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = title,
        };
        dialog.Filters = new[] { "*.json ; Metaroom JSON" };
        return dialog;
    }

    private static Texture2D? LoadTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string local = path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("user://", StringComparison.OrdinalIgnoreCase)
                ? ProjectSettings.GlobalizePath(path)
                : path;

        if (File.Exists(local))
        {
            Image image = Image.LoadFromFile(local);
            if (image != null)
                return ImageTexture.CreateFromImage(image);
        }

        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    private static MetaroomDefinition CreateDefaultDefinition()
        => new()
        {
            Id = "editor-draft",
            Name = "Editor Draft",
            BackgroundPath = "res://art/metaroom/metaroom-right-connector-v2.png",
            BackgroundWidth = 1983,
            BackgroundHeight = 793,
            WorldWidth = 40,
            WorldHeight = 13.5f,
            BackdropCenterY = 6.75f,
        };
}
