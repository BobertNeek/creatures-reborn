using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using CreaturesReborn.Sim.Brain;
using Godot;

namespace CreaturesReborn.Godot.BrainGpu;

public sealed class GodotRenderingDeviceBrainBackend : ICpuAuthoritativeShadowBrainBackend, IDisposable
{
    private const int FloatBytes = sizeof(float);
    private const int IntBytes = sizeof(int);
    private const int RuleEntryBytes = 16;
    private const int ResultInts = 4;
    private const int LobePushConstantInts = 4;
    private const int TractPushConstantInts = 8;

    private readonly RenderingDevice _device;
    private readonly Rid _lobeShader;
    private readonly Rid _lobePipeline;
    private readonly Rid _tractShader;
    private readonly Rid _tractPipeline;
    private readonly BrainGpuStorageBufferCache _buffers;
    private int _dispatchesThisTick;
    private long _dispatchTicksThisTick;
    private long _readbackTicksThisTick;
    private bool _disposed;

    private GodotRenderingDeviceBrainBackend(
        RenderingDevice device,
        Rid lobeShader,
        Rid lobePipeline,
        Rid tractShader,
        Rid tractPipeline)
    {
        _device = device;
        _lobeShader = lobeShader;
        _lobePipeline = lobePipeline;
        _tractShader = tractShader;
        _tractPipeline = tractPipeline;
        _buffers = new BrainGpuStorageBufferCache(device);
    }

    public string Name => "godot renderingdevice brain gpu";
    public BrainExecutionBackendKind Kind => BrainExecutionBackendKind.Gpu;
    public bool IsAvailable => !_disposed
                               && _lobePipeline.IsValid
                               && _tractPipeline.IsValid
                               && _device.ComputePipelineIsValid(_lobePipeline)
                               && _device.ComputePipelineIsValid(_tractPipeline);
    public int AcceleratedLobesLastTick { get; private set; }
    public int AcceleratedTractsLastTick { get; private set; }
    public int CpuLobesLastTick { get; private set; }
    public int CpuTractsLastTick { get; private set; }
    public string AcceleratedLobeTokensLastTick { get; private set; } = "";
    public string AcceleratedTractsLastTickDescription { get; private set; } = "";
    public BrainGpuCapabilityReport LastCapabilityReport { get; private set; } = BrainGpuCapabilityReport.Empty;
    public BrainGpuDispatchMetrics LastDispatchMetrics { get; private set; } = BrainGpuDispatchMetrics.Empty;
    public string? LastShadowValidationFailure { get; private set; }

    public static GodotRenderingDeviceBrainBackend? TryCreate(out string? reason)
    {
        if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
        {
            reason = "RenderingDevice compute is unavailable with the headless renderer.";
            return null;
        }

        RenderingDevice? device = null;
        try
        {
            device = RenderingServer.CreateLocalRenderingDevice();
            if (device == null)
            {
                reason = "RenderingDevice is unavailable for the current renderer.";
                return null;
            }

            if (!CreateComputePipeline(device, BrainGpuShaderLibrary.LobeComputeShaderSource, "lobe", out Rid lobeShader, out Rid lobePipeline, out reason))
            {
                device.Free();
                return null;
            }

            if (!CreateComputePipeline(device, BrainGpuShaderLibrary.TractComputeShaderSource, "tract", out Rid tractShader, out Rid tractPipeline, out reason))
            {
                device.FreeRid(lobePipeline);
                device.FreeRid(lobeShader);
                device.Free();
                return null;
            }

            reason = null;
            return new GodotRenderingDeviceBrainBackend(device, lobeShader, lobePipeline, tractShader, tractPipeline);
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            device?.Free();
            return null;
        }
    }

