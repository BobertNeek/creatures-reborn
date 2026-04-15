namespace CreaturesReborn.Sim.Util;

/// <summary>
/// Deterministic random-number source injected into every Sim object that needs randomness.
/// Mirrors the <c>Rnd</c> and <c>RndFloat</c> helpers used throughout the c2e C++ source.
/// </summary>
/// <remarks>
/// Never use <see cref="System.Random.Shared"/>, <c>Godot.GD.Randf</c>, or DateTime-based entropy
/// inside <c>Sim/</c>. All randomness flows through this interface so that every tick is
/// reproducible from a seed — this is what makes the headless validation harness and regression
/// tests possible.
/// </remarks>
public interface IRng
{
    /// <summary>
    /// Returns a uniform integer in the inclusive range <c>[0, max]</c>.
    /// Mirrors c2e's single-arg <c>Rnd(max)</c>.
    /// </summary>
    int Rnd(int max);

    /// <summary>
    /// Returns a uniform integer in the inclusive range <c>[min, max]</c>.
    /// Mirrors c2e's two-arg <c>Rnd(min, max)</c>.
    /// </summary>
    int Rnd(int min, int max);

    /// <summary>
    /// Returns a uniform float in <c>[0.0, 1.0)</c>. Mirrors c2e's <c>RndFloat()</c>.
    /// </summary>
    float RndFloat();
}
