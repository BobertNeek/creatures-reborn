using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Util;
using Xunit;
using B = CreaturesReborn.Sim.Brain.Brain;

namespace CreaturesReborn.Sim.Tests;

public sealed class BrainAcceleratorStateTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void Lobe_CreateAcceleratorState_CopiesNeuronInputsStatesAndRules()
    {
        B brain = LoadBrain();
        Lobe lobe = brain.GetLobe(0)!;
        lobe.SetNeuronInput(0, 0.25f);
        lobe.GetNeuron(0).States[NeuronVar.State] = 0.5f;

        LobeAcceleratorState state = lobe.CreateAcceleratorState();
        lobe.SetNeuronInput(0, 0.75f);
        lobe.GetNeuron(0).States[NeuronVar.State] = 0.9f;

        Assert.Equal(lobe.GetNoOfNeurons() * BrainConst.NumSVRuleVariables, state.NeuronStates.Length);
        Assert.Equal(lobe.GetNoOfNeurons(), state.NeuronInputs.Length);
        Assert.Equal(0.25f, state.NeuronInputs[0], precision: 6);
        Assert.Equal(0.5f, state.NeuronStates[NeuronVar.State], precision: 6);
        Assert.Equal(BrainConst.SVRuleLength, state.InitRule.Count);
        Assert.Equal(BrainConst.SVRuleLength, state.UpdateRule.Count);
    }

    [Fact]
    public void Lobe_ApplyAcceleratorResult_ReplacesNeuronStatesAndWinningNeuronThenClearsInputs()
    {
        B brain = LoadBrain();
        Lobe lobe = brain.GetLobe(0)!;
        lobe.SetNeuronInput(0, 0.4f);
        float[] states = lobe.CreateAcceleratorState().NeuronStates;
        states[BrainConst.NumSVRuleVariables + NeuronVar.State] = 0.77f;
        states[BrainConst.NumSVRuleVariables + NeuronVar.Output] = 0.66f;

        lobe.ApplyAcceleratorResult(states, winningNeuronId: 1);
        LobeAcceleratorState after = lobe.CreateAcceleratorState();

        Assert.Equal(1, lobe.GetWhichNeuronWon());
        Assert.Equal(0.77f, lobe.GetNeuron(1).States[NeuronVar.State], precision: 6);
        Assert.Equal(0.66f, lobe.GetSpareNeuronVariables()[NeuronVar.Output], precision: 6);
        Assert.All(after.NeuronInputs, input => Assert.Equal(0.0f, input, precision: 6));
    }

    [Fact]
    public void Lobe_AcceleratorSupportRejectsRulesThatReadRandom()
    {
        var rule = new[]
        {
            new SVRuleEntrySnapshot(0, SVRule.Op.LoadAccumulatorFrom, SVRule.Operand.Random, 0, 0),
        };

        Assert.False(LobeAcceleratorState.RulesCanRunDeterministically(rule, Array.Empty<SVRuleEntrySnapshot>(), out string? reason));
        Assert.Contains("random", reason, StringComparison.OrdinalIgnoreCase);
    }

    private static B LoadBrain()
    {
        var genome = GenomeReader.LoadNew(new Rng(1), Path.GetFullPath(StarterGenomePath));
        var brain = new B();
        brain.ReadFromGenome(genome, new Rng(1));
        brain.RegisterBiochemistry(new float[256]);
        return brain;
    }
}
