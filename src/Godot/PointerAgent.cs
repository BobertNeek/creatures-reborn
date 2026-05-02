using System;
using Godot;
using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Godot;

/// <summary>
/// The Hand / Pointer agent — the user's cursor in the world.
/// Matches DS's hand functionality:
///   - Click creatures to select them (shows their info in the GUI)
///   - Click + drag to pick up and carry creatures/objects
///   - Right-click a creature to tickle (positive stimulus)
///   - Middle-click or Shift+click to slap (negative stimulus)
///   - Click on food plants/gadgets to activate them
///   - Hover shows tooltips
///
/// The pointer projects the 2D mouse position onto the 3D world plane
/// (Y=FloorY) to get a world-space position for picking and interaction.
/// </summary>
[GlobalClass]
public partial class PointerAgent : Node3D
{
    // ── State ───────────────────────────────────────────────────────────────
    private Camera3D?     _camera;
    private CreatureNode? _selectedCreature;
    private CreatureNode? _carriedCreature;
    private IHandCarryable? _carriedObject;
    private CreatureNode? _leadCandidate;
    private CreatureNode? _ledCreature;
    private Vector3       _carriedCreatureOffset;
    private Vector3       _carriedObjectOffset;
    private Node3D?       _handVisual;
    private float         _worldFloorY;
    private bool          _rightButtonDown;
    private double        _rightButtonPressedAt;
    private const double  LeadHoldThreshold = 0.28;

    // ── Visual feedback ─────────────────────────────────────────────────────
    private MeshInstance3D? _cursor;
    private float _cursorPulse;

    /// <summary>The creature the user has selected (clicked on).</summary>
    public CreatureNode? SelectedCreature => _selectedCreature;

    /// <summary>Is the hand currently carrying something?</summary>
    public bool IsCarrying => _carriedCreature != null || _carriedObject != null;
    public AgentArchetype AgentArchetype => AgentCatalog.Hand;
    public AgentClassifier Classifier => AgentArchetype.Classifier;
    public int ObjectCategory => AgentArchetype.ObjectCategory;

    public override void _Ready()
    {
        _camera = GetViewport().GetCamera3D();
        BuildHandVisual();
    }

    public override void _Process(double delta)
    {
        if (_camera == null) return;

        // Project mouse onto the Z=0 vertical plane (the agent layer).
        // For our side-on orthographic camera this gives the true (X, Y) the
        // user is pointing at — so clicks on upper floors land on those floors
        // instead of being collapsed onto a single ground plane.
        var mousePos = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mousePos);
        var dir  = _camera.ProjectRayNormal(mousePos);

        if (MathF.Abs(dir.Z) > 0.001f)
        {
            float t = (0f - from.Z) / dir.Z;
            if (t > 0)
            {
                var worldPos = from + dir * t;
                Position = new Vector3(worldPos.X, worldPos.Y, 0f);
            }
        }

        // Update cursor visual
        _cursorPulse += (float)delta * 3.0f;
        if (_cursor != null)
        {
            float pulse = 0.9f + MathF.Sin(_cursorPulse) * 0.1f;
            _cursor.Scale = new Vector3(pulse, 1, pulse);
        }

        // Move carried items with the hand
        if (_carriedCreature != null)
        {
            _carriedCreature.Position = Position + _carriedCreatureOffset;
        }
        if (_carriedObject != null)
        {
            _carriedObject.CarryNode.GlobalPosition = GlobalPosition + _carriedObjectOffset;
        }

        if (_rightButtonDown && _ledCreature == null)
        {
            double now = Time.GetTicksMsec() / 1000.0;
            if (now - _rightButtonPressedAt >= LeadHoldThreshold)
                BeginLeadingCreature();
        }

