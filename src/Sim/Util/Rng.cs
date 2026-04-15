using System;

namespace CreaturesReborn.Sim.Util;

/// <summary>
/// Default <see cref="IRng"/> backed by <see cref="System.Random"/>.
/// </summary>
public sealed class Rng : IRng
{
    private readonly Random _rand;

    public Rng(int seed) => _rand = new Random(seed);

    public Rng(Random rand) => _rand = rand;

    public int Rnd(int max) => _rand.Next(0, max + 1);

    public int Rnd(int min, int max) => _rand.Next(min, max + 1);

    public float RndFloat() => (float)_rand.NextDouble();
}
