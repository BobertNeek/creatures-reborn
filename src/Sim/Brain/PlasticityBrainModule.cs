using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.Brain;

public sealed record PlasticityBrainModuleOptions
{
    public bool Enabled { get; init; }
    public int? ShadowedLobeToken { get; init; }
    public int SourceLobeToken { get; init; } = Brain.TokenFromString("driv");
    public int TargetLobeToken { get; init; } = Brain.TokenFromString("decn");
    public int SourceNeuronCount { get; init; } = 8;
    public int TargetNeuronCount { get; init; } = 8;
    public int SourceStateVariable { get; init; } = NeuronVar.State;
    public int TargetStateVariable { get; init; } = NeuronVar.State;
    public float LearningRate { get; init; } = 0.05f;
    public float WeightDecayRate { get; init; }
    public float MinWeight { get; init; } = -1.0f;
    public float MaxWeight { get; init; } = 1.0f;
    public float OutputGain { get; init; } = 1.0f;
    public bool WriteTargetInputs { get; init; }
}

public sealed record PlasticityBrainModuleSnapshot(
    int Tick,
    bool Enabled,
    int? ShadowedLobeToken,
    string? ShadowedLobeTokenText,
    int SourceLobeToken,
    string SourceLobeTokenText,
    int TargetLobeToken,
    string TargetLobeTokenText,
    float[] SourceStates,
    float[] TargetStates,
    float[] Outputs,
    float[] Weights,
    float[] LastWeightDeltas);

public sealed class PlasticityBrainModule : IBrainModule, IBrainModuleSnapshotProvider
{
    public const int MaxTrackedNeurons = 64;

    private readonly PlasticityBrainModuleOptions _options;
    private readonly float[] _sourceStates;
    private readonly float[] _targetStates;
    private readonly float[] _outputs;
    private readonly float[] _weights;
    private readonly float[] _lastWeightDeltas;
    private int _tick;

    public PlasticityBrainModule()
        : this(new PlasticityBrainModuleOptions())
    {
    }

    public PlasticityBrainModule(PlasticityBrainModuleOptions options)
    {
        _options = Validate(options);
        _sourceStates = new float[_options.SourceNeuronCount];
        _targetStates = new float[_options.TargetNeuronCount];
        _outputs = new float[_options.TargetNeuronCount];
        _weights = new float[_options.SourceNeuronCount * _options.TargetNeuronCount];
        _lastWeightDeltas = new float[_weights.Length];
    }

    public int? ShadowedLobeToken => _options.Enabled ? _options.ShadowedLobeToken : null;

    public PlasticityBrainModuleSnapshot LatestSnapshot => CreateSnapshot();

    public void Initialise(Brain brain)
    {
        CaptureStates(brain);
    }

    public void Update(Brain brain)
    {
        _tick++;
        CaptureStates(brain);
        Array.Clear(_outputs, 0, _outputs.Length);
        Array.Clear(_lastWeightDeltas, 0, _lastWeightDeltas.Length);

        if (!_options.Enabled)
            return;

        for (int target = 0; target < _targetStates.Length; target++)
        {
            float output = 0.0f;
            for (int source = 0; source < _sourceStates.Length; source++)
            {
                int weightIndex = WeightIndex(source, target);
                float previous = _weights[weightIndex];
                float decayed = previous - (previous * _options.WeightDecayRate);
                float learned = _options.LearningRate * _sourceStates[source] * _targetStates[target];
                float next = Math.Clamp(decayed + learned, _options.MinWeight, _options.MaxWeight);
                _weights[weightIndex] = next;
                _lastWeightDeltas[weightIndex] = next - previous;
                output += next * _sourceStates[source];
            }

            _outputs[target] = output * _options.OutputGain;
        }

        if (_options.WriteTargetInputs)
            WriteTargetInputs(brain);
    }

