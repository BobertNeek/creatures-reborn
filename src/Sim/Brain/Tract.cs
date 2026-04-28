using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Save;
using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Sim.Brain;

/// <summary>
/// A bundle of dendrites connecting a source lobe to a destination lobe.
/// Handles dendrite SVRule update, reward/punishment reinforcement, and migration.
/// Direct port of c2e's <c>Tract</c> class (Tract.h / Tract.cpp).
/// </summary>
public sealed class Tract : BrainComponent
{
    // Hardcoded catalogue defaults (c2e reads from Brain.catalogue at runtime)
    private const int DefaultMaxMigrations    = 8;
    private const int DefaultStrengthSvIndex  = DendriteVar.Strength;  // = 7

    // -------------------------------------------------------------------------
    // Nested: lobe attachment info
    // -------------------------------------------------------------------------
    private struct LobeAttachment
    {
        public Lobe?         Lobe;
        public int           NeuronMin;
        public int           NeuronMax;
        public int           DendritesPerNeuron;
        public int           NeuralGrowthFactorSvIndex;
        public List<Neuron>  Neurons;
    }

    // -------------------------------------------------------------------------
    // Nested: reward / punishment reinforcement details
    // -------------------------------------------------------------------------
    public sealed class ReinforcementDetails
    {
        private bool  _supported;
        private float _threshold;
        private float _rate;
        private byte  _chemIdx;

        public void SetSupported(bool b)   => _supported  = b;
        public void SetThreshold(float f)  => _threshold  = f;
        public void SetRate(float f)       => _rate       = f;
        public void SetChemIndex(byte b)   => _chemIdx    = b;
        public byte GetChemIndex()         => _chemIdx;
        public bool IsSupported()          => _supported;

