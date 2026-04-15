using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Util;
using G      = CreaturesReborn.Sim.Genome.Genome;
using SVRule = CreaturesReborn.Sim.Brain.SVRule;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

/// <summary>
/// Per-opcode unit tests for the SVRule interpreter.
/// Each test builds a minimal rule via a raw byte array, feeds a known state vector,
/// and asserts the expected post-state.
///
/// SVRule.InitFromGenome reads exactly 16 entries × 3 bytes each:
///   byte 0: opcode  (% NumOpCodes)
///   byte 1: operand (% NumOperands)
///   byte 2: arrayIndex (% 8 for var-index operands, % 256 otherwise)
/// FloatValue is derived as: min(1.0, arrayIndex / 248f)
/// </summary>
public class SVRuleOpcodeTests
{
    // -------------------------------------------------------------------------
    // Rule builder helpers
    // -------------------------------------------------------------------------

    // Produce a byte that encodes a float via FloatDivisor: f → (int)(f * 248)
    private static byte FloatByte(float f) => (byte)Math.Clamp((int)(f * BrainConst.FloatDivisor), 0, 255);

    private sealed class RuleBuilder
    {
        private readonly List<byte> _bytes = new();

        /// <summary>Append one SVRule entry.</summary>
        public RuleBuilder Add(SVRule.Op op, SVRule.Operand operand, byte arrayIndex = 0)
        {
            _bytes.Add((byte)(int)op);
            _bytes.Add((byte)(int)operand);
            _bytes.Add(arrayIndex);
            return this;
        }

        /// <summary>Append one SVRule entry with a float value encoded via FloatDivisor.</summary>
        public RuleBuilder AddF(SVRule.Op op, SVRule.Operand operand, float floatValue)
            => Add(op, operand, FloatByte(floatValue));

        /// <summary>Append a StopImmediately entry.</summary>
        public RuleBuilder Stop()
            => Add(SVRule.Op.StopImmediately, SVRule.Operand.Zero, 0);

        public SVRule Build()
        {
            // Pad to 16 entries × 3 bytes = 48 bytes with StopImmediately
            while (_bytes.Count < BrainConst.SVRuleLength * 3)
                _bytes.Add(0);

            byte[] raw = _bytes.ToArray();

            var genome = new G(new Rng(0));
            genome.AttachBytes(raw, 0, 255, 0, "test");

            var rule = new SVRule();
            rule.InitFromGenome(genome);
            return rule;
        }
    }

    private static RuleBuilder Rule() => new();

    // Shortcut: run a rule and return the result
    private static SVRule.ReturnCode Run(SVRule rule,
        float[] input, float[] dend, float[] neur, float[] spare,
        int srcId = 0, int dstId = 0)
        => rule.Process(input, dend, neur, spare, srcId, dstId);

    private static float[] Vec(float a = 0, float b = 0, float c = 0, float d = 0,
                                float e = 0, float f = 0, float g = 0, float h = 0)
        => new[] { a, b, c, d, e, f, g, h };

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public void StopImmediately_StopsBeforeSubsequentEntry()
    {
        var rule = Rule()
            .Stop()
            .AddF(SVRule.Op.Add, SVRule.Operand.Value, 0.9f)   // never reached
            .Build();

        var neur = Vec(0.1f);
        Run(rule, Vec(), Vec(), neur, Vec());
        Assert.Equal(0.1f, neur[NeuronVar.State], 5);
    }

    [Fact]
    public void Add_AddsValueToAccumulator_StoresInNeuronState()
    {
        // inputVars[0] seeds accumulator. Add 0.25, store into neuron[State].
        var rule = Rule()
            .AddF(SVRule.Op.Add, SVRule.Operand.Value, 0.25f)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var input = Vec(0.5f); // accumulator seed
        var neur  = Vec();
        Run(rule, input, Vec(), neur, Vec());
        // ~0.5 + ~0.25 = ~0.75
        Assert.True(neur[NeuronVar.State] > 0.5f && neur[NeuronVar.State] <= 1.0f);
    }

