using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Sim.Brain;

/// <summary>
/// 16-opcode accumulator-based interpreter that updates neuron / dendrite state vectors.
/// Direct port of c2e's <c>SVRule</c> class (SVRule.h / SVRule.cpp).
/// </summary>
/// <remarks>
/// <para>
/// Each rule contains 16 entries of (opCode, operandVariable, arrayIndex, floatValue).
/// The interpreter runs an accumulator + tendRate over the entries, reading from / writing to
/// four 8-element float arrays: inputVars, dendriteVars, neuronVars, spareNeuronVars.
/// </para>
/// <para>
/// Arrays are mutable in-place — passing them by value (reference-type default in C#)
/// is equivalent to c2e's <c>SVRuleVariables&amp;</c> references.
/// </para>
/// </remarks>
public sealed class SVRule
{
    // -------------------------------------------------------------------------
    // Enums — mirror c2e SVRule::OpCodes and SVRule::OperandCodes exactly
    // -------------------------------------------------------------------------
    public enum Op : int
    {
        StopImmediately = 0,            BlankOperand,
        StoreAccumulatorInto,           LoadAccumulatorFrom,
        IfEqualTo,                      IfNotEqualTo,
        IfGreaterThan,                  IfLessThan,
        IfGreaterThanOrEqualTo,         IfLessThanOrEqualTo,
        IfZero,                         IfNonZero,
        IfPositive,                     IfNegative,
        IfNonNegative,                  IfNonPositive,
        Add,                            Subtract,
        SubtractFrom,                   MultiplyBy,
        DivideBy,                       DivideInto,
        MinIntoAccumulator,             MaxIntoAccumulator,
        SetTendRate,                    TendAccumulatorToOperandAtTendRate,
        NegateOperandIntoAccumulator,   LoadAbsoluteValueOfOperandIntoAccumulator,
        GetDistanceTo,                  FlipAccumulatorAround,
        NoOperation,                    SetToSpareNeuron,
        BoundInZeroOne,                 BoundInMinusOnePlusOne,
        AddAndStoreIn,                  TendToAndStoreIn,
        DoNominalThreshold,             DoLeakageRate,
        DoRestState,                    DoInputGainLoHi,
        DoPersistence,                  DoSignalNoise,
        DoWinnerTakesAll,               DoSetSTtoLTRate,
        DoSetLTtoSTRateAndDoWeightSTLTWeightConvergence, StoreAbsInto,
        IfZeroStop,                     IfNZeroStop,
        IfZeroGoto,                     IfNZeroGoto,
        DivideAndAddToNeuronInput,      MultiplyAndAddToNeuronInput,
        GotoLine,
        IfLessThanStop,                 IfGreaterThanStop,
        IfLessThanOrEqualStop,          IfGreaterThanOrEqualStop,
        SetRewardThreshold,             SetRewardRate,
        SetRewardChemicalIndex,         SetPunishmentThreshold,
        SetPunishmentRate,              SetPunishmentChemicalIndex,
        PreserveVariable,               RestoreVariable,
        PreserveSpareVariable,          RestoreSpareVariable,
        IfNegativeGoto,                 IfPositiveGoto,
        NumOpCodes,
    }

    public enum Operand : int
    {
        Accumulator = 0,
        InputNeuron, Dendrite, Neuron, SpareNeuron,
        Random,
        ChemBySrc, Chem, ChemByDst,
        Zero, One,
        Value, NegativeValue, ValueTen, ValueTenth, ValueInt,
        NumOperands,
    }

    public enum ReturnCode { DoNothing = 0, SetSpareNeuronToCurrent = 1 }

    // -------------------------------------------------------------------------
    // SVRule entry
    // -------------------------------------------------------------------------
    private struct Entry
    {
        public int   OpCode;
        public int   OperandCode;
        public int   ArrayIndex;
        public float FloatValue;
    }

    // -------------------------------------------------------------------------
    // Static shared zero-array (mirrors c2e's invalidVariables)
    // -------------------------------------------------------------------------
    public static readonly float[] InvalidVariables = new float[BrainConst.NumSVRuleVariables];

    // -------------------------------------------------------------------------
    // Instance state
    // -------------------------------------------------------------------------
    private readonly Entry[] _entries = new Entry[BrainConst.SVRuleLength + 1];
    private float[]? _chemicals;
    private IRng?    _rng;

    public SVRule()
    {
        // Sentinel stop at the end.
        _entries[BrainConst.SVRuleLength].OpCode = (int)Op.StopImmediately;
    }

    public void RegisterChemicals(float[] chemicals) => _chemicals = chemicals;
    public void RegisterRng(IRng rng) => _rng = rng;

