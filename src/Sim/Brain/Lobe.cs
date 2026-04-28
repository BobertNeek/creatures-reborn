using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.Save;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Brain;

/// <summary>
/// A grid of neurons, each updated by init+update SVRules each tick.
/// Tracks a "winner" neuron (highest State) via the SetSpareNeuronToCurrent opcode.
/// Direct port of c2e's <c>Lobe</c> class (Lobe.h / Lobe.cpp).
/// </summary>
public sealed class Lobe : BrainComponent
{
    // -------------------------------------------------------------------------
    // Genome-read fields
    // -------------------------------------------------------------------------
    public  int    Token;        // 4-char packed token (e.g. "driv", "attn")
    private int    _x, _y;      // vat-tool display coords (unused in sim)
    public  int    Width;
    public  int    Height;
    private byte[] _colour = new byte[3];
    private int    _tissueId;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------
    private readonly List<Neuron> _neurons     = new();
    private float[]               _neuronInputs = Array.Empty<float>();

    // Points to the current "spare" (winner) neuron variables.
    // Starts at neurons[0] after init; updated each DoUpdate() pass.
    private float[] _spareNeuronVars = new float[BrainConst.NumSVRuleVariables];
    private int     _winningNeuronId;

    // -------------------------------------------------------------------------
    // Constructor — reads one G_LOBE gene from the genome
    // -------------------------------------------------------------------------
    public Lobe(G genome)
    {
        _winningNeuronId = 0;

        Token       = genome.GetToken();
        UpdateAtTime = genome.GetInt();

        _x      = genome.GetInt();
        _y      = genome.GetInt();
        Width   = genome.GetByte();
        Height  = genome.GetByte();

        _colour[0] = genome.GetByte();
        _colour[1] = genome.GetByte();
        _colour[2] = genome.GetByte();

        // winner-takes-all flag (obsoleted in C3; always use WTA opcode)
        genome.GetBool();

        _tissueId = genome.GetByte();

        _runInitRuleAlways = genome.GetCodon(0, 1) > 0;

        // Dummy slots
        genome.GetByte();
        genome.GetInt();
        genome.GetToken();

        InitRule.InitFromGenome(genome);
        UpdateRule.InitFromGenome(genome);

        int noOfNeurons = Width * Height;
        if (noOfNeurons == 0 || noOfNeurons > BrainConst.MaxNeuronsPerLobe)
            throw new InvalidOperationException($"Lobe: neuron count {noOfNeurons} out of range.");

        _neuronInputs = new float[noOfNeurons];
        for (int i = 0; i < noOfNeurons; i++)
        {
            _neurons.Add(new Neuron { IdInList = i });
        }

        // Spare vars start pointing at neuron 0's states
        _spareNeuronVars = _neurons[0].States;
    }

    // -------------------------------------------------------------------------
    // Initialise — run init SVRule on each neuron once
    // -------------------------------------------------------------------------
    public override void Initialise()
    {
        if (_initialised) return;
        _initialised = true;

        for (int i = 0; i < _neurons.Count; i++)
        {
            Neuron n = _neurons[i];
            if (_runInitRuleAlways)
            {
                n.ClearStates();
            }
            else
            {
                // inputVars[0] = 0 for lobe init; same src and dst id
                InitRule.Process(
                    SVRule.InvalidVariables, SVRule.InvalidVariables, n.States,
                    SVRule.InvalidVariables, i, i);
            }
        }
    }

    // -------------------------------------------------------------------------
    // DoUpdate — run update (and optionally init) rule on every neuron; track WTA
    // -------------------------------------------------------------------------
    public override void DoUpdate()
    {
        if (UpdateAtTime == 0) return;

        // Use a fresh dummy spare to avoid neuron 0 being the default winner
        var dummySpare = new float[BrainConst.NumSVRuleVariables];
        _spareNeuronVars = dummySpare;
        _winningNeuronId = 0;

        for (int i = 0; i < _neurons.Count; i++)
        {
            Neuron n = _neurons[i];

            // Latch accumulated input into inputVars[0] then reset the accumulator
            SVRule.InvalidVariables[0] = _neuronInputs[i];
            _neuronInputs[i] = 0.0f;

            bool flagAsSpare = false;

            if (_runInitRuleAlways)
            {
                var rc = InitRule.Process(
                    SVRule.InvalidVariables, SVRule.InvalidVariables, n.States,
                    _spareNeuronVars, n.IdInList, n.IdInList);
                if (rc == SVRule.ReturnCode.SetSpareNeuronToCurrent)
                    flagAsSpare = true;
            }

            {
                var rc = UpdateRule.Process(
                    SVRule.InvalidVariables, SVRule.InvalidVariables, n.States,
                    _spareNeuronVars, n.IdInList, n.IdInList);
                if (rc == SVRule.ReturnCode.SetSpareNeuronToCurrent)
                    flagAsSpare = true;
            }

            if (flagAsSpare)
            {
                _spareNeuronVars = n.States;
                _winningNeuronId = i;
            }
        }

        // If no neuron won, default back to neuron 0
        if (_spareNeuronVars == dummySpare)
        {
            _spareNeuronVars = _neurons[0].States;
            _winningNeuronId = 0;
        }
    }

