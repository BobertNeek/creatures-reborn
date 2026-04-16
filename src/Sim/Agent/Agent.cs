using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Sim.Agent;

/// <summary>
/// Base class for all agents in the world. An agent is any interactive object:
/// gadgets, plants, food, machines, creatures, the pointer, etc.
///
/// Each agent has:
///   - A classifier (family, genus, species) for script lookup
///   - Position, velocity, physics parameters
///   - Attribute and behaviour flags
///   - 100 OVxx "object variables" (local state)
///   - A timer that fires Timer events
///   - CA emit settings (room-based smell emission)
///
/// This is the C# equivalent of openc2e's Agent class.
/// </summary>
public class Agent
{
    // ── Identity ────────────────────────────────────────────────────────────
    public AgentClassifier Classifier { get; private set; }
    public int UniqueId { get; internal set; }

    public void SetClassifier(int family, int genus, int species)
        => Classifier = new AgentClassifier(family, genus, species);

    // ── Spatial ─────────────────────────────────────────────────────────────
    public float X     { get; set; }
    public float Y     { get; set; }
    public float VelX  { get; set; }
    public float VelY  { get; set; }

    /// <summary>Gravity acceleration (default 5, matching DS's c3_creature_accg).</summary>
    public float AccG  { get; set; } = 5.0f;

    /// <summary>Aerodynamic drag (0 = brick, 100 = balloon). Affects VelY damping.</summary>
    public int   Aero  { get; set; } = 0;

    /// <summary>Friction coefficient for horizontal velocity damping.</summary>
    public int   Friction { get; set; } = 100;

    /// <summary>Permeability: agent can pass through doors with perm ≥ this.</summary>
    public int   Perm  { get; set; } = 100;

    /// <summary>Elasticity for bouncing off walls (0-100).</summary>
    public int   Elas  { get; set; } = 0;

    // ── Attributes ──────────────────────────────────────────────────────────
    public AgentAttr      Attr { get; set; } = AgentAttr.None;
    public AgentBehaviour Bhvr { get; set; } = AgentBehaviour.None;

    // ── Object variables (OV00-OV99) ────────────────────────────────────────
    private readonly float[] _ov = new float[100];
    public float GetOV(int i)          => (uint)i < 100 ? _ov[i] : 0;
    public void  SetOV(int i, float v) { if ((uint)i < 100) _ov[i] = v; }

    // ── Timer ───────────────────────────────────────────────────────────────
    public int TimerRate { get; set; }
    private int _ticksSinceLastTimer;

    // ── CA emission ─────────────────────────────────────────────────────────
    public int   EmitCaIndex  { get; set; } = -1;
    public float EmitCaAmount { get; set; } = 0;

    // ── Z-order ─────────────────────────────────────────────────────────────
    public int ZOrder { get; set; }

    // ── Carry ───────────────────────────────────────────────────────────────
    public Agent? Carrying  { get; set; }
    public Agent? CarriedBy { get; set; }
    public Agent? InVehicle { get; set; }

    // ── Room cache ──────────────────────────────────────────────────────────
    public Room? CurrentRoom { get; set; }

    // ── State ───────────────────────────────────────────────────────────────
    public bool Paused  { get; set; }
    public bool Visible { get; set; } = true;
    public bool Dying   { get; set; }

    // ── Script handling (C# action delegates instead of CAOS VM) ────────────
    /// <summary>
    /// Registered event handlers. Key = event ID, Value = action to execute.
    /// This replaces the CAOS VM script system with direct C# delegates.
    /// </summary>
    private readonly Dictionary<int, Action<Agent, Agent?>> _scripts = new();

    /// <summary>Register a handler for an event on this agent.</summary>
    public void OnEvent(int eventId, Action<Agent, Agent?> handler)
        => _scripts[eventId] = handler;

    /// <summary>
    /// Fire an event on this agent. If a handler is registered, it runs immediately.
    /// Returns true if a handler was found.
    /// </summary>
    public bool FireEvent(int eventId, Agent? from = null)
    {
        if (_scripts.TryGetValue(eventId, out var handler))
        {
            handler(this, from);
            return true;
        }
        return false;
    }

    // ── Construction ────────────────────────────────────────────────────────

    public Agent(int family, int genus, int species)
    {
        Classifier = new AgentClassifier(family, genus, species);
    }

    // ── Tick ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called every world tick. Handles physics, timer, and CA emission.
    /// Override in subclasses for custom behaviour.
    /// </summary>
    public virtual void Tick(GameMap map)
    {
        if (Dying || Paused) return;

        // Physics
        if (Attr.HasFlag(AgentAttr.SufferPhysics))
            PhysicsTick(map);

        // Timer
        if (TimerRate > 0)
        {
            _ticksSinceLastTimer++;
            if (_ticksSinceLastTimer >= TimerRate)
            {
                _ticksSinceLastTimer = 0;
                FireEvent(ScriptEvent.Timer);
            }
        }

        // CA emission
        if (EmitCaIndex >= 0 && CurrentRoom != null)
        {
            CurrentRoom.CA[EmitCaIndex] = Math.Clamp(
                CurrentRoom.CA[EmitCaIndex] + EmitCaAmount, 0f, 1f);
        }
    }

    protected virtual void PhysicsTick(GameMap map)
    {
        // Gravity
        if (AccG > 0)
            VelY += AccG * 0.05f;  // dt = 1/20

        // Aero drag on vertical
        if (Aero > 0)
            VelY *= 1.0f - (Aero / 100.0f) * 0.1f;

        // Friction on horizontal
        if (Friction > 0)
            VelX *= 1.0f - (Friction / 100.0f) * 0.05f;

        // Move
        X += VelX * 0.05f;
        Y += VelY * 0.05f;

        // Floor collision — find the room and clamp to floor
        CurrentRoom = map.RoomAt(X, Y);
        if (CurrentRoom != null)
        {
            float floor = CurrentRoom.FloorYAtX(X);
            if (Y >= floor)
            {
                Y    = floor;
                VelY = -VelY * (Elas / 100.0f);
                if (MathF.Abs(VelY) < 0.5f) VelY = 0;
            }
        }
    }

    // ── Movement helpers ────────────────────────────────────────────────────

    public void MoveTo(float x, float y)
    {
        X = x;
        Y = y;
    }

    // ── Kill ────────────────────────────────────────────────────────────────

    public virtual void Kill()
    {
        Dying = true;
        if (CarriedBy != null)
        {
            CarriedBy.Carrying = null;
            CarriedBy = null;
        }
        if (Carrying != null)
        {
            Carrying.CarriedBy = null;
            Carrying = null;
        }
    }
}