    public IReadOnlyList<SVRuleEntrySnapshot> DescribeEntries()
    {
        var entries = new List<SVRuleEntrySnapshot>(BrainConst.SVRuleLength);
        for (int i = 0; i < BrainConst.SVRuleLength; i++)
        {
            Entry entry = _entries[i];
            entries.Add(new(
                i,
                (Op)entry.OpCode,
                (Operand)entry.OperandCode,
                entry.ArrayIndex,
                entry.FloatValue));
        }

        return entries;
    }

    // -------------------------------------------------------------------------
    // InitFromGenome — mirrors c2e SVRule::InitFromGenome
    // -------------------------------------------------------------------------
    public void InitFromGenome(Genome.Genome genome)
    {
        for (int i = 0; i < BrainConst.SVRuleLength; i++)
        {
            ref Entry e = ref _entries[i];
            e.OpCode      = genome.GetCodonLessThan((int)Op.NumOpCodes);
            e.OperandCode = genome.GetCodonLessThan((int)Operand.NumOperands);

            bool isVarIndex = e.OperandCode is
                (int)Operand.InputNeuron or (int)Operand.Dendrite or
                (int)Operand.Neuron      or (int)Operand.SpareNeuron;

            e.ArrayIndex = genome.GetCodonLessThan(isVarIndex ? BrainConst.NumSVRuleVariables : 256);
            e.FloatValue = MathF.Min(1.0f, e.ArrayIndex / (float)BrainConst.FloatDivisor);
        }
    }

