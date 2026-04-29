using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class ChemicalReinforcementBusTests
{
    [Fact]
    public void Evaluate_HungerDecreaseAndAtpIncrease_CreatePositiveSurvivalSignals()
    {
        ChemicalDeltaWindow window = Window(
            (ChemID.HungerForCarb, before: 0.8f, after: 0.2f),
            (ChemID.ATP, before: 0.2f, after: 0.6f));

        ChemicalReinforcementTrace trace = ChemicalReinforcementBus.Evaluate(window, ChemicalReinforcementProfile.Default);

        Assert.Contains(trace.Signals, signal =>
            signal.ChemicalId == ChemID.HungerForCarb &&
            signal.Domain == ChemicalReinforcementDomain.Hunger &&
            signal.Valence == ChemicalReinforcementValence.Positive &&
            signal.Strength > 0.0f);
        Assert.Contains(trace.Signals, signal =>
            signal.ChemicalId == ChemID.ATP &&
            signal.Domain == ChemicalReinforcementDomain.Energy &&
            signal.Valence == ChemicalReinforcementValence.Positive &&
            signal.Strength > 0.0f);
    }

    [Fact]
    public void Evaluate_PainAndPunishmentIncrease_CreateNegativeSignals()
    {
        ChemicalDeltaWindow window = Window(
            (ChemID.Pain, before: 0.1f, after: 0.5f),
            (ChemID.Punishment, before: 0.0f, after: 0.7f));

        ChemicalReinforcementTrace trace = ChemicalReinforcementBus.Evaluate(window, ChemicalReinforcementProfile.Default);

        Assert.Contains(trace.Signals, signal =>
            signal.ChemicalId == ChemID.Pain &&
            signal.Domain == ChemicalReinforcementDomain.Pain &&
            signal.Valence == ChemicalReinforcementValence.Negative);
        Assert.Contains(trace.Signals, signal =>
            signal.ChemicalId == ChemID.Punishment &&
            signal.Domain == ChemicalReinforcementDomain.Learning &&
            signal.Valence == ChemicalReinforcementValence.Negative);
    }

    [Fact]
    public void Evaluate_ProfileWeightsScaleSignalsDeterministically()
    {
        ChemicalDeltaWindow window = Window((ChemID.Reward, before: 0.0f, after: 0.5f));
        ChemicalReinforcementProfile profile = ChemicalReinforcementProfile.Default
            .WithDomainWeight(ChemicalReinforcementDomain.Learning, 0.25f);

        ChemicalReinforcementTrace first = ChemicalReinforcementBus.Evaluate(window, profile);
        ChemicalReinforcementTrace second = ChemicalReinforcementBus.Evaluate(window, profile);

        ChemicalReinforcementSignal signal = Assert.Single(first.Signals);
        Assert.Equal(0.125f, signal.Strength, precision: 6);
        Assert.Equal(first.Signals.Select(s => s.Strength), second.Signals.Select(s => s.Strength));
    }

    [Fact]
    public void EvaluateTrace_UsesBiochemistryTraceDeltas()
    {
        var trace = new BiochemistryTrace();
        trace.Record(ChemID.Loneliness, before: 0.9f, amount: -0.4f, after: 0.5f, ChemicalDeltaSource.Stimulus, "social");

        ChemicalReinforcementTrace reinforcement = ChemicalReinforcementBus.Evaluate(trace, ChemicalReinforcementProfile.Default);

        ChemicalReinforcementSignal signal = Assert.Single(reinforcement.Signals);
        Assert.Equal(ChemicalReinforcementDomain.Social, signal.Domain);
        Assert.Equal(ChemicalReinforcementValence.Positive, signal.Valence);
    }

    private static ChemicalDeltaWindow Window(params (int Chemical, float before, float after)[] values)
    {
        float[] before = new float[BiochemConst.NUMCHEM];
        float[] after = new float[BiochemConst.NUMCHEM];
        foreach ((int chemical, float beforeValue, float afterValue) in values)
        {
            before[chemical] = beforeValue;
            after[chemical] = afterValue;
        }

        return ChemicalDeltaWindow.FromSnapshots(before, after);
    }
}
