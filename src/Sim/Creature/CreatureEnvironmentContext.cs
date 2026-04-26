using System;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Sim.Creature;

public readonly record struct CreatureEnvironmentContext(
    float Temperature,
    float Light,
    float Radiation)
{
    public static CreatureEnvironmentContext FromRoom(Room room)
        => new(
            Clamp01(room.CA[CaIndex.Temperature]),
            Clamp01(room.CA[CaIndex.Light]),
            Clamp01(room.CA[CaIndex.Radiation]));

    private static float Clamp01(float value) => Math.Clamp(value, 0.0f, 1.0f);
}

public readonly record struct CreatureEnvironmentResponse(
    float Temperature,
    float Light,
    float Radiation,
    float Hotness,
    float Coldness,
    float ComfortNeed,
    float Stress);

public static class CreatureEnvironmentEffects
{
    private const float ColdThreshold = 0.35f;
    private const float HotThreshold = 0.65f;
    private const float RadiationStressThreshold = 0.10f;

    public static CreatureEnvironmentResponse Evaluate(CreatureEnvironmentContext context)
    {
        float temperature = Math.Clamp(context.Temperature, 0.0f, 1.0f);
        float light = Math.Clamp(context.Light, 0.0f, 1.0f);
        float radiation = Math.Clamp(context.Radiation, 0.0f, 1.0f);

        float hotness = temperature > HotThreshold
            ? Math.Clamp((temperature - HotThreshold) / (1.0f - HotThreshold), 0.0f, 1.0f)
            : 0.0f;
        float coldness = temperature < ColdThreshold
            ? Math.Clamp((ColdThreshold - temperature) / ColdThreshold, 0.0f, 1.0f)
            : 0.0f;
        float comfortNeed = Math.Max(hotness, coldness);
        float stress = radiation > RadiationStressThreshold
            ? Math.Clamp((radiation - RadiationStressThreshold) / (1.0f - RadiationStressThreshold), 0.0f, 1.0f)
            : 0.0f;

        return new(
            temperature,
            light,
            radiation,
            hotness,
            coldness,
            comfortNeed,
            stress);
    }
}