        if (_ledCreature != null)
            _ledCreature.SetForcedWalkTarget(Position);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            switch (mb.ButtonIndex)
            {
                case MouseButton.Left:
                    HandleLeftClick();
                    break;
                case MouseButton.Right:
                    BeginRightClickGesture();
                    break;
                case MouseButton.Middle:
                    HandleMiddleClick();  // slap
                    break;
            }
        }

        if (@event is InputEventMouseButton rb && !rb.Pressed && rb.ButtonIndex == MouseButton.Right)
            EndRightClickGesture();

        // Shift+Left = slap
        if (@event is InputEventMouseButton mb2 && mb2.Pressed
            && mb2.ButtonIndex == MouseButton.Left && mb2.ShiftPressed)
        {
            HandleMiddleClick();
        }
    }

    // ── Left click: select / pick up / drop / call elevator ─────────────────
    private void HandleLeftClick()
    {
        // If carrying, drop at cursor (preserves the floor Y you're hovering)
        if (_carriedCreature != null)
        {
            DropCreature();
            return;
        }
        if (_carriedObject != null)
        {
            DropObject();
            return;
        }

        // Click on an elevator → cycle its target floor (top → mid → bottom → top)
        var lift = FindNearestElevator(1.2f);
        if (lift != null)
        {
            int next = lift.YHigh > lift.YLow && Position.Y < (lift.YLow + lift.YHigh) * 0.5f
                ? 0
                : 1;
            lift.GoToFloor(next);
            FlashCursor(new Color(0.6f, 0.85f, 1.0f));
            GD.Print($"[Hand] Called lift to floor {next}.");
            return;
        }

        // Try to find what we clicked on
        var creature = FindNearestCreature(1.5f);
        var carryable = FindNearestCarryable(1.0f);

        if (creature != null)
        {
            _selectedCreature = creature;
            float dist = Position.DistanceTo(creature.Position);
            if (dist < 0.8f) PickUpCreature(creature);
        }
        else if (carryable != null)
        {
            PickUpObject(carryable);
        }
    }

    private ElevatorNode? FindNearestElevator(float range)
    {
        var parent = GetParent();
        if (parent == null) return null;
        ElevatorNode? nearest = null;
        float nearestDist = range;
        foreach (Node n in parent.GetChildren())
        {
            if (n is ElevatorNode lift)
            {
                // Distance check on X only — the lift extends vertically through all floors
                float d = MathF.Abs(Position.X - lift.Position.X);
                if (d < nearestDist) { nearest = lift; nearestDist = d; }
            }
        }
        return nearest;
    }

    // ── Right click: tickle selected creature ───────────────────────────────
    private void HandleRightClick()
    {
        var creature = FindNearestCreature(2.0f);
        if (creature?.Creature != null)
        {
            StimulusTable.Apply(creature.Creature, StimulusId.PatOnBack);
            _selectedCreature = creature;
            GD.Print($"[Hand] Tickled creature!");

            // Visual feedback — brief green flash
            FlashCursor(new Color(0.3f, 1.0f, 0.3f));
        }
    }

    private void BeginRightClickGesture()
    {
        _rightButtonDown = true;
        _rightButtonPressedAt = Time.GetTicksMsec() / 1000.0;
        _leadCandidate = FindNearestCreature(1.2f) ?? _selectedCreature;
    }

    private void EndRightClickGesture()
    {
        if (!_rightButtonDown)
            return;

        bool wasLeading = _ledCreature != null;
        EndLeadingCreature();
        _rightButtonDown = false;

        if (!wasLeading)
            HandleRightClick();

        _leadCandidate = null;
    }

    private void BeginLeadingCreature()
    {
        CreatureNode? creature = _leadCandidate;
        if (creature == null || creature.Creature == null)
            return;

        _selectedCreature = creature;
        _ledCreature = creature;
        _ledCreature.SetForcedWalkTarget(Position);
        if (creature.Creature != null)
            StimulusTable.Apply(creature.Creature, StimulusId.HandHeldCreature);
        FlashCursor(new Color(0.65f, 0.85f, 1.0f));
        GD.Print("[Hand] Leading creature by hand.");
    }

    private void EndLeadingCreature()
    {
        if (_ledCreature == null)
            return;

        _ledCreature.SetForcedWalkTarget(null);
        if (_ledCreature.Creature != null)
            StimulusTable.Apply(_ledCreature.Creature, StimulusId.HandDroppedCreature);
        GD.Print("[Hand] Released creature lead.");
        _ledCreature = null;
    }

    // ── Middle click / Shift+click: slap ────────────────────────────────────
    private void HandleMiddleClick()
    {
        var creature = FindNearestCreature(2.0f);
        if (creature?.Creature != null)
        {
            StimulusTable.Apply(creature.Creature, StimulusId.Slap);
            _selectedCreature = creature;
            GD.Print($"[Hand] Slapped creature!");

            FlashCursor(new Color(1.0f, 0.2f, 0.2f));
        }
    }

    // ── Pick up / drop creatures ────────────────────────────────────────────
    private void PickUpCreature(CreatureNode creature)
    {
        _carriedCreature = creature;
        creature.SetHeldByHand(true);
        _carriedCreatureOffset = creature.Position - Position;
        if (creature.Creature != null)
            StimulusTable.Apply(creature.Creature, StimulusId.HandHeldCreature);
        GD.Print("[Hand] Picked up creature.");
    }

    private void DropCreature()
    {
        if (_carriedCreature == null) return;

        // Snap to the nearest authored floor so they don't hang in mid-air.
        // Falls back to Y=0 in scenes without registered geometry.
        var world  = GetParent() as WorldNode;
        float dropX = Position.X;
        if (world != null) dropX = world.ClampX(dropX);
        float dropY = SnapToNearestFloorY(dropX, Position.Y + _carriedCreatureOffset.Y);

        _carriedCreature.Position = new Vector3(dropX, dropY, _carriedCreature.Position.Z);
        _carriedCreature.SetHeldByHand(false);

        if (_carriedCreature.Creature != null)
            StimulusTable.Apply(_carriedCreature.Creature, StimulusId.HandDroppedCreature);

        GD.Print($"[Hand] Dropped creature at floor Y={dropY:F1}.");
        _carriedCreature = null;
    }

    /// <summary>Snap to the active world's nearest authored floor, if available.</summary>
    private float SnapToNearestFloorY(float x, float y)
    {
        var world = GetParent() as WorldNode;
        if (world?.Navigation != null)
            return world.SnapToWalkableY(x, y, 0f);

        var treehouse = GetParent()?.GetNodeOrNull<TreehouseMetaroomNode>("Metaroom");
        if (treehouse == null) return 0f;
        return treehouse.GetNearestFloorY(x, y);
    }

    // ── Pick up / drop objects ──────────────────────────────────────────────
    private void PickUpObject(IHandCarryable carryable)
    {
        _carriedObject = carryable;
        _carriedObjectOffset = carryable.CarryNode.GlobalPosition - GlobalPosition;
        carryable.PickUp(this);
        GD.Print($"[Hand] Picked up {carryable.AgentArchetype.Noun}.");
    }

    private void DropObject()
    {
        if (_carriedObject == null) return;
        var world = GetParent() as WorldNode;
        float dropX = world?.ClampX(Position.X) ?? Position.X;
        float dropY = SnapToNearestFloorY(dropX, Position.Y + _carriedObjectOffset.Y) + 0.18f;
        _carriedObject.Drop(new Vector3(dropX, dropY, 0));
        GD.Print($"[Hand] Dropped {_carriedObject.AgentArchetype.Noun} at floor Y={dropY:F1}.");
        _carriedObject = null;
    }

    // ── Queries ─────────────────────────────────────────────────────────────
    private CreatureNode? FindNearestCreature(float range)
    {
        CreatureNode? nearest = null;
        float nearestDist     = range;
        var parent = GetParent();
        if (parent == null) return null;

        foreach (Node n in parent.GetChildren())
        {
            if (n is CreatureNode cn)
            {
                float d = Position.DistanceTo(cn.Position);
                if (d < nearestDist) { nearest = cn; nearestDist = d; }
            }
        }
        return nearest;
    }

    private IHandCarryable? FindNearestCarryable(float range)
    {
        IHandCarryable? nearest = null;
        float nearestDist = range;
        var parent = GetParent();
        if (parent == null) return null;

        FindNearestCarryableIn(parent, ref nearest, ref nearestDist);
        return nearest;
    }

    private void FindNearestCarryableIn(Node parent, ref IHandCarryable? nearest, ref float nearestDist)
    {
        foreach (Node n in parent.GetChildren())
        {
            if (n is IHandCarryable carryable && carryable.CanBeCarriedByHand)
            {
                float d = GlobalPosition.DistanceTo(carryable.CarryNode.GlobalPosition);
                if (d < nearestDist) { nearest = carryable; nearestDist = d; }
            }

            if (n.GetChildCount() > 0)
                FindNearestCarryableIn(n, ref nearest, ref nearestDist);
        }
    }

    // ── Visuals ─────────────────────────────────────────────────────────────
    private void BuildHandVisual()
    {
        // Cursor ring on the ground
        _cursor = new MeshInstance3D();
        var torusMesh = new TorusMesh
        {
            InnerRadius = 0.12f,
            OuterRadius = 0.18f,
            Rings       = 16,
            RingSegments = 8,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.95f, 0.7f, 0.65f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        torusMesh.Material = mat;
        _cursor.Mesh = torusMesh;
        _cursor.RotationDegrees = new Vector3(90, 0, 0);
        _cursor.Position = new Vector3(0, 0.02f, 0);
        AddChild(_cursor);

        // Small point light to illuminate what the hand is near
        var light = new OmniLight3D
        {
            LightColor  = new Color(1f, 0.95f, 0.8f),
            LightEnergy = 0.3f,
            OmniRange   = 2.0f,
        };
        light.Position = new Vector3(0, 0.5f, 0);
        AddChild(light);
    }

    private void FlashCursor(Color col)
    {
        if (_cursor?.Mesh is TorusMesh tm && tm.Material is StandardMaterial3D mat)
        {
            mat.AlbedoColor = col;
            // Reset after a short delay via a timer
            var timer = GetTree().CreateTimer(0.3);
            timer.Timeout += () =>
            {
                if (mat != null) mat.AlbedoColor = new Color(1f, 0.95f, 0.7f, 0.65f);
            };
        }
    }
}
