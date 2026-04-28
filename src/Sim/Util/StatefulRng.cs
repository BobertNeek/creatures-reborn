namespace CreaturesReborn.Sim.Util;

public readonly record struct StatefulRngState(ulong State);

/// <summary>
/// Serializable deterministic RNG for save/load continuity.
/// </summary>
public sealed class StatefulRng : IRng
{
    private ulong _state;

    public StatefulRng(int seed)
        : this((ulong)(uint)seed + 0x9E3779B97F4A7C15UL)
    {
    }

    private StatefulRng(ulong state)
    {
        _state = state == 0 ? 0x9E3779B97F4A7C15UL : state;
    }

    public StatefulRngState CreateState() => new(_state);

    public static StatefulRng FromState(StatefulRngState state) => new(state.State);

    public int Rnd(int max)
    {
        if (max <= 0)
            return 0;
        return (int)(NextUInt64() % (uint)(max + 1));
    }

    public int Rnd(int min, int max)
    {
        if (max <= min)
            return min;
        return min + Rnd(max - min);
    }

    public float RndFloat()
        => (NextUInt64() >> 40) / (float)(1 << 24);

    private ulong NextUInt64()
    {
        ulong x = _state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _state = x;
        return x * 2685821657736338717UL;
    }
}
