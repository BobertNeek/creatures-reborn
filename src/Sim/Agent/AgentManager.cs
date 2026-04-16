using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Sim.Agent;

/// <summary>
/// Central registry for all agents in the world. Manages creation, lookup,
/// and tick dispatching. This is equivalent to openc2e's World agent list.
///
/// Script lookup uses the Scriptorium pattern: scripts are registered
/// by (classifier + event) and looked up when events fire.
/// </summary>
public sealed class AgentManager
{
    private readonly List<Agent>  _agents  = new();
    private readonly List<Agent>  _killQueue = new();
    private int _nextUniqueId = 1;

    // ── Scriptorium: global event handlers by (classifier + event) ──────────
    // These are "species scripts" — default handlers for any agent matching
    // the classifier. Agent-specific handlers (via agent.OnEvent) take priority.
    private readonly Dictionary<(AgentClassifier, int), Action<Agent, Agent?>> _scriptorium = new();

    public IReadOnlyList<Agent> Agents => _agents;

    // ── Agent lifecycle ─────────────────────────────────────────────────────

    /// <summary>
    /// Register an agent with the world. Assigns a unique ID.
    /// </summary>
    public void Add(Agent agent)
    {
        agent.UniqueId = _nextUniqueId++;
        _agents.Add(agent);
    }

    /// <summary>
    /// Queue an agent for removal at end of tick (safe during iteration).
    /// </summary>
    public void QueueKill(Agent agent)
    {
        agent.Kill();
        _killQueue.Add(agent);
    }

    // ── Scriptorium ─────────────────────────────────────────────────────────

    /// <summary>
    /// Register a global event handler for all agents with the given classifier.
    /// </summary>
    public void RegisterScript(AgentClassifier classifier, int eventId,
                                Action<Agent, Agent?> handler)
    {
        _scriptorium[(classifier, eventId)] = handler;
    }

    /// <summary>
    /// Fire an event on an agent. First checks the agent's own handlers,
    /// then falls back to the scriptorium.
    /// </summary>
    public bool FireScript(Agent agent, int eventId, Agent? from = null)
    {
        // Agent-local handler first
        if (agent.FireEvent(eventId, from))
            return true;

        // Scriptorium lookup (exact match, then wildcards)
        if (_scriptorium.TryGetValue((agent.Classifier, eventId), out var handler))
        {
            handler(agent, from);
            return true;
        }

        return false;
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Find all agents matching a classifier query (0 = wildcard).
    /// </summary>
    public List<Agent> FindAgents(int family, int genus, int species)
    {
        var result = new List<Agent>();
        for (int i = 0; i < _agents.Count; i++)
        {
            if (_agents[i].Classifier.Matches(family, genus, species) && !_agents[i].Dying)
                result.Add(_agents[i]);
        }
        return result;
    }

    /// <summary>Find the nearest agent matching a classifier within range of a point.</summary>
    public Agent? FindNearest(float x, float y, float range, int family, int genus, int species)
    {
        Agent? best     = null;
        float  bestDist = range * range;

        for (int i = 0; i < _agents.Count; i++)
        {
            var a = _agents[i];
            if (a.Dying || !a.Classifier.Matches(family, genus, species)) continue;
            float dx = a.X - x;
            float dy = a.Y - y;
            float d2 = dx * dx + dy * dy;
            if (d2 < bestDist)
            {
                best     = a;
                bestDist = d2;
            }
        }
        return best;
    }

    /// <summary>Count non-dying agents matching a classifier.</summary>
    public int CountAgents(int family, int genus, int species)
    {
        int count = 0;
        for (int i = 0; i < _agents.Count; i++)
            if (!_agents[i].Dying && _agents[i].Classifier.Matches(family, genus, species))
                count++;
        return count;
    }

    // ── Tick ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tick all agents and flush the kill queue.
    /// </summary>
    public void Tick(GameMap map)
    {
        for (int i = 0; i < _agents.Count; i++)
            _agents[i].Tick(map);

        // Flush kills
        if (_killQueue.Count > 0)
        {
            for (int i = 0; i < _killQueue.Count; i++)
                _agents.Remove(_killQueue[i]);
            _killQueue.Clear();
        }
    }
}
