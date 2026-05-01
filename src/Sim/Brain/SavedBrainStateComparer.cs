using System;
using CreaturesReborn.Sim.Save;

namespace CreaturesReborn.Sim.Brain;

internal static class SavedBrainStateComparer
{
    public static bool Equivalent(SavedBrainState expected, SavedBrainState actual)
    {
        if (expected.InstinctsRemaining != actual.InstinctsRemaining
            || expected.IsProcessingInstincts != actual.IsProcessingInstincts
            || expected.Lobes.Count != actual.Lobes.Count
            || expected.Tracts.Count != actual.Tracts.Count)
            return false;

        for (int i = 0; i < expected.Lobes.Count; i++)
        {
            SavedLobeState a = expected.Lobes[i];
            SavedLobeState b = actual.Lobes[i];
            if (a.Index != b.Index
                || a.Token != b.Token
                || a.WinningNeuronId != b.WinningNeuronId
                || a.Neurons.Count != b.Neurons.Count)
                return false;

            for (int n = 0; n < a.Neurons.Count; n++)
            {
                if (a.Neurons[n].Index != b.Neurons[n].Index
                    || !FloatArraysEquivalent(a.Neurons[n].States, b.Neurons[n].States))
                    return false;
            }
        }

        for (int i = 0; i < expected.Tracts.Count; i++)
        {
            SavedTractState a = expected.Tracts[i];
            SavedTractState b = actual.Tracts[i];
            if (a.Index != b.Index
                || !NearlyEqual(a.STtoLTRate, b.STtoLTRate)
                || a.Dendrites.Count != b.Dendrites.Count)
                return false;

            for (int d = 0; d < a.Dendrites.Count; d++)
            {
                if (a.Dendrites[d].Index != b.Dendrites[d].Index
                    || !FloatArraysEquivalent(a.Dendrites[d].Weights, b.Dendrites[d].Weights))
                    return false;
            }
        }

        return true;
    }

    public static bool FloatArraysEquivalent(float[] expected, float[] actual)
    {
        if (expected.Length != actual.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
            if (!NearlyEqual(expected[i], actual[i]))
                return false;

        return true;
    }

    private static bool NearlyEqual(float expected, float actual)
        => MathF.Abs(expected - actual) <= 0.00001f;
}
