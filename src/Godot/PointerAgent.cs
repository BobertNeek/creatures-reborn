using System;
using Godot;
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
    private FoodNode?     _carriedFood;
    private Node3D?       _handVisual;
    private float         _worldFloorY;

    // ── Visual feedback ─────────────────────────────────────────────────────
    private MeshInstance3D? _cursor;
    private float _cursorPulse;

    /// <summary>The creature the user has selected (clicked on).</summary>
    public CreatureNode? SelectedCreature => _selectedCreature;

    /// <summary>Is the hand currently carrying something?</summary>
    public bool IsCarrying => _carriedCreature != null || _carriedFood != null;

    public override void _Ready()
    {
        _camera = GetViewport().GetCamera3D();
        BuildHandVisual();
    }

    public override void _Process(double delta)
    {
        if (_camera == null) return;

        // Project mouse to world plane
        var mousePos = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mousePos);
        var dir  = _camera.ProjectRayNormal(mousePos);

        // Intersect with Y=FloorY plane
        if (MathF.Abs(dir.Y) > 0.001f)
        {
            float t = (_worldFloorY - from.Y) / dir.Y;
            if (t > 0)
            {
                var worldPos = from + dir * t;
                Position = worldPos;
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
            _carriedCreature.Position = Position + new Vector3(0, 1.2f, 0);
        }
        if (_carriedFood != null)
        {
            _carriedFood.Position = Position + new Vector3(0, 0.5f, 0);
        }
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
                    HandleRightClick();   // tickle
                    break;
                case MouseButton.Middle:
                    HandleMiddleClick();  // slap
                    break;
            }
        }

        // Shift+Left = slap
        if (@event is InputEventMouseButton mb2 && mb2.Pressed
            && mb2.ButtonIndex == MouseButton.Left && mb2.ShiftPressed)
        {
            HandleMiddleClick();
        }
    }

    // ── Left click: select / pick up / drop / activate ──────────────────────
    private void HandleLeftClick()
    {
        // If carrying, drop
        if (_carriedCreature != null)
        {
            DropCreature();
            return;
        }
        if (_carriedFood != null)
        {
            DropFood();
            return;
        }

        // Try to find what we clicked on
        var creature = FindNearestCreature(1.5f);
        var food     = FindNearestFood(1.0f);

        if (creature != null)
        {
            _selectedCreature = creature;
            // Double-click distance → pick up
            float dist = Position.DistanceTo(creature.Position);
            if (dist < 0.8f)
            {
                PickUpCreature(creature);
            }
        }
        else if (food != null && !food.IsConsumed)
        {
            PickUpFood(food);
        }
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
        if (creature.Creature != null)
            StimulusTable.Apply(creature.Creature, StimulusId.HandHeldCreature);
        GD.Print("[Hand] Picked up creature.");
    }

    private void DropCreature()
    {
        if (_carriedCreature == null) return;

        // Find the world node for bounds clamping
        var world = GetParent() as WorldNode;
        float dropX = Position.X;
        if (world != null) dropX = world.ClampX(dropX);

        _carriedCreature.Position = new Vector3(dropX, 0, _carriedCreature.Position.Z);

        if (_carriedCreature.Creature != null)
            StimulusTable.Apply(_carriedCreature.Creature, StimulusId.HandDroppedCreature);

        GD.Print("[Hand] Dropped creature.");
        _carriedCreature = null;
    }

    // ── Pick up / drop food ─────────────────────────────────────────────────
    private void PickUpFood(FoodNode food)
    {
        _carriedFood = food;
        food.PickUp(this);
        GD.Print("[Hand] Picked up food.");
    }

    private void DropFood()
    {
        if (_carriedFood == null) return;
        _carriedFood.Drop(new Vector3(Position.X, 0, 0));
        _carriedFood = null;
        GD.Print("[Hand] Dropped food.");
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

    private FoodNode? FindNearestFood(float range)
    {
        FoodNode? nearest = null;
        float nearestDist = range;
        var parent = GetParent();
        if (parent == null) return null;

        foreach (Node n in parent.GetChildren())
        {
            if (n is FoodNode food && !food.IsConsumed && !food.IsHeld)
            {
                float d = Position.DistanceTo(food.Position);
                if (d < nearestDist) { nearest = food; nearestDist = d; }
            }
        }
        return nearest;
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
