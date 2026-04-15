using System;

namespace CreaturesReborn.Sim.World;

/// <summary>
/// Minimal sim-side metaroom: a rectangular walkable area with bounds.
/// The Godot-side <c>MetaroomNode</c> wraps this and adds visuals.
/// </summary>
public sealed class MetaroomSim
{
    public float LeftBound  { get; set; } = -8.0f;
    public float RightBound { get; set; } =  8.0f;
    public float FloorY     { get; set; } =  0.0f;

    /// <summary>Clamp an X position to the walkable area.</summary>
    public float ClampX(float x) => Math.Clamp(x, LeftBound, RightBound);
}
