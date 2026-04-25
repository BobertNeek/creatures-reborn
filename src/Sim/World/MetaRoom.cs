using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.World;

/// <summary>
/// A MetaRoom is a large area (one "screen" or zone) that contains many
/// <see cref="Room"/> objects. In DS the metarooms are the Norn Meso,
/// Corridor, Workshop, Comms Room, and Space.
///
/// This replaces the minimal <see cref="MetaroomSim"/> with a full room-graph
/// system matching c2e's architecture.
/// </summary>
public sealed class MetaRoom
{
    // ── Identity ────────────────────────────────────────────────────────────
    public int    Id   { get; set; }
    public string Name { get; set; } = "";

    // ── Bounds (world-pixel space in c2e; we use float units) ───────────
    public float X      { get; set; }
    public float Y      { get; set; }
    public float Width  { get; set; }
    public float Height { get; set; }

    /// <summary>Background image/resource name for this metaroom.</summary>
    public string Background { get; set; } = "";

    /// <summary>Default music track for this metaroom.</summary>
    public string Music { get; set; } = "";

    /// <summary>Whether the metaroom wraps horizontally (infinite scrolling).</summary>
    public bool Wraps { get; set; }

    // ── Rooms ───────────────────────────────────────────────────────────────
    private readonly List<Room> _rooms = new();
    public IReadOnlyList<Room> Rooms => _rooms;

    /// <summary>
    /// Add a room to this metaroom. Returns the room's assigned ID.
    /// </summary>
    public int AddRoom(Room room)
    {
        room.MetaRoomId = Id;
        _rooms.Add(room);
        return room.Id;
    }

    public void ClearRooms()
    {
        _rooms.Clear();
    }

    /// <summary>
    /// Find the room containing the given point, or null if outside all rooms.
    /// </summary>
    public Room? RoomAt(float x, float y)
    {
        for (int i = 0; i < _rooms.Count; i++)
        {
            if (_rooms[i].ContainsPoint(x, y))
                return _rooms[i];
        }
        return null;
    }

    /// <summary>
    /// Find all rooms that contain the given point (rooms can overlap).
    /// </summary>
    public List<Room> RoomsAt(float x, float y)
    {
        var result = new List<Room>();
        for (int i = 0; i < _rooms.Count; i++)
        {
            if (_rooms[i].ContainsPoint(x, y))
                result.Add(_rooms[i]);
        }
        return result;
    }

    /// <summary>
    /// Find the room whose floor is nearest below the point (x,y).
    /// Used for creature gravity/falling.
    /// </summary>
    public Room? NearestFloorBelow(float x, float y)
    {
        Room?  best     = null;
        float  bestDist = float.MaxValue;

        for (int i = 0; i < _rooms.Count; i++)
        {
            var r = _rooms[i];
            if (x < r.XLeft || x > r.XRight) continue;
            float floor = r.FloorYAtX(x);
            float dist  = floor - y;
            if (dist >= 0 && dist < bestDist)
            {
                best     = r;
                bestDist = dist;
            }
        }
        return best;
    }

    // ── Convenience: flat bounds (compatible with old MetaroomSim API) ──────
    public float LeftBound  => X;
    public float RightBound => X + Width;

    /// <summary>Clamp an X position to the metaroom bounds.</summary>
    public float ClampX(float x) => Math.Clamp(x, X, X + Width);

    // ── CA Tick ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Run one step of Cellular Automata diffusion across all rooms.
    /// </summary>
    public void TickCA()
    {
        for (int i = 0; i < _rooms.Count; i++)
            _rooms[i].PreTick();
        for (int i = 0; i < _rooms.Count; i++)
            _rooms[i].PostTick();
    }
}
