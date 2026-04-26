using System.Linq;
using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.World;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class AgentInteractionTests
{
    [Fact]
    public void AffordanceCatalog_ContainsCoreCreatureInteractions()
    {
        Assert.Equal(11, AgentAffordanceCatalog.All.Count);

        AgentAffordance eat = AgentAffordanceCatalog.ForKind(AgentAffordanceKind.Eat);
        Assert.Equal("eat", eat.Token);
        Assert.Equal(VerbId.Eat, eat.VerbId);
        Assert.Equal(ScriptEvent.DoneToEat, eat.TargetScriptEventId);
        Assert.Equal(StimulusId.AteFoodSuccess, eat.StimulusId);

        Assert.Contains(AgentAffordanceCatalog.All, a => a.Kind == AgentAffordanceKind.Push);
        Assert.Contains(AgentAffordanceCatalog.All, a => a.Kind == AgentAffordanceKind.Pull);
        Assert.Contains(AgentAffordanceCatalog.All, a => a.Kind == AgentAffordanceKind.Hit);
        Assert.Contains(AgentAffordanceCatalog.All, a => a.Kind == AgentAffordanceKind.Activate);
        Assert.Contains(AgentAffordanceCatalog.All, a => a.Kind == AgentAffordanceKind.Pickup);
        Assert.Contains(AgentAffordanceCatalog.All, a => a.Kind == AgentAffordanceKind.Drop);
        Assert.Contains(AgentAffordanceCatalog.All, a => a.Kind == AgentAffordanceKind.Approach);
        Assert.Contains(AgentAffordanceCatalog.All, a => a.Kind == AgentAffordanceKind.Retreat);
        Assert.Contains(AgentAffordanceCatalog.All, a => a.Kind == AgentAffordanceKind.Play);
        Assert.Contains(AgentAffordanceCatalog.All, a => a.Kind == AgentAffordanceKind.Mate);
    }

    [Fact]
    public void AffordanceCatalog_ForAgent_UsesCurrentFlagsAndArchetype()
    {
        var food = new Agent.Agent(AgentFamily.Simple, 2, 3)
        {
            Attr = AgentAttr.Carryable,
            Bhvr = AgentBehaviour.CanEat | AgentBehaviour.CanPickup,
        };

        AgentAffordanceKind[] kinds = AgentAffordanceCatalog
            .ForAgent(food, AgentCatalog.Food)
            .Select(a => a.Kind)
            .ToArray();

        Assert.Contains(AgentAffordanceKind.Eat, kinds);
        Assert.Contains(AgentAffordanceKind.Pickup, kinds);
        Assert.Contains(AgentAffordanceKind.Drop, kinds);
        Assert.Contains(AgentAffordanceKind.Approach, kinds);
        Assert.Contains(AgentAffordanceKind.Retreat, kinds);
        Assert.DoesNotContain(AgentAffordanceKind.Hit, kinds);
        Assert.DoesNotContain(AgentAffordanceKind.Play, kinds);
    }

    [Fact]
    public void InteractionEffects_CoverStimulusChemicalCaScriptAndDebugReasons()
    {
        InteractionEffect stimulus = InteractionEffect.Stimulus(StimulusId.PlayedWithToy);
        InteractionEffect chemical = InteractionEffect.ChemicalDelta(ChemID.Reward, 0.15f, "toy reward");
        InteractionEffect ca = InteractionEffect.CaEmission(CaIndex.Scent3, 0.4f, "toy smell");
        InteractionEffect script = InteractionEffect.ScriptEvent(ScriptEvent.DoneToPush);
        InteractionEffect debug = InteractionEffect.DebugReason("catalog preview");

        var result = InteractionResult.CreateHandled(new[] { stimulus, chemical, ca, script, debug }, scriptFired: true);

        Assert.True(result.Handled);
        Assert.True(result.ScriptFired);
        Assert.Equal(InteractionEffectKind.Stimulus, result.Effects[0].Kind);
        Assert.Equal(StimulusId.PlayedWithToy, result.Effects[0].StimulusId);
        Assert.Equal(ChemID.Reward, result.Effects[1].ChemicalId);
        Assert.Equal(CaIndex.Scent3, result.Effects[2].CaChannelIndex);
        Assert.Equal(ScriptEvent.DoneToPush, result.Effects[3].ScriptEventId);
        Assert.Equal("catalog preview", result.Effects[4].Reason);
    }

    [Fact]
    public void ScriptBridge_FiresExistingScriptoriumAndReturnsDeclaredEffects()
    {
        var manager = new AgentManager();
        var actor = new Agent.Agent(AgentFamily.Creature, 1, 1);
        var target = new Agent.Agent(AgentFamily.Simple, 2, 3);
        bool fired = false;

        manager.RegisterScript(target.Classifier, ScriptEvent.DoneToEat, (self, from) =>
        {
            fired = true;
            Assert.Same(target, self);
            Assert.Same(actor, from);
        });

        var context = new InteractionContext(
            actor,
            target,
            AgentAffordanceCatalog.ForKind(AgentAffordanceKind.Eat));

        InteractionResult result = manager.FireAffordance(context);

        Assert.True(fired);
        Assert.True(result.Handled);
        Assert.True(result.ScriptFired);
        Assert.Contains(result.Effects, e => e.Kind == InteractionEffectKind.ScriptEvent && e.ScriptEventId == ScriptEvent.DoneToEat);
        Assert.Contains(result.Effects, e => e.Kind == InteractionEffectKind.Stimulus && e.StimulusId == StimulusId.AteFoodSuccess);
    }
}
