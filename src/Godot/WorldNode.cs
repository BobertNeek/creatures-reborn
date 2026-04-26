using System;
using System.Collections.Generic;
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
    public RoomNavigation? Navigation { get; private set; }

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

        // Dev helper: auto-screenshot-and-quit when launched with
        // --screenshot=<path>. Inert otherwise. See DebugScreenshot.cs.
        AddChild(new DebugScreenshot { Name = "DebugScreenshot" });

        RegisterSceneGeometry();

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
        var treehouse = GetNodeOrNull<TreehouseMetaroomNode>("Metaroom");
        if (treehouse != null) return (treehouse.MetaRoom.LeftBound, treehouse.MetaRoom.RightBound);
        return null;
    }

    /// <summary>Clamp X to room bounds.</summary>
    public float ClampX(float x)
    {
        var b = GetRoomBounds();
        if (!b.HasValue)
            return x;

        var (l, r) = b.Value;
        return Math.Clamp(x, l, r);
    }

    public float SnapToWalkableY(float x, float y, float fallback = 0f)
        => Navigation?.SnapToNearestSurface(x, y)?.Y ?? fallback;

    public Vector3 ProjectWalkStep(Vector3 from, float requestedX)
    {
        WalkableSurface? surface = Navigation?.ProjectHorizontalStep(from.X, from.Y, requestedX);
        if (surface == null)
            return new Vector3(ClampX(requestedX), from.Y, from.Z);

        return new Vector3(surface.Value.X, surface.Value.Y, from.Z);
    }

    public int FirstNavigationDirection(Vector3 from, Vector3 target)
        => Navigation?.FirstHorizontalDirection(from.X, from.Y, target.X, target.Y)
           ?? Math.Sign(target.X - from.X);

    private void RegisterSceneGeometry()
    {
        World.Map.Reset();
        Navigation = null;

        var treehouse = GetNodeOrNull<TreehouseMetaroomNode>("Metaroom");
        if (treehouse != null)
        {
            RegisterTreehouseGeometry(treehouse);
            Navigation = new RoomNavigation(World.Map);
            GD.Print($"[WorldNode] Registered Treehouse geometry: {World.Map.AllRooms.Count} rooms.");
            return;
        }

        var colony = GetNodeOrNull<ColonyMetaroomNode>("Metaroom");
        if (colony != null)
        {
            World.Map.RegisterMetaRoom(colony.MetaRoom);
            Navigation = new RoomNavigation(World.Map);
            return;
        }
    }

    private void RegisterTreehouseGeometry(TreehouseMetaroomNode treehouse)
    {
        var floorPlates = new List<FloorPlateNode>();
        foreach (Node child in GetChildren())
        {
            if (child is FloorPlateNode floorPlate)
                floorPlates.Add(floorPlate);
        }

        treehouse.RebuildRoomLayoutFromFloorPlates(floorPlates);
        World.Map.SetDimensions(treehouse.RoomWidth, treehouse.RoomHeight);
        World.Map.RegisterMetaRoom(treehouse.MetaRoom);
        RegisterStairLinks(treehouse.MetaRoom);
        RegisterElevatorLinks(treehouse.MetaRoom);
    }

    private void RegisterStairLinks(MetaRoom metaRoom)
    {
        foreach (Node child in GetChildren())
        {
            if (child is not StairsNode stairs) continue;
            if (!stairs.Enabled) continue;

            Room? from = FindEndpointRoom(metaRoom, stairs.XLeft, stairs.YLeft);
            Room? to = FindEndpointRoom(metaRoom, stairs.XRight, stairs.YRight);
            if (from == null || to == null || ReferenceEquals(from, to)) continue;

            World.Map.ConnectRooms(
                from,
                to,
                RoomLinkKind.Stair,
                stairs.XLeft,
                stairs.YLeft,
                stairs.XRight,
                stairs.YRight);
        }
    }

    private void RegisterElevatorLinks(MetaRoom metaRoom)
    {
        foreach (Node child in GetChildren())
        {
            if (child is not ElevatorNode elevator) continue;

            float x = elevator.Position.X;
            float lowY = elevator.YLow;
            float highY = elevator.YHigh;
            if (highY <= lowY)
            {
                lowY = TreehouseMetaroomNode.BottomFloorY;
                highY = TreehouseMetaroomNode.TopFloorY;
            }

            Room? low = FindEndpointRoom(metaRoom, x, lowY);
            Room? high = FindEndpointRoom(metaRoom, x, highY);
            if (low == null || high == null || ReferenceEquals(low, high)) continue;

            World.Map.ConnectRooms(
                low,
                high,
                RoomLinkKind.Elevator,
                x,
                lowY,
                x,
                highY);
        }
    }

    private static Room? FindEndpointRoom(MetaRoom metaRoom, float x, float y)
    {
        Room? exact = metaRoom.RoomAt(x, y);
        if (exact != null) return exact;

        Room? best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < metaRoom.Rooms.Count; i++)
        {
            Room room = metaRoom.Rooms[i];
            float sx = Math.Clamp(x, room.XLeft, room.XRight);
            float sy = room.FloorYAtX(sx);
            float dx = sx - x;
            float dy = sy - y;
            float distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                best = room;
                bestDistance = distance;
            }
        }

        return best;
    }
}