    [Fact]
    public void Subtract_SubtractsValue()
    {
        var rule = Rule()
            .AddF(SVRule.Op.Subtract, SVRule.Operand.Value, 0.2f)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var input = Vec(0.8f);
        var neur  = Vec();
        Run(rule, input, Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.State] > 0.4f && neur[NeuronVar.State] < 0.8f);
    }

    [Fact]
    public void MultiplyBy_MultipliesAccumulator()
    {
        var rule = Rule()
            .AddF(SVRule.Op.MultiplyBy, SVRule.Operand.Value, 0.5f)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var input = Vec(0.8f); // acc seed
        var neur  = Vec();
        Run(rule, input, Vec(), neur, Vec());
        // 0.8 * 0.5 ≈ 0.4
        Assert.True(neur[NeuronVar.State] > 0.2f && neur[NeuronVar.State] < 0.6f);
    }

    [Fact]
    public void DivideBy_DividesAccumulator()
    {
        // acc = 0.6 / 0.5 ≈ 1.0 (clamped)
        var rule = Rule()
            .AddF(SVRule.Op.DivideBy, SVRule.Operand.Value, 0.5f)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var input = Vec(0.6f);
        var neur  = Vec();
        Run(rule, input, Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.State] > 0.8f);
    }

    [Fact]
    public void LoadAccumulatorFrom_NeuronState_LoadsCorrectValue()
    {
        // Store 0.7 into neuron[State], load it back into accumulator, store into neuron[Input]
        var rule = Rule()
            .Add(SVRule.Op.LoadAccumulatorFrom, SVRule.Operand.Neuron, NeuronVar.State)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.Input)
            .Stop()
            .Build();

        var neur = Vec(/*State*/ 0.7f);
        Run(rule, Vec(), Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.Input] > 0.5f, $"Expected ~0.7, got {neur[NeuronVar.Input]}");
    }

    [Fact]
    public void StoreAccumulatorInto_Dendrite_WritesDendriteWeight()
    {
        var rule = Rule()
            .AddF(SVRule.Op.Add, SVRule.Operand.Value, 0.6f)  // acc = 0 + 0.6
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Dendrite, DendriteVar.WeightST)
            .Stop()
            .Build();

        var dend = Vec();
        Run(rule, Vec(), dend, Vec(), Vec());
        Assert.True(dend[DendriteVar.WeightST] > 0.4f);
    }

    [Fact]
    public void IfGreaterThan_SkipsNextWhenFalse()
    {
        // acc = 0.2, IfGreaterThan 0.5 → false → skip Add(0.9)
        var rule = Rule()
            .AddF(SVRule.Op.IfGreaterThan, SVRule.Operand.Value, 0.5f)
            .AddF(SVRule.Op.Add, SVRule.Operand.Value, 0.9f)   // skipped
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var input = Vec(0.2f);
        var neur  = Vec();
        Run(rule, input, Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.State] < 0.5f);
    }

    [Fact]
    public void IfGreaterThan_ExecutesNextWhenTrue()
    {
        // acc = 0.8, IfGreaterThan 0.5 → true → execute Add(0.1)
        var rule = Rule()
            .AddF(SVRule.Op.IfGreaterThan, SVRule.Operand.Value, 0.5f)
            .AddF(SVRule.Op.Add, SVRule.Operand.Value, 0.1f)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var input = Vec(0.8f);
        var neur  = Vec();
        Run(rule, input, Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.State] > 0.5f);
    }

    [Fact]
    public void IfZero_SkipsNextWhenNonZero()
    {
        var rule = Rule()
            .Add(SVRule.Op.IfZero, SVRule.Operand.Value, FloatByte(0.5f))  // operand = 0.5 ≠ 0 → skip
            .AddF(SVRule.Op.Add, SVRule.Operand.Value, 0.8f)               // skipped
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var input = Vec(0.1f);
        var neur  = Vec();
        Run(rule, input, Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.State] < 0.5f);
    }

    [Fact]
    public void BoundInZeroOne_ClampsAboveOne()
    {
        // acc = 0 + 1.5? → can't get > 1.0 from Value operand (max = 1.0)
        // Use Add twice to exceed 1.0 in accumulator
        var rule = Rule()
            .AddF(SVRule.Op.Add, SVRule.Operand.Value, 0.8f)
            .AddF(SVRule.Op.Add, SVRule.Operand.Value, 0.8f)
            .Add(SVRule.Op.BoundInZeroOne, SVRule.Operand.Accumulator)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var neur = Vec();
        Run(rule, Vec(), Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.State] >= 0.0f && neur[NeuronVar.State] <= 1.0f);
    }

    [Fact]
    public void MinIntoAccumulator_TakesSmaller()
    {
        // acc = 0.8 (from input), min(0.3) → result = 0.3
        var rule = Rule()
            .AddF(SVRule.Op.MinIntoAccumulator, SVRule.Operand.Value, 0.3f)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var neur = Vec();
        Run(rule, Vec(0.8f), Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.State] < 0.5f);
    }

    [Fact]
    public void MaxIntoAccumulator_TakesLarger()
    {
        // acc = 0.2 (from input), max(0.7) → result = 0.7
        var rule = Rule()
            .AddF(SVRule.Op.MaxIntoAccumulator, SVRule.Operand.Value, 0.7f)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var neur = Vec();
        Run(rule, Vec(0.2f), Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.State] > 0.5f);
    }

    [Fact]
    public void DoWinnerTakesAll_ReturnsSetSpareWhenNeuronWins()
    {
        // neuron.State = 0.9 > spare.State = 0.1 → wins
        var rule = Rule()
            .Add(SVRule.Op.DoWinnerTakesAll, SVRule.Operand.Zero)
            .Stop()
            .Build();

        var neur  = Vec(0.9f);
        var spare = Vec(0.1f);
        var rc = Run(rule, Vec(), Vec(), neur, spare);
        Assert.Equal(SVRule.ReturnCode.SetSpareNeuronToCurrent, rc);
    }

    [Fact]
    public void DoWinnerTakesAll_DoesNotReturnSetSpareWhenNeuronLoses()
    {
        var rule = Rule()
            .Add(SVRule.Op.DoWinnerTakesAll, SVRule.Operand.Zero)
            .Stop()
            .Build();

        var neur  = Vec(0.1f);
        var spare = Vec(0.9f);
        var rc = Run(rule, Vec(), Vec(), neur, spare);
        Assert.Equal(SVRule.ReturnCode.DoNothing, rc);
    }

    [Fact]
    public void SetToSpareNeuron_AlwaysReturnsSetSpare()
    {
        var rule = Rule()
            .Add(SVRule.Op.SetToSpareNeuron, SVRule.Operand.Zero)
            .Stop()
            .Build();

        var rc = Run(rule, Vec(), Vec(), Vec(), Vec());
        Assert.Equal(SVRule.ReturnCode.SetSpareNeuronToCurrent, rc);
    }

    [Fact]
    public void DoPersistence_BlendsStateAndInput()
    {
        // State = 0.4, Input = 0.6, persistence = ~0.5 → new State = 0.6*(0.5) + 0.4*(0.5) = 0.5
        var rule = Rule()
            .AddF(SVRule.Op.DoPersistence, SVRule.Operand.Value, 0.5f)
            .Stop()
            .Build();

        var neur = Vec(/*State*/ 0.4f, /*Input*/ 0.6f);
        Run(rule, Vec(), Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.State] > 0.3f && neur[NeuronVar.State] < 0.7f);
    }

    [Fact]
    public void NegateOperandIntoAccumulator_Negates()
    {
        // Load 0.5 into neuron[State], negate it, store into neuron[Input]
        var rule = Rule()
            .Add(SVRule.Op.NegateOperandIntoAccumulator, SVRule.Operand.Neuron, NeuronVar.State)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.Input)
            .Stop()
            .Build();

        var neur = Vec(/*State*/ 0.5f);
        Run(rule, Vec(), Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.Input] < 0.0f);
    }

    [Fact]
    public void SetTendRate_ThenTend_ConvergesPartially()
    {
        // Set tend rate = 0.5, then tend acc (=0.0) toward 1.0 → result ≈ 0.5
        var rule = Rule()
            .AddF(SVRule.Op.SetTendRate, SVRule.Operand.Value, 0.5f)
            .AddF(SVRule.Op.TendAccumulatorToOperandAtTendRate, SVRule.Operand.Value, 1.0f)
            .Add(SVRule.Op.StoreAccumulatorInto, SVRule.Operand.Neuron, NeuronVar.State)
            .Stop()
            .Build();

        var neur = Vec();
        Run(rule, Vec(0.0f), Vec(), neur, Vec());
        Assert.True(neur[NeuronVar.State] > 0.2f && neur[NeuronVar.State] < 0.8f);
    }
}