    private static bool CreateComputePipeline(
        RenderingDevice device,
        string sourceText,
        string label,
        out Rid shader,
        out Rid pipeline,
        out string? reason)
    {
        shader = default;
        pipeline = default;
        var source = new RDShaderSource
        {
            Language = RenderingDevice.ShaderLanguage.Glsl,
            SourceCompute = sourceText,
        };
        RDShaderSpirV spirv = device.ShaderCompileSpirVFromSource(source, false);
        if (!string.IsNullOrWhiteSpace(spirv.CompileErrorCompute))
        {
            reason = $"{label} shader compile failed: {spirv.CompileErrorCompute}";
            return false;
        }

        shader = device.ShaderCreateFromSpirV(spirv);
        if (!shader.IsValid)
        {
            reason = $"RenderingDevice could not create the {label} compute shader.";
            return false;
        }

        pipeline = device.ComputePipelineCreate(shader, new global::Godot.Collections.Array<RDPipelineSpecializationConstant>());
        if (!pipeline.IsValid || !device.ComputePipelineIsValid(pipeline))
        {
            reason = $"RenderingDevice could not create the {label} compute pipeline.";
            device.FreeRid(shader);
            shader = default;
            return false;
        }

        reason = null;
        return true;
    }

    public bool Supports(BrainExecutionContext context, out string? reason)
    {
        if (!IsAvailable)
        {
            reason = "RenderingDevice compute pipeline is unavailable.";
            return false;
        }

        reason = null;
        return true;
    }

    public void Update(BrainExecutionContext context)
    {
        AcceleratedLobesLastTick = 0;
        AcceleratedTractsLastTick = 0;
        CpuLobesLastTick = 0;
        CpuTractsLastTick = 0;
        LastShadowValidationFailure = null;
        ResetTickMetrics();
        BrainGpuCapabilityReport capability = BrainGpuCapabilityReport.FromContext(context);
        var acceleratedTokens = new List<string>();
        var acceleratedTracts = new List<string>();
        float[] chemicals = context.Brain.CreateChemicalSnapshot();
        if (chemicals.Length == 0)
            chemicals = new float[1];

        foreach (BrainComponent component in context.Components)
        {
            if (component is Lobe lobe)
            {
                if (context.IsLobeTokenShadowed(lobe.Token))
                    continue;
                if (lobe.CanRunOnAccelerator(out _))
                {
                    RunLobe(lobe, chemicals);
                    AcceleratedLobesLastTick++;
                    acceleratedTokens.Add(Brain.TokenToString(lobe.Token));
                }
                else
                {
                    lobe.DoUpdate();
                    CpuLobesLastTick++;
                }
            }
            else if (component is Tract tract)
            {
                if (context.Trace == null && tract.CanRunOnAccelerator(out _))
                {
                    RunTract(tract, chemicals);
                    AcceleratedTractsLastTick++;
                    TractAcceleratorState state = tract.CreateAcceleratorState();
                    acceleratedTracts.Add($"{Brain.TokenToString(state.SourceToken)}->{Brain.TokenToString(state.DestinationToken)}");
                }
                else
                {
                    tract.DoUpdate(context.Trace, context.Tick);
                    CpuTractsLastTick++;
                }
            }
            else
            {
                component.DoUpdate();
            }
        }

        foreach (IBrainModule module in context.Modules)
            module.Update(context.Brain);

        AcceleratedLobeTokensLastTick = string.Join(",", acceleratedTokens.Distinct());
        AcceleratedTractsLastTickDescription = string.Join(",", acceleratedTracts.Distinct());
        LastCapabilityReport = capability.WithAcceleratedCounts(AcceleratedLobesLastTick, AcceleratedTractsLastTick);
        CompleteTickMetrics();
    }

