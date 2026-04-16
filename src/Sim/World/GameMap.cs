using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.World;

/// <summary>
/// The world map. Contains all <see cref="MetaRoom"/> instances and manages
/// door connections between rooms, room ID allocation, and CA ticking.
/// This is the equivalent of openc2e's <c>Map</c> class and the c2e
/// <c>mapd</c>/<c>addm</c>/<c>addr</c> CAOS commands.
/// </summary>
public sealed class GameMap
{
    // ── Map dimensions (virtual world space) ────────────────────────────────
    public float WorldWidth  { get; private set; } = 10000;
    public float WorldHeight { get; private set; } = 10000;

    // ── Storage ─────────────────────────────────────────────────────────────
    private readonly List<MetaRoom> _metaRooms = new();
    private readonly List<Room>     _allRooms  = new();

    private int _nextMetaRoomId = 0;
    private int _nextRoomId     = 0;

    public IReadOnlyList<MetaRoom> MetaRooms => _metaRooms;
    public IReadOnlyList<Room>     AllRooms  => _allRooms;

    // ── Configuration ───────────────────────────────────────────────────────

    public void SetDimensions(float w, float h)
    {
        WorldWidth  = w;
        WorldHeight = h;
    }

    // ── MetaRoom management ─────────────────────────────────────────────────

    /// <summary>
    /// Create and register a new metaroom. Returns the new metaroom's ID.
    /// Mirrors the CAOS <c>addm</c> command.
    /// </summary>
    public MetaRoom AddMetaRoom(float x, float y, float width, float height, string background)
    {
        var mr = new MetaRoom
        {
            Id         = _nextMetaRoomId++,
            X          = x,
            Y          = y,
            Width      = width,
            Height     = height,
            Background = background,
        };
        _metaRooms.Add(mr);
        return mr;
    }

    public MetaRoom? GetMetaRoom(int id)
    {
        for (int i = 0; i < _metaRooms.Count; i++)
            if (_metaRooms[i].Id == id) return _metaRooms[i];
        return null;
    }

    // ── Room management ─────────────────────────────────────────────────────

    /// <summary>
    /// Create a new room and add it to the specified metaroom.
    /// Mirrors the CAOS <c>addr</c> command.
    /// Parameters are the trapezoid corners: xL, xR, yLT, yRT, yLB, yRB.
    /// </summary>
    public Room AddRoom(int metaRoomId, float xLeft, float xRight,
                        float yLeftCeiling, float yRightCeiling,
                        float yLeftFloor, float yRightFloor)
    {
        var room = new Room
        {
            Id            = _nextRoomId++,
            XLeft         = xLeft,
            XRight        = xRight,
            YLeftCeiling  = yLeftCeiling,
            YRightCeiling = yRightCeiling,
            YLeftFloor    = yLeftFloor,
            YRightFloor   = yRightFloor,
        };

        var mr = GetMetaRoom(metaRoomId);
        mr?.AddRoom(room);
        _allRooms.Add(room);
        return room;
    }

    // ── Doors ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Set the permeability of the door between two rooms (0-100).
    /// Creates the door if it doesn't exist. Mirrors the CAOS <c>door</c> command.
    /// </summary>
    public void SetDoor(Room r1, Room r2, int perm)
    {
        r1.Doors[r2] = perm;
        r2.Doors[r1] = perm;
    }

    public bool HasDoor(Room r1, Room r2)
        => r1.Doors.ContainsKey(r2);

    public int GetDoorPerm(Room r1, Room r2)
        => r1.Doors.TryGetValue(r2, out int p) ? p : 0;

    // ── Queries ─────────────────────────────────────────────────────────────

    /// <summary>Find which metaroom contains the given world coordinate.</summary>
    public MetaRoom? MetaRoomAt(float x, float y)
    {
        for (int i = 0; i < _metaRooms.Count; i++)
        {
            var mr = _metaRooms[i];
            if (x >= mr.X && x <= mr.X + mr.Width &&
                y >= mr.Y && y <= mr.Y + mr.Height)
                return mr;
        }
        return null;
    }

    /// <summary>Find the specific room at a world coordinate.</summary>
    public Room? RoomAt(float x, float y)
    {
        for (int i = 0; i < _allRooms.Count; i++)
            if (_allRooms[i].ContainsPoint(x, y))
                return _allRooms[i];
        return null;
    }

    // ── Tick ────────────────────────────────────────────────────────────────

    /// <summary>Run one CA diffusion step on all metarooms.</summary>
    public void Tick()
    {
        for (int i = 0; i < _metaRooms.Count; i++)
            _metaRooms[i].TickCA();
    }

    // ── Reset ───────────────────────────────────────────────────────────────

    public void Reset()
    {
        _metaRooms.Clear();
        _allRooms.Clear();
        _nextMetaRoomId = 0;
        _nextRoomId     = 0;
    }
}
