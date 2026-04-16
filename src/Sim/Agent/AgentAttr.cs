using System;

namespace CreaturesReborn.Sim.Agent;

/// <summary>
/// Agent attribute flags, matching the c2e <c>attr</c> bitfield.
/// These control physics, input handling, visibility, and creature interaction.
/// </summary>
[Flags]
public enum AgentAttr : uint
{
    None             = 0,

    /// <summary>Can be picked up by creatures or the hand.</summary>
    Carryable        = 1 << 0,

    /// <summary>Responds to mouse clicks.</summary>
    Mouseable        = 1 << 1,

    /// <summary>Can be activated (push/pull/hit) by creatures.</summary>
    Activateable     = 1 << 2,

    /// <summary>Greedy cabin — contained agents don't fall out.</summary>
    GreedyCabin      = 1 << 3,

    /// <summary>Invisible — not rendered.</summary>
    Invisible        = 1 << 4,

    /// <summary>Floatable — attached to another agent's movement.</summary>
    Floatable        = 1 << 5,

    /// <summary>Suffers collisions with room boundaries.</summary>
    SufferCollisions = 1 << 6,

    /// <summary>Suffers physics (gravity, velocity, aero).</summary>
    SufferPhysics    = 1 << 7,

    /// <summary>Camera-shy — doesn't trigger camera tracking.</summary>
    CameraShy        = 1 << 8,

    /// <summary>Open-air cabin — agents can fall in/out of vehicles.</summary>
    OpenAirCabin     = 1 << 9,

    /// <summary>Can be rotated (spin physics).</summary>
    Rotatable        = 1 << 10,

    /// <summary>Has CA smell presence in rooms.</summary>
    Presence         = 1 << 11,
}

/// <summary>
/// Creature-interaction behaviour flags, matching c2e <c>bhvr</c>.
/// </summary>
[Flags]
public enum AgentBehaviour : byte
{
    None       = 0,
    CanPush    = 1 << 0,
    CanPull    = 1 << 1,
    CanStop    = 1 << 2,
    CanHit     = 1 << 3,
    CanEat     = 1 << 4,
    CanPickup  = 1 << 5,
}
