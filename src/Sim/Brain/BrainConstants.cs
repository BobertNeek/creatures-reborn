namespace CreaturesReborn.Sim.Brain;

/// <summary>
/// Brain-subsystem capacity constants and SVRule variable index enums.
/// Direct port of c2e <c>BrainConstants.h</c>.
/// </summary>
public static class BrainConst
{
    public const int NumSVRuleVariables          = 8;   // state vector size per neuron / dendrite
    public const int NoOfVariablesAvailableAsLoci = 4;   // how many neuron state vars are biochem-addressable (BiochemistryConstants.h:89)
    public const int SVRuleLength            = 16;  // opcodes per SVRule
    public const int FloatDivisor            = 248; // divisor used to convert arrayIndex → float value
    public const int MaxLobes                = 255;
    public const int MaxTracts               = 255;
    public const int MaxDendritesPerTract    = 255 * 255;
    public const int MaxNeuronsPerLobe       = 255 * 255;
    public const int MaxInstincts            = 255;
}

/// <summary>
/// Indices into a neuron's 8-variable state vector (<c>SVRuleVariables</c> in c2e).
/// Mirrors <c>NeuronVariableNames</c> in SVRule.h.
/// </summary>
public static class NeuronVar
{
    public const int State  = 0;  // STATE_VAR
    public const int Input  = 1;  // INPUT_VAR
    public const int Output = 2;  // OUTPUT_VAR
    public const int Third  = 3;  // THIRD_VAR
    public const int Fourth = 4;  // FOURTH_VAR
    public const int Fifth  = 5;  // FIFTH_VAR
    public const int Sixth  = 6;  // SIXTH_VAR
    public const int NGF    = 7;  // NGF_VAR — Neural Growth Factor (drives dendrite migration)
}

/// <summary>
/// Indices into a dendrite's 8-variable weight vector.
/// Mirrors <c>DendriteVariableNames</c> in SVRule.h.
/// </summary>
public static class DendriteVar
{
    public const int WeightST  = 0;  // WEIGHT_SHORTTERM_VAR
    public const int WeightLT  = 1;  // WEIGHT_LONGTERM_VAR
    public const int Second    = 2;  // SECOND_DENDRITE_VAR
    public const int Third     = 3;  // THIRD_DENDRITE_VAR
    public const int Fourth    = 4;  // FOURTH_DENDRITE_VAR
    public const int Fifth     = 5;  // FIFTH_DENDRITE_VAR
    public const int Sixth     = 6;  // SIXTH_DENDRITE_VAR
    public const int Strength  = 7;  // STRENGTH_VAR — resistance to migration
}
