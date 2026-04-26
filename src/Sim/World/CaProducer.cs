using System.Collections.Generic;
using CreaturesReborn.Sim.Agent;
using SimAgent = CreaturesReborn.Sim.Agent.Agent;

namespace CreaturesReborn.Sim.World;

public enum CaEmissionKind
{
    AgentEmit,
    RoomAlteration,
    Script,
    Weather,
    Debug,
}

public sealed record CaEmission(
    CaChannelDefinition Channel,
    float Amount,
    CaEmissionKind Kind,
    string Source);

public sealed record CaProducer(
    int AgentId,
    AgentClassifier Classifier,
    int RoomId,
    IReadOnlyList<CaEmission> Emissions);

public static class AgentCaProducerExtensions
{
    public static CaProducer? CreateCaProducer(this SimAgent agent)
    {
        if (agent.CurrentRoom == null)
            return null;

        if ((uint)agent.EmitCaIndex >= CaIndex.Count)
            return null;

        var emission = new CaEmission(
            CaChannelCatalog.Get(agent.EmitCaIndex),
            agent.EmitCaAmount,
            CaEmissionKind.AgentEmit,
            $"agent:{agent.UniqueId}:{agent.Classifier}");

        return new CaProducer(
            agent.UniqueId,
            agent.Classifier,
            agent.CurrentRoom.Id,
            new[] { emission });
    }
}
