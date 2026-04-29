using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class ChemicalLearningAdapterTests
{
    [Fact]
    public void ClassicOnly_IgnoresChemicalSignals()
    {
        ChemicalReinforcementTrace trace = ChemicalReinforcementBus.Evaluate(
            new ChemicalDeltaWindow([
                new ChemicalDelta(
                    ChemID.HungerForCarb,
                    ChemicalCatalog.Get(ChemID.HungerForCarb),
                    Before: 0.8f,
                    Amount: -0.4f,
                    After: 0.4f,
                    ChemicalDeltaSource.Metabolism,
                    "satiety")
            ]),
            ChemicalReinforcementProfile.Default);

        BrainReinforcementInput input = ChemicalLearningAdapter.ToBrainInput(trace, BrainLearningMode.ClassicOnly);

        Assert.Equal(BrainLearningMode.ClassicOnly, input.Mode);
        Assert.Empty(input.Signals);
        Assert.Equal(0.0f, input.PositiveStrength);
        Assert.Equal(0.0f, input.NegativeStrength);
    }

    [Fact]
    public void ChemicalBusClassic_ProjectsPositiveAndNegativeSignals()
    {
        ChemicalReinforcementTrace trace = ChemicalReinforcementBus.Evaluate(
            new ChemicalDeltaWindow([
                new ChemicalDelta(
                    ChemID.HungerForCarb,
                    ChemicalCatalog.Get(ChemID.HungerForCarb),
                    Before: 0.9f,
                    Amount: -0.3f,
                    After: 0.6f,
                    ChemicalDeltaSource.Metabolism,
                    "satiety"),
                new ChemicalDelta(
                    ChemID.Pain,
                    ChemicalCatalog.Get(ChemID.Pain),
                    Before: 0.1f,
                    Amount: 0.2f,
                    After: 0.3f,
                    ChemicalDeltaSource.OrganInjury,
                    "injury")
            ]),
            ChemicalReinforcementProfile.Default);

        BrainReinforcementInput input = ChemicalLearningAdapter.ToBrainInput(trace, BrainLearningMode.ChemicalBusClassic);

        Assert.Equal(BrainLearningMode.ChemicalBusClassic, input.Mode);
        Assert.Equal(2, input.Signals.Count);
        Assert.True(input.PositiveStrength > 0.0f);
        Assert.True(input.NegativeStrength > 0.0f);
        Assert.Contains(input.Signals, signal => signal.Domain == ChemicalReinforcementDomain.Hunger);
        Assert.Contains(input.Signals, signal => signal.Domain == ChemicalReinforcementDomain.Pain);
    }

    [Fact]
    public void LearningTrace_CanRecordChemicalSignalIdsWithoutChangingClassicReinforcement()
    {
        var learningTrace = new LearningTrace();
        ChemicalReinforcementTrace chemicalTrace = ChemicalReinforcementBus.Evaluate(
            new ChemicalDeltaWindow([
                new ChemicalDelta(
                    ChemID.Reward,
                    ChemicalCatalog.Get(ChemID.Reward),
                    Before: 0.0f,
                    Amount: 0.5f,
                    After: 0.5f,
                    ChemicalDeltaSource.Stimulus,
                    "reward")
            ]),
            ChemicalReinforcementProfile.Default);

        BrainReinforcementInput input = ChemicalLearningAdapter.ToBrainInput(chemicalTrace, BrainLearningMode.ChemicalBusClassic);
        learningTrace.RecordChemicalReinforcement(input);

        Assert.Single(learningTrace.ChemicalSignals);
        Assert.Equal(ChemID.Reward, learningTrace.ChemicalSignals[0].ChemicalId);
        Assert.Empty(learningTrace.Reinforcements);
    }
}