    // -------------------------------------------------------------------------
    // Input control — used by Brain to feed sensory data in
    // -------------------------------------------------------------------------
    public void SetNeuronInput(int neuron, float value)
    {
        if ((uint)neuron < (uint)_neuronInputs.Length)
            _neuronInputs[neuron] = value;
    }

    public void SetLobeWideInput(float value)
    {
        for (int i = 0; i < _neuronInputs.Length; i++)
            _neuronInputs[i] = value;
    }

    public void ClearNeuronActivity(int neuron)
    {
        if ((uint)neuron < (uint)_neurons.Count)
            _neurons[neuron].States[NeuronVar.State] = 0.0f;
    }

    public void ClearActivity()
    {
        foreach (Neuron n in _neurons)
            n.States[NeuronVar.State] = 0.0f;
    }

    // -------------------------------------------------------------------------
    // Accessors used by Tract, Brain, and IBrainLocusProvider
    // -------------------------------------------------------------------------
    public int    GetNoOfNeurons()          => _neurons.Count;
    public Neuron GetNeuron(int i)          => _neurons[i];
    public int    GetToken()                => Token;
    public int    GetTissueId()             => _tissueId;
    public int    GetWhichNeuronWon()       => _winningNeuronId;
    public float[] GetSpareNeuronVariables() => _spareNeuronVars;

    public float GetNeuronState(int neuron, int stateVar)
    {
        if ((uint)neuron >= (uint)_neurons.Count) return 0.0f;
        if ((uint)stateVar >= BrainConst.NumSVRuleVariables) return 0.0f;
        return _neurons[neuron].States[stateVar];
    }

    public LobeSnapshot CreateSnapshot(int index, int maxNeurons)
    {
        int count = Math.Min(Math.Max(0, maxNeurons), _neurons.Count);
        var neurons = new List<NeuronSnapshot>(count);
        for (int i = 0; i < count; i++)
        {
            var states = new float[BrainConst.NumSVRuleVariables];
            Array.Copy(_neurons[i].States, states, states.Length);
            neurons.Add(new NeuronSnapshot(i, states));
        }

        return new LobeSnapshot(
            index,
            Token,
            Brain.TokenToString(Token),
            _tissueId,
            Width,
            Height,
            UpdateAtTime,
            _neurons.Count,
            _winningNeuronId,
            neurons);
    }

    public SavedLobeState CreateSaveState(int index)
    {
        var neurons = new List<SavedNeuronState>(_neurons.Count);
        for (int i = 0; i < _neurons.Count; i++)
        {
            neurons.Add(new SavedNeuronState
            {
                Index = i,
                States = (float[])_neurons[i].States.Clone(),
            });
        }

        return new SavedLobeState
        {
            Index = index,
            Token = Token,
            WinningNeuronId = _winningNeuronId,
            Neurons = neurons,
        };
    }

    public void RestoreSaveState(SavedLobeState state)
    {
        int count = Math.Min(_neurons.Count, state.Neurons.Count);
        for (int i = 0; i < count; i++)
        {
            SavedNeuronState saved = state.Neurons[i];
            if ((uint)saved.Index >= (uint)_neurons.Count)
                continue;

            Array.Clear(_neurons[saved.Index].States);
            Array.Copy(
                saved.States,
                _neurons[saved.Index].States,
                Math.Min(saved.States.Length, _neurons[saved.Index].States.Length));
        }

        _winningNeuronId = Math.Clamp(state.WinningNeuronId, 0, Math.Max(0, _neurons.Count - 1));
        _spareNeuronVars = _neurons.Count > 0
            ? _neurons[_winningNeuronId].States
            : SVRule.InvalidVariables;
    }
}