    public void UpdateCpuAuthoritativeShadow(BrainExecutionContext context)
    {
        AcceleratedLobesLastTick = 0;
        AcceleratedTractsLastTick = 0;
        CpuLobesLastTick = 0;
        CpuTractsLastTick = 0;
        LastShadowValidationFailure = null;
        ResetTickMetrics();
        BrainGpuCapabilityReport capability = BrainGpuCapabilityReport.FromContext(context);
        var acceleratedTokens = new List<string>();
        var acceleratedTracts = new List<string>();
        float[] chemicals = context.Brain.CreateChemicalSnapshot();
        if (chemicals.Length == 0)
            chemicals = new float[1];

        foreach (BrainComponent component in context.Components)
        {
            if (component is Lobe lobe)
            {
                if (context.IsLobeTokenShadowed(lobe.Token))
                    continue;

                if (lobe.CanRunOnAccelerator(out _))
                    ShadowValidateLobe(lobe, chemicals, acceleratedTokens);
                else
                {
                    lobe.DoUpdate();
                    CpuLobesLastTick++;
                }
            }
            else if (component is Tract tract)
            {
                if (context.Trace == null && tract.CanRunOnAccelerator(out _))
                    ShadowValidateTract(tract, chemicals, context.Tick, acceleratedTracts);
                else
                {
                    tract.DoUpdate(context.Trace, context.Tick);
                    CpuTractsLastTick++;
                }
            }
            else
            {
                component.DoUpdate();
            }
        }

        foreach (IBrainModule module in context.Modules)
            module.Update(context.Brain);

        AcceleratedLobeTokensLastTick = string.Join(",", acceleratedTokens.Distinct());
        AcceleratedTractsLastTickDescription = string.Join(",", acceleratedTracts.Distinct());
        LastCapabilityReport = capability.WithAcceleratedCounts(AcceleratedLobesLastTick, AcceleratedTractsLastTick);
        CompleteTickMetrics();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_tractPipeline.IsValid)
            _device.FreeRid(_tractPipeline);
        if (_tractShader.IsValid)
            _device.FreeRid(_tractShader);
        if (_lobePipeline.IsValid)
            _device.FreeRid(_lobePipeline);
        if (_lobeShader.IsValid)
            _device.FreeRid(_lobeShader);
        _buffers.Dispose();
        _device.Free();
        _disposed = true;
    }

    private void RunLobe(Lobe lobe, float[] chemicals)
    {
        LobeAcceleratorState state = lobe.CreateAcceleratorState();
        state.ValidateResult(state.NeuronStates);

        byte[] neuronBytes = FloatsToBytes(state.NeuronStates);
        byte[] inputBytes = FloatsToBytes(state.NeuronInputs);
        byte[] initRuleBytes = RuleEntriesToBytes(state.InitRule);
        byte[] updateRuleBytes = RuleEntriesToBytes(state.UpdateRule);
        byte[] chemicalBytes = FloatsToBytes(chemicals);
        byte[] invalidBytes = FloatsToBytes(SVRule.InvalidVariables);
        byte[] resultBytes = IntsToBytes(new int[ResultInts]);

        Rid uniformSet = default;

        try
        {
            string prefix = $"lobe:{lobe.IdInList}:{lobe.Token}";
            Rid neuronBuffer = _buffers.Upload($"{prefix}:neurons", neuronBytes);
            Rid inputBuffer = _buffers.Upload($"{prefix}:inputs", inputBytes);
            Rid initRuleBuffer = _buffers.Upload($"{prefix}:init-rule", initRuleBytes);
            Rid updateRuleBuffer = _buffers.Upload($"{prefix}:update-rule", updateRuleBytes);
            Rid chemicalBuffer = _buffers.Upload("brain:chemicals", chemicalBytes);
            Rid resultBuffer = _buffers.Upload($"{prefix}:result", resultBytes);
            Rid invalidBuffer = _buffers.Upload($"{prefix}:invalid", invalidBytes);

            uniformSet = _device.UniformSetCreate(new global::Godot.Collections.Array<RDUniform>
            {
                StorageUniform(0, neuronBuffer),
                StorageUniform(1, inputBuffer),
                StorageUniform(2, initRuleBuffer),
                StorageUniform(3, updateRuleBuffer),
                StorageUniform(4, chemicalBuffer),
                StorageUniform(5, resultBuffer),
                StorageUniform(6, invalidBuffer),
            }, _lobeShader, 0);

            if (!uniformSet.IsValid || !_device.UniformSetIsValid(uniformSet))
                throw new InvalidOperationException("RenderingDevice could not create the lobe compute uniform set.");

            byte[] pushConstants = IntsToBytes(new[]
            {
                state.NeuronCount,
                state.RunInitRuleAlways ? 1 : 0,
                chemicals.Length,
                0,
            });

            long dispatchStarted = Stopwatch.GetTimestamp();
            long computeList = _device.ComputeListBegin();
            _device.ComputeListBindComputePipeline(computeList, _lobePipeline);
            _device.ComputeListBindUniformSet(computeList, uniformSet, 0);
            _device.ComputeListSetPushConstant(computeList, pushConstants, (uint)(LobePushConstantInts * IntBytes));
            _device.ComputeListDispatch(computeList, 1, 1, 1);
            _device.ComputeListEnd();
            _device.Submit();
            _device.Sync();
            _dispatchTicksThisTick += Stopwatch.GetTimestamp() - dispatchStarted;
            _dispatchesThisTick++;

            long readbackStarted = Stopwatch.GetTimestamp();
            byte[] updatedNeuronBytes = _device.BufferGetData(neuronBuffer, 0, (uint)neuronBytes.Length);
            byte[] updatedInvalidBytes = _device.BufferGetData(invalidBuffer, 0, (uint)invalidBytes.Length);
            byte[] updatedResultBytes = _device.BufferGetData(resultBuffer, 0, (uint)resultBytes.Length);
            _readbackTicksThisTick += Stopwatch.GetTimestamp() - readbackStarted;
            float[] updatedStates = BytesToFloats(updatedNeuronBytes);
            float[] updatedInvalid = BytesToFloats(updatedInvalidBytes);
            int[] result = BytesToInts(updatedResultBytes);

            Array.Copy(updatedInvalid, SVRule.InvalidVariables, Math.Min(updatedInvalid.Length, SVRule.InvalidVariables.Length));
            lobe.ApplyAcceleratorResult(updatedStates, result[0]);
        }
        finally
        {
            FreeRid(uniformSet);
        }
    }

    private void RunTract(Tract tract, float[] chemicals)
    {
        TractAcceleratorState state = tract.CreateAcceleratorState();
        state.ValidateResult(state.SourceNeuronStates, state.DestinationNeuronStates, state.DendriteWeights);

        byte[] sourceBytes = FloatsToBytes(state.SourceNeuronStates);
        byte[] destinationBytes = FloatsToBytes(state.DestinationNeuronStates);
        byte[] weightBytes = FloatsToBytes(state.DendriteWeights);
        byte[] sourceIdBytes = IntsToBytes(state.SourceNeuronIds);
        byte[] destinationIdBytes = IntsToBytes(state.DestinationNeuronIds);
        byte[] initRuleBytes = RuleEntriesToBytes(state.InitRule);
        byte[] updateRuleBytes = RuleEntriesToBytes(state.UpdateRule);
        byte[] chemicalBytes = FloatsToBytes(chemicals);
        byte[] resultBytes = FloatsToBytes(new[] { state.STtoLTRate });

        Rid uniformSet = default;

        try
        {
            string prefix = $"tract:{tract.IdInList}:{state.SourceToken}:{state.DestinationToken}";
            Rid sourceBuffer = _buffers.Upload($"{prefix}:source", sourceBytes);
            Rid destinationBuffer = _buffers.Upload($"{prefix}:destination", destinationBytes);
            Rid weightBuffer = _buffers.Upload($"{prefix}:weights", weightBytes);
            Rid sourceIdBuffer = _buffers.Upload($"{prefix}:source-ids", sourceIdBytes);
            Rid destinationIdBuffer = _buffers.Upload($"{prefix}:destination-ids", destinationIdBytes);
            Rid initRuleBuffer = _buffers.Upload($"{prefix}:init-rule", initRuleBytes);
            Rid updateRuleBuffer = _buffers.Upload($"{prefix}:update-rule", updateRuleBytes);
            Rid chemicalBuffer = _buffers.Upload("brain:chemicals", chemicalBytes);
            Rid resultBuffer = _buffers.Upload($"{prefix}:result", resultBytes);

            uniformSet = _device.UniformSetCreate(new global::Godot.Collections.Array<RDUniform>
            {
                StorageUniform(0, sourceBuffer),
                StorageUniform(1, destinationBuffer),
                StorageUniform(2, weightBuffer),
                StorageUniform(3, sourceIdBuffer),
                StorageUniform(4, destinationIdBuffer),
                StorageUniform(5, initRuleBuffer),
                StorageUniform(6, updateRuleBuffer),
                StorageUniform(7, chemicalBuffer),
                StorageUniform(8, resultBuffer),
            }, _tractShader, 0);

            if (!uniformSet.IsValid || !_device.UniformSetIsValid(uniformSet))
                throw new InvalidOperationException("RenderingDevice could not create the tract compute uniform set.");

            byte[] pushConstants = IntsToBytes(new[]
            {
                state.DendriteCount,
                state.SourceNeuronCount,
                state.DestinationNeuronCount,
                chemicals.Length,
                state.RunInitRuleAlways ? 1 : 0,
                state.SourceWinningNeuronId,
                0,
                0,
            });

            long dispatchStarted = Stopwatch.GetTimestamp();
            long computeList = _device.ComputeListBegin();
            _device.ComputeListBindComputePipeline(computeList, _tractPipeline);
            _device.ComputeListBindUniformSet(computeList, uniformSet, 0);
            _device.ComputeListSetPushConstant(computeList, pushConstants, (uint)(TractPushConstantInts * IntBytes));
            _device.ComputeListDispatch(computeList, 1, 1, 1);
            _device.ComputeListEnd();
            _device.Submit();
            _device.Sync();
            _dispatchTicksThisTick += Stopwatch.GetTimestamp() - dispatchStarted;
            _dispatchesThisTick++;

            long readbackStarted = Stopwatch.GetTimestamp();
            float[] updatedSource = BytesToFloats(_device.BufferGetData(sourceBuffer, 0, (uint)sourceBytes.Length));
            float[] updatedDestination = BytesToFloats(_device.BufferGetData(destinationBuffer, 0, (uint)destinationBytes.Length));
            float[] updatedWeights = BytesToFloats(_device.BufferGetData(weightBuffer, 0, (uint)weightBytes.Length));
            float[] updatedResult = BytesToFloats(_device.BufferGetData(resultBuffer, 0, (uint)resultBytes.Length));
            _readbackTicksThisTick += Stopwatch.GetTimestamp() - readbackStarted;

            tract.ApplyAcceleratorResult(updatedSource, updatedDestination, updatedWeights, updatedResult.Length > 0 ? updatedResult[0] : state.STtoLTRate);
        }
        finally
        {
            FreeRid(uniformSet);
        }
    }

    private void ShadowValidateLobe(Lobe lobe, float[] chemicals, List<string> acceleratedTokens)
    {
        LobeAcceleratorState before = lobe.CreateAcceleratorState();
        float[] scratchBefore = (float[])SVRule.InvalidVariables.Clone();

        try
        {
            RunLobe(lobe, chemicals);
            LobeAcceleratorState gpu = lobe.CreateAcceleratorState();
            float[] gpuScratch = (float[])SVRule.InvalidVariables.Clone();

            RestoreLobe(lobe, before, scratchBefore);
            lobe.DoUpdate();
            LobeAcceleratorState cpu = lobe.CreateAcceleratorState();
            float[] cpuScratch = (float[])SVRule.InvalidVariables.Clone();

            if (LobeStatesEquivalent(cpu, gpu)
                && FloatArraysEquivalent(cpuScratch, gpuScratch))
            {
                AcceleratedLobesLastTick++;
                acceleratedTokens.Add(Brain.TokenToString(lobe.Token));
                return;
            }

            LastShadowValidationFailure ??= $"{Brain.TokenToString(lobe.Token)} lobe GPU output differed from CPU: {DescribeLobeDifference(cpu, gpu, cpuScratch, gpuScratch)}";
        }
        catch (Exception ex)
        {
            RestoreLobe(lobe, before, scratchBefore);
            lobe.DoUpdate();
            LastShadowValidationFailure ??= $"{Brain.TokenToString(lobe.Token)} lobe GPU execution failed: {ex.Message}";
        }
    }

    private void ShadowValidateTract(Tract tract, float[] chemicals, int tick, List<string> acceleratedTracts)
    {
        TractAcceleratorState before = tract.CreateAcceleratorState();
        float[] scratchBefore = (float[])SVRule.InvalidVariables.Clone();
        string description = $"{Brain.TokenToString(before.SourceToken)}->{Brain.TokenToString(before.DestinationToken)}";

        try
        {
            RunTract(tract, chemicals);
            TractAcceleratorState gpu = tract.CreateAcceleratorState();
            float[] gpuScratch = (float[])SVRule.InvalidVariables.Clone();

            RestoreTract(tract, before, scratchBefore);
            tract.DoUpdate(trace: null, tick);
            TractAcceleratorState cpu = tract.CreateAcceleratorState();
            float[] cpuScratch = (float[])SVRule.InvalidVariables.Clone();

            if (TractStatesEquivalent(cpu, gpu)
                && FloatArraysEquivalent(cpuScratch, gpuScratch))
            {
                AcceleratedTractsLastTick++;
                acceleratedTracts.Add(description);
                return;
            }

            LastShadowValidationFailure ??= $"{description} tract GPU output differed from CPU.";
        }
        catch (Exception ex)
        {
            RestoreTract(tract, before, scratchBefore);
            tract.DoUpdate(trace: null, tick);
            LastShadowValidationFailure ??= $"{description} tract GPU execution failed: {ex.Message}";
        }
    }

    private static bool LobeStatesEquivalent(LobeAcceleratorState expected, LobeAcceleratorState actual)
        => expected.WinningNeuronId == actual.WinningNeuronId
           && FloatArraysEquivalent(expected.NeuronStates, actual.NeuronStates);

    private static string DescribeLobeDifference(
        LobeAcceleratorState expected,
        LobeAcceleratorState actual,
        float[] expectedScratch,
        float[] actualScratch)
    {
        if (expected.WinningNeuronId != actual.WinningNeuronId)
        {
            int cpuOffset = expected.WinningNeuronId * BrainConst.NumSVRuleVariables;
            int gpuOffset = actual.WinningNeuronId * BrainConst.NumSVRuleVariables;
            return "winner "
                   + $"CPU={expected.WinningNeuronId} state={ReadStateForDiagnostic(expected.NeuronStates, cpuOffset):R} "
                   + $"GPU={actual.WinningNeuronId} state={ReadStateForDiagnostic(actual.NeuronStates, gpuOffset):R} "
                   + $"CPU-at-GPU={ReadStateForDiagnostic(expected.NeuronStates, gpuOffset):R} "
                   + $"GPU-at-CPU={ReadStateForDiagnostic(actual.NeuronStates, cpuOffset):R}";
        }

        int index = -1;
        float delta = 0.0f;
        for (int i = 0; i < Math.Min(expected.NeuronStates.Length, actual.NeuronStates.Length); i++)
        {
            float current = MathF.Abs(expected.NeuronStates[i] - actual.NeuronStates[i]);
            if (current > delta)
            {
                delta = current;
                index = i;
            }
        }

        if (delta > 0.00001f)
            return $"state index={index} variable={index % BrainConst.NumSVRuleVariables} CPU={expected.NeuronStates[index]:R} GPU={actual.NeuronStates[index]:R} delta={delta:R}";

        for (int i = 0; i < Math.Min(expectedScratch.Length, actualScratch.Length); i++)
        {
            float current = MathF.Abs(expectedScratch[i] - actualScratch[i]);
            if (current > 0.00001f)
                return $"scratch index={i} CPU={expectedScratch[i]:R} GPU={actualScratch[i]:R} delta={current:R}";
        }

        return "state length or scratch length differed";
    }

    private static float ReadStateForDiagnostic(float[] states, int offset)
        => (uint)offset < (uint)states.Length ? states[offset] : float.NaN;

    private static bool TractStatesEquivalent(TractAcceleratorState expected, TractAcceleratorState actual)
        => FloatArraysEquivalent(expected.SourceNeuronStates, actual.SourceNeuronStates)
           && FloatArraysEquivalent(expected.DestinationNeuronStates, actual.DestinationNeuronStates)
           && FloatArraysEquivalent(expected.DendriteWeights, actual.DendriteWeights)
           && MathF.Abs(expected.STtoLTRate - actual.STtoLTRate) <= 0.00001f;

    private static bool FloatArraysEquivalent(float[] expected, float[] actual)
    {
        if (expected.Length != actual.Length)
            return false;
        for (int i = 0; i < expected.Length; i++)
            if (MathF.Abs(expected[i] - actual[i]) > 0.00001f)
                return false;
        return true;
    }

    private static void RestoreLobe(Lobe lobe, LobeAcceleratorState state, float[] scratch)
    {
        lobe.ApplyAcceleratorResult(state.NeuronStates, state.WinningNeuronId);
        lobe.RestoreInputSnapshot(state.NeuronInputs);
        Array.Copy(scratch, SVRule.InvalidVariables, Math.Min(scratch.Length, SVRule.InvalidVariables.Length));
    }

    private static void RestoreTract(Tract tract, TractAcceleratorState state, float[] scratch)
    {
        tract.ApplyAcceleratorResult(
            state.SourceNeuronStates,
            state.DestinationNeuronStates,
            state.DendriteWeights,
            state.STtoLTRate);
        Array.Copy(scratch, SVRule.InvalidVariables, Math.Min(scratch.Length, SVRule.InvalidVariables.Length));
    }

    private void FreeRid(Rid rid)
    {
        if (rid.IsValid)
            _device.FreeRid(rid);
    }

    private void ResetTickMetrics()
    {
        _dispatchesThisTick = 0;
        _dispatchTicksThisTick = 0;
        _readbackTicksThisTick = 0;
        _buffers.ResetTickMetrics();
        LastDispatchMetrics = BrainGpuDispatchMetrics.Empty;
    }

    private void CompleteTickMetrics()
    {
        LastDispatchMetrics = new BrainGpuDispatchMetrics(
            _dispatchesThisTick,
            _buffers.BufferRebuildsThisTick,
            StopwatchTicksToTimeSpan(_dispatchTicksThisTick),
            StopwatchTicksToTimeSpan(_readbackTicksThisTick));
    }

    private static TimeSpan StopwatchTicksToTimeSpan(long ticks)
        => TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);

    private static RDUniform StorageUniform(int binding, Rid rid)
    {
        var uniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = binding,
        };
        uniform.AddId(rid);
        return uniform;
    }

    private static byte[] FloatsToBytes(float[] values)
        => MemoryMarshal.AsBytes(values.AsSpan()).ToArray();

    private static byte[] IntsToBytes(int[] values)
        => MemoryMarshal.AsBytes(values.AsSpan()).ToArray();

    private static float[] BytesToFloats(byte[] bytes)
        => MemoryMarshal.Cast<byte, float>(bytes.AsSpan()).ToArray();

    private static int[] BytesToInts(byte[] bytes)
        => MemoryMarshal.Cast<byte, int>(bytes.AsSpan()).ToArray();

    private static byte[] RuleEntriesToBytes(IReadOnlyList<SVRuleEntrySnapshot> entries)
    {
        var bytes = new byte[entries.Count * RuleEntryBytes];
        for (int i = 0; i < entries.Count; i++)
        {
            int offset = i * RuleEntryBytes;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, IntBytes), (int)entries[i].Operation);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 4, IntBytes), (int)entries[i].Operand);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 8, IntBytes), entries[i].ArrayIndex);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 12, FloatBytes), entries[i].FloatValue);
        }

        return bytes;
    }


}
