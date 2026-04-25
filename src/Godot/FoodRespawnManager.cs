using System.Collections.Generic;
using Godot;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Godot;

/// <summary>
/// Watches a set of food spawn points and respawns a <see cref="FoodNode"/>
/// at each point after a configurable delay whenever the previous item was consumed.
///
/// Add one of these as a sibling of the food nodes in your scene, then add each
/// food node's initial world position to <see cref="_spawnPoints"/> — or let
/// _Ready() auto-discover every FoodNode sibling and record its position.
/// </summary>
[GlobalClass]
public partial class FoodRespawnManager : Node3D
{
    /// <summary>Seconds after consumption before a new food item appears.</summary>
    [Export] public float RespawnDelay = 15.0f;

    // Per-spawn-point state
    private readonly struct SpawnPoint
    {
        public readonly Vector3 Position;
        public readonly FoodKind FoodKind;
        public readonly float   GlycogenAmount;
        public readonly float   ATPAmount;
        public SpawnPoint(Vector3 pos, FoodKind kind, float g, float a)
        {
            Position = pos;
            FoodKind = kind;
            GlycogenAmount = g;
            ATPAmount = a;
        }
    }

    private readonly List<SpawnPoint> _points = new();
    private readonly List<float>      _timers = new();   // >0 = counting down to respawn

    // ─────────────────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        // Auto-discover all FoodNode siblings and register their spawn points
        if (GetParent() == null) return;

        foreach (Node n in GetParent().GetChildren())
        {
            if (n is FoodNode food)
            {
                _points.Add(new SpawnPoint(food.GlobalPosition, food.FoodKind, food.GlycogenAmount, food.ATPAmount));
                _timers.Add(0f);
            }
        }

        GD.Print($"[FoodRespawnManager] Tracking {_points.Count} spawn points.");
    }

    public override void _Process(double delta)
    {
        if (GetParent() == null) return;

        // Build a quick lookup of occupied spawn points (by nearest live food node)
        for (int i = 0; i < _points.Count; i++)
        {
            if (_timers[i] <= 0f)
            {
                // Check if there is still a live food near this point
                if (!HasFoodAt(_points[i].Position, 0.5f))
                {
                    // Start countdown
                    _timers[i] = RespawnDelay;
                }
            }
            else
            {
                _timers[i] -= (float)delta;
                if (_timers[i] <= 0f)
                {
                    // Re-check: only spawn if the slot is still empty
                    if (!HasFoodAt(_points[i].Position, 0.5f))
                        SpawnFood(i);
                    else
                        _timers[i] = 0f; // already occupied, clear timer
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    private bool HasFoodAt(Vector3 worldPos, float tolerance)
    {
        foreach (Node n in GetParent()!.GetChildren())
        {
            if (n is FoodNode food && !food.IsConsumed)
            {
                if (food.GlobalPosition.DistanceTo(worldPos) < tolerance)
                    return true;
            }
        }
        return false;
    }

    private void SpawnFood(int idx)
    {
        var food = new FoodNode
        {
            GlycogenAmount = _points[idx].GlycogenAmount,
            ATPAmount      = _points[idx].ATPAmount,
            FoodKind       = _points[idx].FoodKind,
        };
        GetParent()!.AddChild(food);
        food.GlobalPosition = _points[idx].Position;
        _timers[idx] = 0f;
        GD.Print($"[FoodRespawnManager] Respawned food at {_points[idx].Position}.");
    }
}
