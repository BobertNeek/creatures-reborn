using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.World;

/// <summary>
/// A single room within a <see cref="MetaRoom"/>. Rooms are defined by a
/// trapezoidal boundary (left edge, right edge, sloped floor and ceiling)
/// matching the c2e room model used in the CAOS <c>addr</c> command.
///
/// Rooms carry Cellular Automata values (heat, light, nutrients, etc.)
/// that diffuse to connected neighbours each tick.
/// </summary>
public sealed class Room
{
    // ── Identity ────────────────────────────────────────────────────────────
    public int    Id         { get; set; }
    public int    MetaRoomId { get; set; }
    public string Music      { get; set; } = "";

    // ── Geometry (world-pixel coordinates, matching c2e convention) ─────────
    // The room is a trapezoid: left wall from (XLeft, YLeftCeiling) to (XLeft, YLeftFloor),
    //                           right wall from (XRight, YRightCeiling) to (XRight, YRightFloor).
    public float XLeft         { get; set; }
    public float XRight        { get; set; }
    public float YLeftCeiling  { get; set; }
    public float YRightCeiling { get; set; }
    public float YLeftFloor    { get; set; }
    public float YRightFloor   { get; set; }

    // ── Room type ───────────────────────────────────────────────────────────
    public RoomType Type { get; set; } = RoomType.Outdoor;

    // ── Cellular Automata ───────────────────────────────────────────────────
    public readonly float[] CA     = new float[CaIndex.Count];
    public readonly float[] CATemp = new float[CaIndex.Count];   // swap buffer

    // ── Doors (connections to other rooms) ──────────────────────────────────
    // Key = connected Room, Value = permeability (0 = impassable, 100 = fully open)
    internal readonly Dictionary<Room, int> Doors = new();

    // ── Derived helpers ─────────────────────────────────────────────────────

    public float Width  => XRight - XLeft;
    public float CenterX => (XLeft + XRight) * 0.5f;
    public float CenterY => (YLeftFloor + YRightFloor + YLeftCeiling + YRightCeiling) * 0.25f;

    /// <summary>
    /// Interpolate the floor Y at a given X within this room.
    /// </summary>
    public float FloorYAtX(float x)
    {
        if (Width < 0.001f) return YLeftFloor;
        float t = Math.Clamp((x - XLeft) / Width, 0f, 1f);
        return YLeftFloor + t * (YRightFloor - YLeftFloor);
    }

    /// <summary>
    /// Interpolate the ceiling Y at a given X within this room.
    /// </summary>
    public float CeilingYAtX(float x)
    {
        if (Width < 0.001f) return YLeftCeiling;
        float t = Math.Clamp((x - XLeft) / Width, 0f, 1f);
        return YLeftCeiling + t * (YRightCeiling - YLeftCeiling);
    }

    /// <summary>
    /// Test whether a point (x,y) is inside this room's trapezoidal boundary.
    /// </summary>
    public bool ContainsPoint(float x, float y)
    {
        if (x < XLeft || x > XRight) return false;
        float ceiling = CeilingYAtX(x);
        float floor   = FloorYAtX(x);
        return y >= ceiling && y <= floor;
    }

    // ── CA tick ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Diffusion rate for this room's type. Metal rooms insulate well;
    /// outdoor rooms diffuse freely.
    /// </summary>
    private float DiffusionRate => Type switch
    {
        RoomType.IndoorMetal    => 0.01f,
        RoomType.IndoorConcrete => 0.04f,
        RoomType.IndoorWood     => 0.08f,
        RoomType.Underwater     => 0.15f,
        _                       => 0.10f,
    };

    /// <summary>
    /// Pre-tick: accumulate source channels into their output channels and
    /// prepare diffusion.
    /// </summary>
    public void PreTick()
    {
        // Sources feed their paired output
        // (HeatSource → Temperature, LightSource → Light, etc.)
        for (int i = 0; i < CaIndex.Count; i += 2)
        {
            if (i + 1 < CaIndex.Count)
                CA[i] = Math.Clamp(CA[i] + CA[i + 1] * 0.05f, 0f, 1f);
        }

        // Copy current values into temp for diffusion averaging
        Array.Copy(CA, CATemp, CaIndex.Count);
    }

    /// <summary>
    /// Post-tick: blend our CA values with neighbours (diffusion).
    /// Call after all rooms have done PreTick.
    /// </summary>
    public void PostTick()
    {
        float rate = DiffusionRate;

        foreach (var (neighbour, perm) in Doors)
        {
            if (perm <= 0) continue;
            float permFactor = perm / 100.0f;
            float blend = rate * permFactor;

            for (int i = 0; i < CaIndex.Count; i++)
            {
                float diff = neighbour.CATemp[i] - CATemp[i];
                CA[i] = Math.Clamp(CA[i] + diff * blend, 0f, 1f);
            }
        }

        // Natural decay (everything tends toward 0 slowly)
        for (int i = 0; i < CaIndex.Count; i++)
        {
            // Don't decay sources
            if ((i & 1) == 1) continue;
            CA[i] *= 0.998f;
        }
    }

    public IReadOnlyDictionary<Room, int> GetDoors() => Doors;
}
