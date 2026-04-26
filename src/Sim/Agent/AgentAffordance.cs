using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Sim.Agent;

public enum AgentAffordanceKind
{
    Eat,
    Push,
    Pull,
    Hit,
    Activate,
    Pickup,
    Drop,
    Approach,
    Retreat,
    Play,
    Mate,
}

public sealed record AgentAffordance(
    AgentAffordanceKind Kind,
    string Token,
    int VerbId,
    int? TargetScriptEventId,
    int? StimulusId,
    AgentBehaviour RequiredBehaviour = AgentBehaviour.None,
    AgentAttr RequiredAttr = AgentAttr.None,
    int? RequiredObjectCategory = null);

public static class AgentAffordanceCatalog
{
    private static readonly AgentAffordance[] Definitions =
    {
        new(AgentAffordanceKind.Eat, "eat", VerbId.Eat, ScriptEvent.DoneToEat, StimulusId.AteFoodSuccess, AgentBehaviour.CanEat),
        new(AgentAffordanceKind.Push, "push", VerbId.Activate1, ScriptEvent.DoneToPush, StimulusId.PushedAgent, AgentBehaviour.CanPush),
        new(AgentAffordanceKind.Pull, "pull", VerbId.Activate2, ScriptEvent.DoneToPull, StimulusId.PulledAgent, AgentBehaviour.CanPull),
        new(AgentAffordanceKind.Hit, "hit", VerbId.Hit, ScriptEvent.DoneToHit, StimulusId.Activate1Bad, AgentBehaviour.CanHit),
        new(AgentAffordanceKind.Activate, "activate", VerbId.Activate1, ScriptEvent.Activate1, StimulusId.Activate1Good, RequiredAttr: AgentAttr.Activateable),
        new(AgentAffordanceKind.Pickup, "pickup", VerbId.Get, ScriptEvent.Pickup, StimulusId.GotFood),
        new(AgentAffordanceKind.Drop, "drop", VerbId.Drop, ScriptEvent.Drop, null),
        new(AgentAffordanceKind.Approach, "approach", VerbId.Approach, null, StimulusId.ApproachSuccess),
        new(AgentAffordanceKind.Retreat, "retreat", VerbId.Retreat, null, StimulusId.RetreatSuccess),
        new(AgentAffordanceKind.Play, "play", VerbId.Activate1, ScriptEvent.DoneToPush, StimulusId.PlayedWithToy, RequiredObjectCategory: ObjectCategory.Toy),
        new(AgentAffordanceKind.Mate, "mate", VerbId.Approach, null, StimulusId.ItMated),
    };

    public static IReadOnlyList<AgentAffordance> All => Definitions;

    public static AgentAffordance ForKind(AgentAffordanceKind kind)
    {
        for (int i = 0; i < Definitions.Length; i++)
        {
            if (Definitions[i].Kind == kind)
                return Definitions[i];
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown agent affordance kind.");
    }

    public static IReadOnlyList<AgentAffordance> ForAgent(Agent agent, AgentArchetype? archetype = null)
    {
        var affordances = new List<AgentAffordance>(Definitions.Length);
        for (int i = 0; i < Definitions.Length; i++)
        {
            AgentAffordance affordance = Definitions[i];
            if (IsAvailable(agent, affordance, archetype))
                affordances.Add(affordance);
        }

        return affordances;
    }

    public static bool IsAvailable(Agent agent, AgentAffordance affordance, AgentArchetype? archetype = null)
    {
        if (agent.Dying || !agent.Visible)
            return false;

        return affordance.Kind switch
        {
            AgentAffordanceKind.Eat => agent.Bhvr.HasFlag(AgentBehaviour.CanEat) || archetype?.IsEdible == true,
            AgentAffordanceKind.Pickup => agent.Bhvr.HasFlag(AgentBehaviour.CanPickup) || agent.Attr.HasFlag(AgentAttr.Carryable),
            AgentAffordanceKind.Drop => agent.Bhvr.HasFlag(AgentBehaviour.CanPickup) || agent.Attr.HasFlag(AgentAttr.Carryable),
            AgentAffordanceKind.Approach => true,
            AgentAffordanceKind.Retreat => true,
            AgentAffordanceKind.Play => MatchesCategory(archetype, ObjectCategory.Toy),
            AgentAffordanceKind.Mate => IsCreatureCategory(archetype),
            _ => MeetsRequiredFlags(agent, affordance) && MatchesCategory(archetype, affordance.RequiredObjectCategory),
        };
    }

    private static bool MeetsRequiredFlags(Agent agent, AgentAffordance affordance)
    {
        if (affordance.RequiredBehaviour != AgentBehaviour.None &&
            !agent.Bhvr.HasFlag(affordance.RequiredBehaviour))
            return false;

        if (affordance.RequiredAttr != AgentAttr.None &&
            !agent.Attr.HasFlag(affordance.RequiredAttr))
            return false;

        return true;
    }

    private static bool MatchesCategory(AgentArchetype? archetype, int? requiredCategory)
        => requiredCategory == null || archetype?.ObjectCategory == requiredCategory.Value;

    private static bool IsCreatureCategory(AgentArchetype? archetype)
        => archetype?.ObjectCategory is ObjectCategory.Norn or ObjectCategory.Grendel or ObjectCategory.Ettin or ObjectCategory.Geat;
}
