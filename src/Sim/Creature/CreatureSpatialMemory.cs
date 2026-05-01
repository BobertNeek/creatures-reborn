using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.Creature;

public sealed record CreatureObjectMemory(
    int ObjectCategory,
    string Noun,
    float X,
    float Y,
    int RoomId,
    int LastSeenTick,
    int ObservationCount,
    float Confidence);

public sealed class CreatureSpatialMemory
{
    private readonly Dictionary<int, CreatureObjectMemory> _byCategory = new();

    public IReadOnlyCollection<CreatureObjectMemory> AllMemories => _byCategory.Values;

    public CreatureObjectMemory? FindByCategory(int objectCategory)
        => _byCategory.TryGetValue(objectCategory, out CreatureObjectMemory? memory)
            ? memory
            : null;

    public CreatureObjectMemory Observe(
        int objectCategory,
        string noun,
        float x,
        float y,
        int roomId,
        int tick,
        float reinforcement = 0.35f)
    {
        noun = string.IsNullOrWhiteSpace(noun) ? "object" : noun.Trim().ToLowerInvariant();
        reinforcement = Math.Clamp(reinforcement, 0.0f, 1.0f);

        if (!_byCategory.TryGetValue(objectCategory, out CreatureObjectMemory? existing))
        {
            var created = new CreatureObjectMemory(
                objectCategory,
                noun,
                x,
                y,
                roomId,
                tick,
                ObservationCount: 1,
                Confidence: Math.Max(0.35f, reinforcement));
            _byCategory[objectCategory] = created;
            return created;
        }

        int count = existing.ObservationCount + 1;
        float blend = 1.0f / count;
        var updated = existing with
        {
            Noun = noun,
            X = existing.X + (x - existing.X) * blend,
            Y = existing.Y + (y - existing.Y) * blend,
            RoomId = roomId,
            LastSeenTick = tick,
            ObservationCount = count,
            Confidence = Math.Clamp(existing.Confidence + reinforcement * 0.5f, 0.0f, 1.0f),
        };
        _byCategory[objectCategory] = updated;
        return updated;
    }
}
