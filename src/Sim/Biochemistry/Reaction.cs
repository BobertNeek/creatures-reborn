namespace CreaturesReborn.Sim.Biochemistry;

/// <summary>
/// One chemical reaction: <c>propR1*R1 + propR2*R2 —rate→ propP1*P1 + propP2*P2</c>.
/// Direct port of c2e's <c>Reaction</c> struct (Reaction.h).
/// </summary>
/// <remarks>
/// <c>Rate</c> is a <see cref="FloatLocus"/> because receptors may bind directly to a reaction's
/// rate locus (via <c>ORGAN_REACTION</c> gene) to enzymatically modulate it.
/// The stored Rate is in the range [0, 1] where 0 = fast and 1 = slow
/// (inverted from genome loading: <c>Rate = 1 - GetFloat()</c>).
/// </remarks>
public sealed class Reaction
{
    public float propR1; public int R1;   // reactant 1 proportion + chemical ID
    public float propR2; public int R2;   // reactant 2 proportion + chemical ID

    /// <summary>
    /// Reaction rate locus.  Receptors targeting <see cref="OrganID.ORGAN_REACTION"/> bind here.
    /// 0 = instant (every tick), 1 = never (stored as "half-life-like" decay fraction computed at runtime).
    /// </summary>
    public FloatLocus Rate = new();

    public float propP1; public int P1;   // product 1 proportion + chemical ID
    public float propP2; public int P2;   // product 2 proportion + chemical ID
}