    public BrainModuleSnapshot CreateModuleSnapshot()
    {
        var states = new List<BrainModuleStateValue>(
            _sourceStates.Length + _targetStates.Length + _outputs.Length);
        for (int i = 0; i < _sourceStates.Length; i++)
            states.Add(new BrainModuleStateValue($"source:{i}", _sourceStates[i]));
        for (int i = 0; i < _targetStates.Length; i++)
            states.Add(new BrainModuleStateValue($"target:{i}", _targetStates[i]));
        for (int i = 0; i < _outputs.Length; i++)
            states.Add(new BrainModuleStateValue($"output:{i}", _outputs[i]));

        var weights = new List<BrainModuleWeightState>(_weights.Length);
        for (int target = 0; target < _targetStates.Length; target++)
        {
            for (int source = 0; source < _sourceStates.Length; source++)
            {
                int index = WeightIndex(source, target);
                weights.Add(new BrainModuleWeightState(
                    $"w:{source}->{target}",
                    source,
                    target,
                    _weights[index],
                    _lastWeightDeltas[index]));
            }
        }

        return new BrainModuleSnapshot(
            nameof(PlasticityBrainModule),
            _tick,
            "local-hebbian-plasticity",
            _options.Enabled,
            ShadowedLobeToken,
            ShadowedLobeToken.HasValue ? Brain.TokenToString(ShadowedLobeToken.Value) : null,
            states,
            weights);
    }

    private PlasticityBrainModuleSnapshot CreateSnapshot()
        => new(
            _tick,
            _options.Enabled,
            ShadowedLobeToken,
            ShadowedLobeToken.HasValue ? Brain.TokenToString(ShadowedLobeToken.Value) : null,
            _options.SourceLobeToken,
            Brain.TokenToString(_options.SourceLobeToken),
            _options.TargetLobeToken,
            Brain.TokenToString(_options.TargetLobeToken),
            Copy(_sourceStates),
            Copy(_targetStates),
            Copy(_outputs),
            Copy(_weights),
            Copy(_lastWeightDeltas));

    private void CaptureStates(Brain brain)
    {
        CaptureLobeStates(
            brain.GetLobeByToken(_options.SourceLobeToken),
            _options.SourceStateVariable,
            _sourceStates);
        CaptureLobeStates(
            brain.GetLobeByToken(_options.TargetLobeToken),
            _options.TargetStateVariable,
            _targetStates);
    }

    private static void CaptureLobeStates(Lobe? lobe, int stateVariable, float[] destination)
    {
        for (int i = 0; i < destination.Length; i++)
            destination[i] = lobe?.GetNeuronState(i, stateVariable) ?? 0.0f;
    }

    private void WriteTargetInputs(Brain brain)
    {
        Lobe? target = brain.GetLobeByToken(_options.TargetLobeToken);
        if (target == null)
            return;

        for (int i = 0; i < _outputs.Length; i++)
            target.SetNeuronInput(i, _outputs[i]);
    }

    private int WeightIndex(int source, int target)
        => (target * _sourceStates.Length) + source;

    private static float[] Copy(float[] source)
    {
        var copy = new float[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    private static PlasticityBrainModuleOptions Validate(PlasticityBrainModuleOptions options)
    {
        ValidateNeuronCount(options.SourceNeuronCount, nameof(options.SourceNeuronCount));
        ValidateNeuronCount(options.TargetNeuronCount, nameof(options.TargetNeuronCount));
        ValidateStateVariable(options.SourceStateVariable, nameof(options.SourceStateVariable));
        ValidateStateVariable(options.TargetStateVariable, nameof(options.TargetStateVariable));
        ValidateFinite(options.LearningRate, nameof(options.LearningRate));
        ValidateFinite(options.WeightDecayRate, nameof(options.WeightDecayRate));
        ValidateFinite(options.MinWeight, nameof(options.MinWeight));
        ValidateFinite(options.MaxWeight, nameof(options.MaxWeight));
        ValidateFinite(options.OutputGain, nameof(options.OutputGain));

        if (options.WeightDecayRate < 0.0f || options.WeightDecayRate > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(options.WeightDecayRate));
        if (options.MinWeight > options.MaxWeight)
            throw new ArgumentException("Minimum weight cannot be greater than maximum weight.", nameof(options));

        return options;
    }

    private static void ValidateNeuronCount(int count, string parameterName)
    {
        if (count < 0 || count > MaxTrackedNeurons)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private static void ValidateStateVariable(int stateVariable, string parameterName)
    {
        if ((uint)stateVariable >= BrainConst.NumSVRuleVariables)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private static void ValidateFinite(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
