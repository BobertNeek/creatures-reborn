using System.Collections.Generic;

namespace CreaturesReborn.Sim.Agent;

public enum InteractionEffectKind
{
    Stimulus,
    ChemicalDelta,
    CaEmission,
    ScriptEvent,
    DebugReason,
}

public sealed record InteractionEffect(
    InteractionEffectKind Kind,
    int? StimulusId = null,
    int? ChemicalId = null,
    float Amount = 0,
    int? CaChannelIndex = null,
    int? ScriptEventId = null,
    string Reason = "")
{
    public static InteractionEffect Stimulus(int stimulusId)
        => new(InteractionEffectKind.Stimulus, StimulusId: stimulusId);

    public static InteractionEffect ChemicalDelta(int chemicalId, float amount, string reason = "")
        => new(InteractionEffectKind.ChemicalDelta, ChemicalId: chemicalId, Amount: amount, Reason: reason);

    public static InteractionEffect CaEmission(int caChannelIndex, float amount, string reason = "")
        => new(InteractionEffectKind.CaEmission, CaChannelIndex: caChannelIndex, Amount: amount, Reason: reason);

    public static InteractionEffect ScriptEvent(int scriptEventId)
        => new(InteractionEffectKind.ScriptEvent, ScriptEventId: scriptEventId);

    public static InteractionEffect DebugReason(string reason)
        => new(InteractionEffectKind.DebugReason, Reason: reason);
}

public sealed record InteractionContext(
    Agent Actor,
    Agent Target,
    AgentAffordance Affordance);

public sealed record InteractionResult(
    bool Handled,
    bool ScriptFired,
    IReadOnlyList<InteractionEffect> Effects)
{
    public static InteractionResult CreateHandled(IEnumerable<InteractionEffect> effects, bool scriptFired = false)
        => new(true, scriptFired, new List<InteractionEffect>(effects));

    public static InteractionResult Ignored(string reason)
        => new(false, false, new[] { InteractionEffect.DebugReason(reason) });
}
