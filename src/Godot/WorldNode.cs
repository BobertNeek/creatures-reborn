using System;
using Godot;
using CreaturesReborn.Sim.World;
using CreaturesReborn.Sim.Agent;

namespace CreaturesReborn.Godot;

/// <summary>
/// Central world simulation driver. This node owns the <see cref="GameWorld"/>
/// (pure C# simulation) and ticks it at 20 Hz, synchronised with the Godot
/// frame loop.
///
/// All game systems route through here:
///   - GameWorld.Tick() drives CA diffusion, agent ticking, and time
///   - Child nodes (creatures, agents, metaroom) query the world for state
///   - The PointerAgent handles user input and dispatches events
///
/// Place this as the root of the main scene. It auto-discovers child nodes
/// of known types and wires them into the simulation.
/// </summary>
[GlobalClass]
public partial class WorldNode : Node3D
{
    // ── Configuration ───────────────────────────────────────────────────────
    [Export] public int   BreedingLimit      = 6;
    [Export] public int   TotalPopulationMax = 16;
    [Export] public float TickRate           = 20.0f;  // Hz

    // ── The sim world ───────────────────────────────────────────────────────
    public GameWorld World { get; private set; } = new();

    // ── Tick accumulator ────────────────────────────────────────────────────
    private float _tickAccum;
    private float TickInterval => 1.0f / TickRate;

    // ── Stats ───────────────────────────────────────────────────────────────
    public int    TotalTicks    { get; private set; }
    public int    CreatureCount { get; private set; }
    public string SeasonName    => World.Time.SeasonName;
    public float  TimeOfDay     => World.Time.TimeOfDay;
    public int    Day           => World.Time.Day;
    public int    Year          => World.Time.Year;

    public override void _Ready()
    {
        World.BreedingLimit      = BreedingLimit;
        World.TotalPopulationMax = TotalPopulationMax;

        GD.Print("[WorldNode] Simulation world initialised.");
        GD.Print($"  Tick rate: {TickRate} Hz, Breeding limit: {BreedingLimit}");
    }

    public override void _Process(double delta)
    {
        _tickAccum += (float)delta;

        // Run simulation ticks at fixed rate
        while (_tickAccum >= TickInterval)
        {
            _tickAccum -= TickInterval;
            World.Tick();
            TotalTicks++;
        }

        // Count creatures for UI
        CreatureCount = 0;
        foreach (Node child in GetChildren())
        {
            if (child is CreatureNode cn && cn.Creature != null)
                CreatureCount++;
        }
    }

    // ── Helper: get the active metaroom bounds ──────────────────────────────
    /// <summary>
    /// Find room bounds from whichever metaroom type is in the scene.
    /// Used by various systems that need walkable area limits.
    /// </summary>
    public (float left, float right)? GetRoomBounds()
    {
        var mm = GetNodeOrNull<MetaroomNode>("Metaroom");
        if (mm != null) return (mm.Sim.LeftBound, mm.Sim.RightBound);
        var colony = GetNodeOrNull<ColonyMetaroomNode>("Metaroom");
        if (colony != null) return (colony.MetaRoom.LeftBound, colony.MetaRoom.RightBound);
        return null;
    }

    /// <summary>Clamp X to room bounds.</summary>
    public float ClampX(float x)
    {
        var b = GetRoomBounds();
        return b is var (l, r) ? Math.Clamp(x, l, r) : x;
    }
}