    // -------------------------------------------------------------------------
    // Process — the interpreter hot loop.
    // Matches c2e SVRule::ProcessGivenVariables verbatim.
    // All four arrays are mutated in-place (reference-type pass-by-value in C#).
    // -------------------------------------------------------------------------
    public ReturnCode Process(
        float[] inputVars,
        float[] dendriteVars,
        float[] neuronVars,
        float[] spareNeuronVars,
        int     srcNeuronId,
        int     dstNeuronId,
        Tract?  owner = null)
    {
        float accumulator = inputVars[0];
        float tendRate    = 0.0f;
        var   rc          = ReturnCode.DoNothing;

        for (int i = 0; i < BrainConst.SVRuleLength; i++)
        {
            ref Entry e  = ref _entries[i];
            var       op = (Op)e.OpCode;

            // ---- Classify operation ----
            if (IsNoOperandOp(op))
            {
                switch (op)
                {
                    case Op.StopImmediately: return rc;
                    case Op.NoOperation:     break;
                    case Op.SetToSpareNeuron: rc = ReturnCode.SetSpareNeuronToCurrent; break;
                    case Op.DoWinnerTakesAll:
                        if (neuronVars[NeuronVar.State] >= spareNeuronVars[NeuronVar.State])
                        {
                            spareNeuronVars[NeuronVar.Output] = 0.0f;
                            neuronVars[NeuronVar.Output]      = neuronVars[NeuronVar.State];
                            rc = ReturnCode.SetSpareNeuronToCurrent;
                        }
                        break;
                }
                continue;
            }

            if (IsWriteOp(op))
            {
                // Resolve write destination array + index
                float[] destArr = GetDestArray(e.OperandCode, inputVars, dendriteVars, neuronVars, spareNeuronVars);
                if (destArr == InvalidVariables) { continue; } // unmapped → discard
                int destIdx = e.ArrayIndex % BrainConst.NumSVRuleVariables;
                switch (op)
                {
                    case Op.StoreAccumulatorInto: destArr[destIdx] = BoundMP1(accumulator);  break;
                    case Op.AddAndStoreIn:        destArr[destIdx] = BoundMP1(accumulator + destArr[destIdx]); break;
                    case Op.BlankOperand:         destArr[destIdx] = 0.0f;                   break;
                    case Op.TendToAndStoreIn:     destArr[destIdx] = BoundMP1(accumulator * (1.0f - tendRate) + destArr[destIdx] * tendRate); break;
                    case Op.StoreAbsInto:         destArr[destIdx] = Bound01(MathF.Abs(accumulator)); break;
                }
                continue;
            }

            // ---- Read operand ----
            float operand = e.OperandCode == (int)Operand.Accumulator
                ? accumulator
                : ReadOperand(e, inputVars, dendriteVars, neuronVars, spareNeuronVars, srcNeuronId, dstNeuronId);

            // ---- Execute read op ----
            switch (op)
            {
                case Op.LoadAccumulatorFrom:  accumulator = operand; break;

                case Op.IfEqualTo:                if (accumulator != operand) i++; break;
                case Op.IfNotEqualTo:             if (accumulator == operand) i++; break;
                case Op.IfGreaterThan:            if (accumulator <= operand) i++; break;
                case Op.IfLessThan:               if (accumulator >= operand) i++; break;
                case Op.IfGreaterThanOrEqualTo:   if (accumulator <  operand) i++; break;
                case Op.IfLessThanOrEqualTo:      if (accumulator >  operand) i++; break;
                case Op.IfZero:                   if (operand != 0.0f) i++; break;
                case Op.IfNonZero:                if (operand == 0.0f) i++; break;
                case Op.IfPositive:               if (operand <= 0.0f) i++; break;
                case Op.IfNegative:               if (operand >= 0.0f) i++; break;
                case Op.IfNonNegative:            if (operand <  0.0f) i++; break;
                case Op.IfNonPositive:            if (operand >  0.0f) i++; break;

                case Op.IfZeroStop:              if (operand == 0.0f) return rc; break;
                case Op.IfNZeroStop:             if (operand != 0.0f) return rc; break;
                case Op.IfLessThanStop:          if (accumulator <  operand) return rc; break;
                case Op.IfGreaterThanStop:       if (accumulator >  operand) return rc; break;
                case Op.IfLessThanOrEqualStop:   if (accumulator <= operand) return rc; break;
                case Op.IfGreaterThanOrEqualStop:if (accumulator >= operand) return rc; break;

                case Op.IfZeroGoto:     if (accumulator == 0) GotoForward(ref i, operand); break;
                case Op.IfNZeroGoto:    if (accumulator != 0) GotoForward(ref i, operand); break;
                case Op.IfNegativeGoto: if (accumulator <  0) GotoForward(ref i, operand); break;
                case Op.IfPositiveGoto: if (accumulator >  0) GotoForward(ref i, operand); break;
                case Op.GotoLine:                              GotoForward(ref i, operand); break;

                case Op.Add:              accumulator += operand; break;
                case Op.Subtract:         accumulator -= operand; break;
                case Op.SubtractFrom:     accumulator  = operand - accumulator; break;
                case Op.MultiplyBy:       accumulator *= operand; break;
                case Op.DivideBy:         if (operand != 0) accumulator /= operand; break;
                case Op.DivideInto:       if (accumulator != 0) accumulator = operand / accumulator; break;
                case Op.MaxIntoAccumulator: if (operand > accumulator) accumulator = operand; break;
                case Op.MinIntoAccumulator: if (operand < accumulator) accumulator = operand; break;
                case Op.SetTendRate:      tendRate = MathF.Abs(operand); break;
                case Op.TendAccumulatorToOperandAtTendRate:
                    accumulator = accumulator * (1.0f - tendRate) + operand * tendRate; break;
                case Op.NegateOperandIntoAccumulator: accumulator = -operand; break;
                case Op.LoadAbsoluteValueOfOperandIntoAccumulator: accumulator = MathF.Abs(operand); break;
                case Op.GetDistanceTo:    accumulator = MathF.Abs(accumulator - operand); break;
                case Op.FlipAccumulatorAround: accumulator = operand - accumulator; break;
                case Op.BoundInZeroOne:   accumulator = Bound01(operand); break;
                case Op.BoundInMinusOnePlusOne: accumulator = BoundMP1(operand); break;

                // C2-style slider ops
                case Op.DoNominalThreshold:
                    if (neuronVars[NeuronVar.Input] < operand) neuronVars[NeuronVar.Input] = 0.0f; break;
                case Op.DoLeakageRate:
                    tendRate = operand; break;
                case Op.DoRestState:
                    neuronVars[NeuronVar.Input] = neuronVars[NeuronVar.Input] * (1.0f - tendRate) + operand * tendRate; break;
                case Op.DoInputGainLoHi:
                    neuronVars[NeuronVar.Input] *= operand; break;
                case Op.DoPersistence:
                    neuronVars[NeuronVar.State] = neuronVars[NeuronVar.Input] * (1.0f - operand) + neuronVars[NeuronVar.State] * operand; break;
                case Op.DoSignalNoise:
                    neuronVars[NeuronVar.State] += operand * (_rng?.RndFloat() ?? 0.0f); break;
                case Op.DivideAndAddToNeuronInput:
                    if (operand != 0) { accumulator /= operand; neuronVars[NeuronVar.Input] = BoundMP1(neuronVars[NeuronVar.Input] + accumulator); } break;
                case Op.MultiplyAndAddToNeuronInput:
                    accumulator *= operand; neuronVars[NeuronVar.Input] = BoundMP1(neuronVars[NeuronVar.Input] + accumulator); break;

                // Reinforcement ops (delegate to Tract)
                case Op.DoSetSTtoLTRate:
                    owner?.HandleSetSTtoLTRate(MathF.Abs(operand)); break;
                case Op.DoSetLTtoSTRateAndDoWeightSTLTWeightConvergence:
                    owner?.HandleSetLTtoSTRateAndDoCalc(operand, dendriteVars); break;
                case Op.SetRewardThreshold:
                    owner?.Reward.SetThreshold(BoundMP1(operand)); break;
                case Op.SetRewardRate:
                    owner?.Reward.SetRate(BoundMP1(operand)); break;
                case Op.SetRewardChemicalIndex:
                    owner?.Reward.SetChemIndex((byte)(ToInt(operand) % BiochemConst.NUMCHEM));
                    owner?.Reward.SetSupported(true); break;
                case Op.SetPunishmentThreshold:
                    owner?.Punishment.SetThreshold(BoundMP1(operand)); break;
                case Op.SetPunishmentRate:
                    owner?.Punishment.SetRate(BoundMP1(operand)); break;
                case Op.SetPunishmentChemicalIndex:
                    owner?.Punishment.SetChemIndex((byte)(ToInt(operand) % BiochemConst.NUMCHEM));
                    owner?.Punishment.SetSupported(true); break;

                case Op.PreserveVariable:
                    neuronVars[NeuronVar.Fourth] = neuronVars[ToInt(operand) % BrainConst.NumSVRuleVariables]; break;
                case Op.RestoreVariable:
                    neuronVars[ToInt(operand) % BrainConst.NumSVRuleVariables] = neuronVars[NeuronVar.Fourth]; break;
                case Op.PreserveSpareVariable:
                    spareNeuronVars[NeuronVar.Fourth] = spareNeuronVars[ToInt(operand) % BrainConst.NumSVRuleVariables]; break;
                case Op.RestoreSpareVariable:
                    spareNeuronVars[ToInt(operand) % BrainConst.NumSVRuleVariables] = spareNeuronVars[NeuronVar.Fourth]; break;
            }
        }
        return rc;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static bool IsNoOperandOp(Op op) =>
        op is Op.StopImmediately or Op.SetToSpareNeuron or Op.NoOperation or Op.DoWinnerTakesAll;

    private static bool IsWriteOp(Op op) =>
        op is Op.BlankOperand or Op.StoreAccumulatorInto or Op.AddAndStoreIn
          or Op.TendToAndStoreIn or Op.StoreAbsInto;

    private static float[] GetDestArray(int operandCode,
        float[] input, float[] dend, float[] neur, float[] spare)
    {
        return operandCode switch
        {
            (int)Operand.InputNeuron  => input,
            (int)Operand.Dendrite     => dend,
            (int)Operand.Neuron       => neur,
            (int)Operand.SpareNeuron  => spare,
            _                         => InvalidVariables,
        };
    }

    private float ReadOperand(in Entry e,
        float[] input, float[] dend, float[] neur, float[] spare,
        int srcId, int dstId)
    {
        int ai = e.ArrayIndex;
        return (Operand)e.OperandCode switch
        {
            Operand.InputNeuron  => input[ai % BrainConst.NumSVRuleVariables],
            Operand.Dendrite     => dend[ai % BrainConst.NumSVRuleVariables],
            Operand.Neuron       => neur[ai % BrainConst.NumSVRuleVariables],
            Operand.SpareNeuron  => spare[ai % BrainConst.NumSVRuleVariables],
            Operand.Random       => _rng?.RndFloat() ?? 0.0f,
            Operand.ChemBySrc    => _chemicals?[(ai + srcId) % BiochemConst.NUMCHEM] ?? 0.0f,
            Operand.Chem         => _chemicals?[ai % BiochemConst.NUMCHEM]           ?? 0.0f,
            Operand.ChemByDst    => _chemicals?[(ai + dstId) % BiochemConst.NUMCHEM] ?? 0.0f,
            Operand.Zero         => 0.0f,
            Operand.One          => 1.0f,
            Operand.Value        => e.FloatValue,
            Operand.NegativeValue=> -e.FloatValue,
            Operand.ValueTen     => e.FloatValue * 10.0f,
            Operand.ValueTenth   => e.FloatValue / 10.0f,
            Operand.ValueInt     => (float)(int)(e.FloatValue * BrainConst.FloatDivisor),
            _                    => 0.0f,
        };
    }

    private static void GotoForward(ref int i, float operand)
    {
        int target = ToInt(operand) - 1;
        if (target > i && target <= BrainConst.SVRuleLength)
            i = target - 1; // -1 because the for-loop increments
    }

    private static float Bound01(float v)    => v < 0 ? 0 : v > 1 ? 1 : v;
    private static float BoundMP1(float v)   => v < -1 ? -1 : v > 1 ? 1 : v;
    private static int   ToInt(float v)      => (int)(v * BrainConst.FloatDivisor);
}
