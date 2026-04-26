using System.Collections.Generic;

namespace CreaturesReborn.Sim.Agent;

public static class AgentInteractionBridge
{
    public static InteractionResult FireAffordance(this AgentManager manager, InteractionContext context)
    {
        var effects = new List<InteractionEffect>();

        bool scriptFired = false;
        if (context.Affordance.TargetScriptEventId is int scriptEventId)
        {
            effects.Add(InteractionEffect.ScriptEvent(scriptEventId));
            scriptFired = manager.FireScript(context.Target, scriptEventId, context.Actor);
        }

        if (context.Affordance.StimulusId is int stimulusId)
            effects.Add(InteractionEffect.Stimulus(stimulusId));

        if (effects.Count == 0)
            effects.Add(InteractionEffect.DebugReason($"affordance:{context.Affordance.Token}"));

        return InteractionResult.CreateHandled(effects, scriptFired);
    }
}