        /// <summary>
        /// If level of reinforcement chemical exceeds the threshold, tend
        /// <paramref name="variable"/> toward 1 at rate proportional to excess.
        /// Direct port of <c>ReinforcementDetails::ReinforceAVariable</c>.
        /// </summary>
        public void ReinforceAVariable(float levelOfReinforcement, ref float variable)
        {
            if (levelOfReinforcement > _threshold)
            {
                float modifier = levelOfReinforcement - _threshold;
                variable = Math.Clamp(variable + _rate * modifier, -1.0f, 1.0f);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------
    private LobeAttachment _src;
    private LobeAttachment _dst;

    private readonly List<Dendrite> _dendrites     = new();
    private readonly List<Dendrite> _weakDendrites = new();

    private bool _randomConnectAndMigrate;
    private bool _randomDendriteCount;

    private readonly int _maxMigrations   = DefaultMaxMigrations;
    private readonly int _strengthSvIndex = DefaultStrengthSvIndex;

    private IRng? _rng;

    /// <summary>
    /// Short-term → long-term weight convergence rate.
    /// Written by the <c>doSetSTtoLTRate</c> SVRule opcode.
    /// </summary>
    public float STtoLTRate;

    public readonly ReinforcementDetails Reward     = new();
    public readonly ReinforcementDetails Punishment = new();

    // -------------------------------------------------------------------------
    // Constructor — reads one G_TRACT gene and wires up to the lobe list
    // -------------------------------------------------------------------------
    public Tract(Genome.Genome genome, IReadOnlyList<Lobe> lobes, IRng rng)
    {
        _rng = rng;

        UpdateAtTime = genome.GetInt();

        // Source attachment
        int srcToken = genome.GetToken();
        _src.NeuronMin          = genome.GetInt();
        _src.NeuronMax          = genome.GetInt();
        _src.DendritesPerNeuron = genome.GetInt();

        // Destination attachment
        int dstToken = genome.GetToken();
        _dst.NeuronMin          = genome.GetInt();
        _dst.NeuronMax          = genome.GetInt();
        _dst.DendritesPerNeuron = genome.GetInt();

        _randomConnectAndMigrate = genome.GetBool();
        _randomDendriteCount     = genome.GetBool();

        _src.NeuralGrowthFactorSvIndex = genome.GetCodon(0, BrainConst.NumSVRuleVariables - 1);
        _dst.NeuralGrowthFactorSvIndex = genome.GetCodon(0, BrainConst.NumSVRuleVariables - 1);

        _runInitRuleAlways = genome.GetCodon(0, 1) > 0;

        // Dummy slots (matching c2e's genome.GetByte() + genome.GetToken())
        genome.GetByte();
        genome.GetToken();

        InitRule.InitFromGenome(genome);
        UpdateRule.InitFromGenome(genome);

        // Find lobes by token
        _src.Neurons = new();
        _dst.Neurons = new();
        foreach (Lobe lobe in lobes)
        {
            if (lobe.Token == srcToken) _src.Lobe = lobe;
            if (lobe.Token == dstToken) _dst.Lobe = lobe;
        }

        if (_src.Lobe == null || _dst.Lobe == null)
            throw new InvalidOperationException($"Tract: could not find src or dst lobe (tokens {srcToken}/{dstToken}).");

        InitNeuronLists();

        if (_src.Neurons.Count == 0 || _dst.Neurons.Count == 0)
            throw new InvalidOperationException("Tract: empty neuron list on src or dst.");

        if (_src.DendritesPerNeuron == 0 && _dst.DendritesPerNeuron == 0)
            throw new InvalidOperationException("Tract: both src and dst dendrites-per-neuron are 0.");

        BuildDendrites();

        _supportsReinforcement = true;
        STtoLTRate = 0;
    }

    // -------------------------------------------------------------------------
    // Initialise — run init SVRule on every dendrite
    // -------------------------------------------------------------------------
    public override void Initialise()
    {
        if (_initialised) return;
        _initialised = true;

        foreach (Dendrite d in _dendrites)
        {
            if (_runInitRuleAlways)
                d.ClearWeights();
            else
                d.InitByRule(InitRule, this);
        }
    }

    // -------------------------------------------------------------------------
    // DoUpdate — migrate, fire update rule, reward/punish, track weak dendrites
    // -------------------------------------------------------------------------
    public override void DoUpdate()
        => DoUpdate(trace: null, tick: 0);

    public void DoUpdate(LearningTrace? trace, int tick)
    {
        if (UpdateAtTime == 0) return;

        if (_randomConnectAndMigrate)
            MigrateWeakDendrites();

        foreach (Dendrite d in _dendrites)
        {
            float[] srcStates   = d.SrcNeuron?.States ?? SVRule.InvalidVariables;
            float[] dstStates   = d.DstNeuron?.States ?? SVRule.InvalidVariables;
            float[] spareStates = _src.Lobe?.GetSpareNeuronVariables() ?? SVRule.InvalidVariables;
            int     srcId       = d.SrcNeuron?.IdInList ?? 0;
            int     dstId       = d.DstNeuron?.IdInList ?? 0;

            if (_runInitRuleAlways)
            {
                InitRule.Process(
                    srcStates, d.Weights, dstStates, spareStates,
                    srcId, dstId, this);
            }

            UpdateRule.Process(
                srcStates, d.Weights, dstStates, spareStates,
                srcId, dstId, this);

            ProcessRewardAndPunishment(d, trace, tick);

            if (_randomConnectAndMigrate)
                UpdateWeakDendritesList(d);
        }
    }

    // -------------------------------------------------------------------------
    // SVRule callbacks — called by SVRule opcodes during dendrite update
    // -------------------------------------------------------------------------

    /// <summary>Stores the ST→LT convergence rate (abs of operand). Called by <c>doSetSTtoLTRate</c>.</summary>
    public void HandleSetSTtoLTRate(float operand) => STtoLTRate = MathF.Abs(operand);

    /// <summary>
    /// Tends ST weight toward LT weight at <see cref="STtoLTRate"/>, and LT toward ST at
    /// <paramref name="ltToSTRate"/>. Called by <c>doSetLTtoSTRateAndDoWeightSTLTWeightConvergence</c>.
    /// </summary>
    public void HandleSetLTtoSTRateAndDoCalc(float ltToSTRate, float[] dendriteVars)
    {
        float oldSTW = dendriteVars[DendriteVar.WeightST];
        float oldLTW = dendriteVars[DendriteVar.WeightLT];
        dendriteVars[DendriteVar.WeightST] += (oldLTW - oldSTW) * STtoLTRate;
        dendriteVars[DendriteVar.WeightLT] += (oldSTW - oldLTW) * ltToSTRate;
    }

    // -------------------------------------------------------------------------
    // ClearActivity — set STW = LTW (used before instinct processing)
    // -------------------------------------------------------------------------
    public void ClearActivity()
    {
        foreach (Dendrite d in _dendrites)
            d.Weights[DendriteVar.WeightST] = d.Weights[DendriteVar.WeightLT];
    }

    // -------------------------------------------------------------------------
    // Accessors
    // -------------------------------------------------------------------------
    public int DendriteCount => _dendrites.Count;

    public TractSnapshot CreateSnapshot(int index, int maxDendrites)
    {
        int count = Math.Min(Math.Max(0, maxDendrites), _dendrites.Count);
        var dendrites = new List<DendriteSnapshot>(count);
        for (int i = 0; i < count; i++)
        {
            Dendrite dendrite = _dendrites[i];
            var weights = new float[BrainConst.NumSVRuleVariables];
            Array.Copy(dendrite.Weights, weights, weights.Length);
            dendrites.Add(new(
                dendrite.IdInList,
                dendrite.SrcNeuron?.IdInList ?? -1,
                dendrite.DstNeuron?.IdInList ?? -1,
                weights));
        }

        int srcToken = _src.Lobe?.Token ?? 0;
        int dstToken = _dst.Lobe?.Token ?? 0;
        return new TractSnapshot(
            index,
            UpdateAtTime,
            srcToken,
            Brain.TokenToString(srcToken),
            dstToken,
            Brain.TokenToString(dstToken),
            _dendrites.Count,
            STtoLTRate,
            dendrites);
    }

    public SavedTractState CreateSaveState(int index)
    {
        var dendrites = new List<SavedDendriteState>(_dendrites.Count);
        for (int i = 0; i < _dendrites.Count; i++)
        {
            dendrites.Add(new SavedDendriteState
            {
                Index = i,
                Weights = (float[])_dendrites[i].Weights.Clone(),
            });
        }

        return new SavedTractState
        {
            Index = index,
            STtoLTRate = STtoLTRate,
            Dendrites = dendrites,
        };
    }

    public void RestoreSaveState(SavedTractState state)
    {
        STtoLTRate = state.STtoLTRate;
        int count = Math.Min(_dendrites.Count, state.Dendrites.Count);
        for (int i = 0; i < count; i++)
        {
            SavedDendriteState saved = state.Dendrites[i];
            if ((uint)saved.Index >= (uint)_dendrites.Count)
                continue;

            Array.Clear(_dendrites[saved.Index].Weights);
            Array.Copy(
                saved.Weights,
                _dendrites[saved.Index].Weights,
                Math.Min(saved.Weights.Length, _dendrites[saved.Index].Weights.Length));
        }
    }

    // -------------------------------------------------------------------------
    // Private: build dendrite list from genome parameters
    // -------------------------------------------------------------------------
    private void BuildDendrites()
    {
        int listId = 0;

        if (_randomConnectAndMigrate)
        {
            if (_src.DendritesPerNeuron > 0 && _dst.DendritesPerNeuron > 0)
                throw new InvalidOperationException("Tract: both constrained in random-migrate mode.");

            if (_src.DendritesPerNeuron == 0)
            {
                // Destination constrained — attach src randomly for each dst neuron
                int clamp = Math.Min(_dst.DendritesPerNeuron, _src.Neurons.Count);
                foreach (Neuron dstN in _dst.Neurons)
                {
                    int n = _randomDendriteCount ? _rng!.Rnd(1, clamp) : clamp;
                    for (int i = 0; i < n; i++)
                    {
                        Dendrite d = new() { IdInList = listId++, DstNeuron = dstN };
                        Neuron srcN;
                        int attempts = 0;
                        do {
                            srcN = _src.Neurons[_rng!.Rnd(_src.Neurons.Count - 1)];
                            attempts++;
                        } while (DoesDendriteExistFromTo(srcN, dstN) && attempts < _src.Neurons.Count);
                        d.SrcNeuron = srcN;
                        _dendrites.Add(d);
                    }
                }
            }
            else
            {
                // Source constrained — attach dst randomly for each src neuron
                int clamp = Math.Min(_src.DendritesPerNeuron, _dst.Neurons.Count);
                foreach (Neuron srcN in _src.Neurons)
                {
                    int n = _randomDendriteCount ? _rng!.Rnd(1, clamp) : clamp;
                    for (int i = 0; i < n; i++)
                    {
                        Dendrite d = new() { IdInList = listId++, SrcNeuron = srcN };
                        Neuron dstN;
                        int attempts = 0;
                        do {
                            dstN = _dst.Neurons[_rng!.Rnd(_dst.Neurons.Count - 1)];
                            attempts++;
                        } while (DoesDendriteExistFromTo(srcN, dstN) && attempts < _dst.Neurons.Count);
                        d.DstNeuron = dstN;
                        _dendrites.Add(d);
                    }
                }
            }
        }
        else
        {
            // Fixed connectivity — interleave src and dst
            if (_src.DendritesPerNeuron == 0 || _dst.DendritesPerNeuron == 0)
                throw new InvalidOperationException("Tract: zero dendrites-per-neuron in fixed-connect mode.");

            int si = 0, di = 0, i = 0;
            do {
                Dendrite d = new(_src.Neurons[si], _dst.Neurons[di]) { IdInList = listId++ };
                _dendrites.Add(d);
                i++;
                if (i % _src.DendritesPerNeuron == 0) di = (di + 1) % _dst.Neurons.Count;
                if (i % _dst.DendritesPerNeuron == 0) si = (si + 1) % _src.Neurons.Count;
            } while (i < BrainConst.MaxDendritesPerTract &&
                     (di != 0 || si != 0));
        }
    }

    private void InitNeuronLists()
    {
        _src.Neurons = new();
        _dst.Neurons = new();

        for (int i = _src.NeuronMin; i <= _src.NeuronMax && i < _src.Lobe!.GetNoOfNeurons(); i++)
            _src.Neurons.Add(_src.Lobe.GetNeuron(i));

        for (int i = _dst.NeuronMin; i <= _dst.NeuronMax && i < _dst.Lobe!.GetNoOfNeurons(); i++)
            _dst.Neurons.Add(_dst.Lobe.GetNeuron(i));
    }

    private bool DoesDendriteExistFromTo(Neuron src, Neuron dst)
    {
        foreach (Dendrite d in _dendrites)
            if (d.SrcNeuron == src && d.DstNeuron == dst) return true;
        return false;
    }

    private Dendrite? GetDendriteIfExistingFromTo(Neuron src, Neuron dst)
    {
        foreach (Dendrite d in _dendrites)
            if (d.SrcNeuron == src && d.DstNeuron == dst) return d;
        return null;
    }

    // -------------------------------------------------------------------------
    // Reward/Punishment
    // -------------------------------------------------------------------------
    private void ProcessRewardAndPunishment(Dendrite d, LearningTrace? trace, int tick)
    {
        if (!Reward.IsSupported() && !Punishment.IsSupported()) return;
        if (d.DstNeuron == null) return;
        if (d.DstNeuron.States[NeuronVar.Output] == 0.0f) return;

        if (Reward.IsSupported() && _chemicals != null)
            ApplyReinforcement(d, trace, tick, Reward, ReinforcementKind.Reward);

        if (Punishment.IsSupported() && _chemicals != null)
            ApplyReinforcement(d, trace, tick, Punishment, ReinforcementKind.Punishment);
    }

    private void ApplyReinforcement(
        Dendrite dendrite,
        LearningTrace? trace,
        int tick,
        ReinforcementDetails details,
        ReinforcementKind kind)
    {
        if (_chemicals == null)
            return;

        int chemicalId = details.GetChemIndex();
        float level = _chemicals[chemicalId];
        float before = dendrite.Weights[DendriteVar.WeightST];
        details.ReinforceAVariable(level, ref dendrite.Weights[DendriteVar.WeightST]);
        float after = dendrite.Weights[DendriteVar.WeightST];
        if (trace == null || before == after)
            return;

        trace.RecordReinforcement(new ReinforcementTrace(
            Tick: tick,
            TractIndex: IdInList,
            ChemicalId: chemicalId,
            Level: level,
            BeforeWeight: before,
            AfterWeight: after,
            Kind: kind)
        {
            DendriteId = dendrite.IdInList,
            SourceNeuronId = dendrite.SrcNeuron?.IdInList ?? -1,
            DestinationNeuronId = dendrite.DstNeuron?.IdInList ?? -1,
        });
    }

    // -------------------------------------------------------------------------
    // Migration
    // -------------------------------------------------------------------------
    private void UpdateWeakDendritesList(Dendrite d)
    {
        if (_maxMigrations == 0) return;

        float strength = d.Weights[_strengthSvIndex];

        // Fast-path: list full and this dendrite is not weaker than the weakest
        if (_weakDendrites.Count == _maxMigrations)
        {
            if (strength >= _weakDendrites[_weakDendrites.Count - 1].Weights[_strengthSvIndex])
                return;
        }

        // Find insertion point (ascending by strength = weakest first)
        int insertAt = _weakDendrites.Count;
        for (int i = 0; i < _weakDendrites.Count; i++)
        {
            if (strength < _weakDendrites[i].Weights[_strengthSvIndex])
            {
                insertAt = i;
                break;
            }
        }
        _weakDendrites.Insert(insertAt, d);

        if (_weakDendrites.Count > _maxMigrations)
            _weakDendrites.RemoveAt(_weakDendrites.Count - 1);
    }

    private void MigrateWeakDendrites()
    {
        if (_weakDendrites.Count == 0) return;

        int srcSvIdx = _src.NeuralGrowthFactorSvIndex;
        int dstSvIdx = _dst.NeuralGrowthFactorSvIndex;

        // Find dst neuron with highest NGF
        Neuron? highestDstNeuron = null;
        float   highestDstNGF   = -1.0f;
        foreach (Neuron n in _dst.Neurons)
        {
            if (n.States[dstSvIdx] > highestDstNGF)
            {
                highestDstNGF   = n.States[dstSvIdx];
                highestDstNeuron = n;
            }
        }

        if (highestDstNGF <= 0.0f || highestDstNeuron == null)
        {
            _weakDendrites.Clear();
            return;
        }

        // Find top-N src neurons with highest NGF
        List<Neuron> topSrc = FindNNeuronsWithHighestGivenState(
            _src.Neurons, srcSvIdx, _weakDendrites.Count);

        foreach (Neuron srcN in topSrc)
        {
            if (srcN.States[srcSvIdx] > 0.0f)
                AttemptMigration(highestDstNeuron, srcN, srcSvIdx);
        }

        _weakDendrites.Clear();
    }

    private static List<Neuron> FindNNeuronsWithHighestGivenState(
        List<Neuron> pool, int svIdx, int maxCount)
    {
        var result = new List<Neuron>(maxCount + 1);
        foreach (Neuron n in pool)
        {
            if (maxCount == 0) break;
            if (result.Count == maxCount &&
                n.States[svIdx] <= result[result.Count - 1].States[svIdx])
                continue;

            int insertAt = result.Count;
            for (int i = 0; i < result.Count; i++)
            {
                if (n.States[svIdx] > result[i].States[svIdx]) { insertAt = i; break; }
            }
            result.Insert(insertAt, n);
            if (result.Count > maxCount)
                result.RemoveAt(result.Count - 1);
        }
        return result;
    }

    private bool AttemptMigration(Neuron dest, Neuron source, int sourceStateIdx)
    {
        Dendrite? existing = GetDendriteIfExistingFromTo(source, dest);
        if (existing != null) return true;

        // Find the strongest weak dendrite whose strength < source's NGF
        for (int i = 0; i < _weakDendrites.Count; i++)
        {
            Dendrite weak = _weakDendrites[i];
            if (weak.Weights[_strengthSvIndex] < source.States[sourceStateIdx])
            {
                weak.SrcNeuron = source;
                weak.DstNeuron = dest;
                _weakDendrites.RemoveAt(i);

                if (_runInitRuleAlways)
                    weak.ClearWeights();
                else
                    weak.InitByRule(InitRule, this);

                return true;
            }
        }
        return false;
    }
}
