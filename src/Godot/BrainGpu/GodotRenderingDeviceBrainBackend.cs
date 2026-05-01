using System;
using System.Buffers.Binary;
using System.Collections.Generic;
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
    private const int PushConstantInts = 4;

    private readonly RenderingDevice _device;
    private readonly Rid _shader;
    private readonly Rid _pipeline;
    private bool _disposed;

    private GodotRenderingDeviceBrainBackend(RenderingDevice device, Rid shader, Rid pipeline)
    {
        _device = device;
        _shader = shader;
        _pipeline = pipeline;
    }

    public string Name => "godot renderingdevice lobe gpu";
    public BrainExecutionBackendKind Kind => BrainExecutionBackendKind.Gpu;
    public bool IsAvailable => !_disposed && _pipeline.IsValid && _device.ComputePipelineIsValid(_pipeline);
    public int AcceleratedLobesLastTick { get; private set; }
    public int CpuLobesLastTick { get; private set; }
    public string AcceleratedLobeTokensLastTick { get; private set; } = "";
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

            var source = new RDShaderSource
            {
                Language = RenderingDevice.ShaderLanguage.Glsl,
                SourceCompute = LobeComputeShaderSource,
            };
            RDShaderSpirV spirv = device.ShaderCompileSpirVFromSource(source, false);
            if (!string.IsNullOrWhiteSpace(spirv.CompileErrorCompute))
            {
                reason = spirv.CompileErrorCompute;
                device.Free();
                return null;
            }

            Rid shader = device.ShaderCreateFromSpirV(spirv);
            if (!shader.IsValid)
            {
                reason = "RenderingDevice could not create the lobe compute shader.";
                device.Free();
                return null;
            }

            Rid pipeline = device.ComputePipelineCreate(shader, new global::Godot.Collections.Array<RDPipelineSpecializationConstant>());
            if (!pipeline.IsValid || !device.ComputePipelineIsValid(pipeline))
            {
                reason = "RenderingDevice could not create the lobe compute pipeline.";
                device.FreeRid(shader);
                device.Free();
                return null;
            }

            reason = null;
            return new GodotRenderingDeviceBrainBackend(device, shader, pipeline);
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            device?.Free();
            return null;
        }
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
        CpuLobesLastTick = 0;
        LastShadowValidationFailure = null;
        var acceleratedTokens = new List<string>();
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
                tract.DoUpdate(context.Trace, context.Tick);
            }
            else
            {
                component.DoUpdate();
            }
        }

        foreach (IBrainModule module in context.Modules)
            module.Update(context.Brain);

        AcceleratedLobeTokensLastTick = string.Join(",", acceleratedTokens.Distinct());
    }

    public void UpdateCpuAuthoritativeShadow(BrainExecutionContext context)
    {
        AcceleratedLobesLastTick = 0;
        CpuLobesLastTick = 0;
        LastShadowValidationFailure = null;
        var acceleratedTokens = new List<string>();
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
                tract.DoUpdate(context.Trace, context.Tick);
            }
            else
            {
                component.DoUpdate();
            }
        }

        foreach (IBrainModule module in context.Modules)
            module.Update(context.Brain);

        AcceleratedLobeTokensLastTick = string.Join(",", acceleratedTokens.Distinct());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_pipeline.IsValid)
            _device.FreeRid(_pipeline);
        if (_shader.IsValid)
            _device.FreeRid(_shader);
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

        Rid neuronBuffer = default;
        Rid inputBuffer = default;
        Rid initRuleBuffer = default;
        Rid updateRuleBuffer = default;
        Rid chemicalBuffer = default;
        Rid resultBuffer = default;
        Rid invalidBuffer = default;
        Rid uniformSet = default;

        try
        {
            RenderingDevice.StorageBufferUsage storageUsage = (RenderingDevice.StorageBufferUsage)0;
            neuronBuffer = _device.StorageBufferCreate((uint)neuronBytes.Length, neuronBytes, storageUsage);
            inputBuffer = _device.StorageBufferCreate((uint)inputBytes.Length, inputBytes, storageUsage);
            initRuleBuffer = _device.StorageBufferCreate((uint)initRuleBytes.Length, initRuleBytes, storageUsage);
            updateRuleBuffer = _device.StorageBufferCreate((uint)updateRuleBytes.Length, updateRuleBytes, storageUsage);
            chemicalBuffer = _device.StorageBufferCreate((uint)chemicalBytes.Length, chemicalBytes, storageUsage);
            resultBuffer = _device.StorageBufferCreate((uint)resultBytes.Length, resultBytes, storageUsage);
            invalidBuffer = _device.StorageBufferCreate((uint)invalidBytes.Length, invalidBytes, storageUsage);

            uniformSet = _device.UniformSetCreate(new global::Godot.Collections.Array<RDUniform>
            {
                StorageUniform(0, neuronBuffer),
                StorageUniform(1, inputBuffer),
                StorageUniform(2, initRuleBuffer),
                StorageUniform(3, updateRuleBuffer),
                StorageUniform(4, chemicalBuffer),
                StorageUniform(5, resultBuffer),
                StorageUniform(6, invalidBuffer),
            }, _shader, 0);

            if (!uniformSet.IsValid || !_device.UniformSetIsValid(uniformSet))
                throw new InvalidOperationException("RenderingDevice could not create the lobe compute uniform set.");

            byte[] pushConstants = IntsToBytes(new[]
            {
                state.NeuronCount,
                state.RunInitRuleAlways ? 1 : 0,
                chemicals.Length,
                0,
            });

            long computeList = _device.ComputeListBegin();
            _device.ComputeListBindComputePipeline(computeList, _pipeline);
            _device.ComputeListBindUniformSet(computeList, uniformSet, 0);
            _device.ComputeListSetPushConstant(computeList, pushConstants, (uint)(PushConstantInts * IntBytes));
            _device.ComputeListDispatch(computeList, 1, 1, 1);
            _device.ComputeListEnd();
            _device.Submit();
            _device.Sync();

            byte[] updatedNeuronBytes = _device.BufferGetData(neuronBuffer, 0, (uint)neuronBytes.Length);
            byte[] updatedInvalidBytes = _device.BufferGetData(invalidBuffer, 0, (uint)invalidBytes.Length);
            byte[] updatedResultBytes = _device.BufferGetData(resultBuffer, 0, (uint)resultBytes.Length);
            float[] updatedStates = BytesToFloats(updatedNeuronBytes);
            float[] updatedInvalid = BytesToFloats(updatedInvalidBytes);
            int[] result = BytesToInts(updatedResultBytes);

            Array.Copy(updatedInvalid, SVRule.InvalidVariables, Math.Min(updatedInvalid.Length, SVRule.InvalidVariables.Length));
            lobe.ApplyAcceleratorResult(updatedStates, result[0]);
        }
        finally
        {
            FreeRid(uniformSet);
            FreeRid(invalidBuffer);
            FreeRid(resultBuffer);
            FreeRid(chemicalBuffer);
            FreeRid(updateRuleBuffer);
            FreeRid(initRuleBuffer);
            FreeRid(inputBuffer);
            FreeRid(neuronBuffer);
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

            LastShadowValidationFailure ??= $"{Brain.TokenToString(lobe.Token)} lobe GPU output differed from CPU.";
        }
        catch (Exception ex)
        {
            RestoreLobe(lobe, before, scratchBefore);
            lobe.DoUpdate();
            LastShadowValidationFailure ??= $"{Brain.TokenToString(lobe.Token)} lobe GPU execution failed: {ex.Message}";
        }
    }

    private static bool LobeStatesEquivalent(LobeAcceleratorState expected, LobeAcceleratorState actual)
        => expected.WinningNeuronId == actual.WinningNeuronId
           && FloatArraysEquivalent(expected.NeuronStates, actual.NeuronStates);

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

    private void FreeRid(Rid rid)
    {
        if (rid.IsValid)
            _device.FreeRid(rid);
    }

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

    private const string LobeComputeShaderSource = """
#version 450

layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

struct RuleEntry {
    int op;
    int operand;
    int array_index;
    float value;
};

layout(set = 0, binding = 0, std430) restrict buffer NeuronBuffer {
    float neuron_states[];
};

layout(set = 0, binding = 1, std430) restrict readonly buffer InputBuffer {
    float neuron_inputs[];
};

layout(set = 0, binding = 2, std430) restrict readonly buffer InitRuleBuffer {
    RuleEntry init_rule[];
};

layout(set = 0, binding = 3, std430) restrict readonly buffer UpdateRuleBuffer {
    RuleEntry update_rule[];
};

layout(set = 0, binding = 4, std430) restrict readonly buffer ChemicalBuffer {
    float chemicals[];
};

layout(set = 0, binding = 5, std430) restrict buffer ResultBuffer {
    int result[];
};

layout(set = 0, binding = 6, std430) restrict buffer InvalidBuffer {
    float invalid_vars[];
};

layout(push_constant, std430) uniform Params {
    int neuron_count;
    int run_init;
    int chemical_count;
    int reserved;
} params;

float bound01(float v) {
    return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
}

float bound_mp1(float v) {
    return v < -1.0 ? -1.0 : (v > 1.0 ? 1.0 : v);
}

int to_int(float v) {
    return int(v * 248.0);
}

int var_index(int i) {
    int m = i % 8;
    return m < 0 ? m + 8 : m;
}

float get_neuron(int neuron_id, int variable) {
    return neuron_states[neuron_id * 8 + var_index(variable)];
}

void set_neuron(int neuron_id, int variable, float value) {
    neuron_states[neuron_id * 8 + var_index(variable)] = value;
}

float get_spare(int spare_index, int variable, inout float dummy_spare[8]) {
    int idx = var_index(variable);
    return spare_index >= 0 ? get_neuron(spare_index, idx) : dummy_spare[idx];
}

void set_spare(int spare_index, int variable, float value, inout float dummy_spare[8]) {
    int idx = var_index(variable);
    if (spare_index >= 0) {
        set_neuron(spare_index, idx, value);
    } else {
        dummy_spare[idx] = value;
    }
}

RuleEntry get_rule_entry(int rule_kind, int index) {
    return rule_kind == 0 ? init_rule[index] : update_rule[index];
}

float read_operand(RuleEntry entry, float accumulator, int neuron_id, int spare_index, inout float dummy_spare[8]) {
    int ai = entry.array_index;
    int idx = var_index(ai);
    if (entry.operand == 0) return accumulator;
    if (entry.operand == 1) return invalid_vars[idx];
    if (entry.operand == 2) return invalid_vars[idx];
    if (entry.operand == 3) return get_neuron(neuron_id, idx);
    if (entry.operand == 4) return get_spare(spare_index, idx, dummy_spare);
    if (entry.operand == 5) return 0.0;
    if (entry.operand == 6) return chemicals[(ai + neuron_id) % params.chemical_count];
    if (entry.operand == 7) return chemicals[ai % params.chemical_count];
    if (entry.operand == 8) return chemicals[(ai + neuron_id) % params.chemical_count];
    if (entry.operand == 9) return 0.0;
    if (entry.operand == 10) return 1.0;
    if (entry.operand == 11) return entry.value;
    if (entry.operand == 12) return -entry.value;
    if (entry.operand == 13) return entry.value * 10.0;
    if (entry.operand == 14) return entry.value / 10.0;
    if (entry.operand == 15) return float(int(entry.value * 248.0));
    return 0.0;
}

void write_dest(RuleEntry entry, float value, int neuron_id, int spare_index, inout float dummy_spare[8]) {
    int idx = var_index(entry.array_index);
    if (entry.operand == 1 || entry.operand == 2) {
        invalid_vars[idx] = value;
    } else if (entry.operand == 3) {
        set_neuron(neuron_id, idx, value);
    } else if (entry.operand == 4) {
        set_spare(spare_index, idx, value, dummy_spare);
    }
}

bool is_no_operand_op(int op) {
    return op == 0 || op == 30 || op == 31 || op == 42;
}

bool is_write_op(int op) {
    return op == 1 || op == 2 || op == 34 || op == 35 || op == 45;
}

void goto_forward(inout int i, float operand) {
    int target = to_int(operand) - 1;
    if (target > i && target <= 16) {
        i = target - 1;
    }
}

int process_rule(int rule_kind, int neuron_id, inout int spare_index, inout float dummy_spare[8]) {
    float accumulator = invalid_vars[0];
    float tend_rate = 0.0;
    int rc = 0;

    for (int i = 0; i < 16; i++) {
        RuleEntry entry = get_rule_entry(rule_kind, i);
        int op = entry.op;

        if (is_no_operand_op(op)) {
            if (op == 0) return rc;
            if (op == 30) {
            } else if (op == 31) {
                rc = 1;
            } else if (op == 42) {
                if (get_neuron(neuron_id, 0) >= get_spare(spare_index, 0, dummy_spare)) {
                    set_spare(spare_index, 2, 0.0, dummy_spare);
                    set_neuron(neuron_id, 2, get_neuron(neuron_id, 0));
                    rc = 1;
                }
            }
            continue;
        }

        if (is_write_op(op)) {
            int idx = var_index(entry.array_index);
            float current = 0.0;
            if (entry.operand == 1 || entry.operand == 2) current = invalid_vars[idx];
            else if (entry.operand == 3) current = get_neuron(neuron_id, idx);
            else if (entry.operand == 4) current = get_spare(spare_index, idx, dummy_spare);

            if (op == 2) write_dest(entry, bound_mp1(accumulator), neuron_id, spare_index, dummy_spare);
            else if (op == 34) write_dest(entry, bound_mp1(accumulator + current), neuron_id, spare_index, dummy_spare);
            else if (op == 1) write_dest(entry, 0.0, neuron_id, spare_index, dummy_spare);
            else if (op == 35) write_dest(entry, bound_mp1(accumulator * (1.0 - tend_rate) + current * tend_rate), neuron_id, spare_index, dummy_spare);
            else if (op == 45) write_dest(entry, bound01(abs(accumulator)), neuron_id, spare_index, dummy_spare);
            continue;
        }

        float operand = entry.operand == 0
            ? accumulator
            : read_operand(entry, accumulator, neuron_id, spare_index, dummy_spare);

        if (op == 3) accumulator = operand;
        else if (op == 4) { if (accumulator != operand) i++; }
        else if (op == 5) { if (accumulator == operand) i++; }
        else if (op == 6) { if (accumulator <= operand) i++; }
        else if (op == 7) { if (accumulator >= operand) i++; }
        else if (op == 8) { if (accumulator < operand) i++; }
        else if (op == 9) { if (accumulator > operand) i++; }
        else if (op == 10) { if (operand != 0.0) i++; }
        else if (op == 11) { if (operand == 0.0) i++; }
        else if (op == 12) { if (operand <= 0.0) i++; }
        else if (op == 13) { if (operand >= 0.0) i++; }
        else if (op == 14) { if (operand < 0.0) i++; }
        else if (op == 15) { if (operand > 0.0) i++; }
        else if (op == 46) { if (operand == 0.0) return rc; }
        else if (op == 47) { if (operand != 0.0) return rc; }
        else if (op == 53) { if (accumulator < operand) return rc; }
        else if (op == 54) { if (accumulator > operand) return rc; }
        else if (op == 55) { if (accumulator <= operand) return rc; }
        else if (op == 56) { if (accumulator >= operand) return rc; }
        else if (op == 48) { if (accumulator == 0.0) goto_forward(i, operand); }
        else if (op == 49) { if (accumulator != 0.0) goto_forward(i, operand); }
        else if (op == 67) { if (accumulator < 0.0) goto_forward(i, operand); }
        else if (op == 68) { if (accumulator > 0.0) goto_forward(i, operand); }
        else if (op == 52) { goto_forward(i, operand); }
        else if (op == 16) accumulator += operand;
        else if (op == 17) accumulator -= operand;
        else if (op == 18) accumulator = operand - accumulator;
        else if (op == 19) accumulator *= operand;
        else if (op == 20) { if (operand != 0.0) accumulator /= operand; }
        else if (op == 21) { if (accumulator != 0.0) accumulator = operand / accumulator; }
        else if (op == 23) { if (operand > accumulator) accumulator = operand; }
        else if (op == 22) { if (operand < accumulator) accumulator = operand; }
        else if (op == 24) tend_rate = abs(operand);
        else if (op == 25) accumulator = accumulator * (1.0 - tend_rate) + operand * tend_rate;
        else if (op == 26) accumulator = -operand;
        else if (op == 27) accumulator = abs(operand);
        else if (op == 28) accumulator = abs(accumulator - operand);
        else if (op == 29) accumulator = operand - accumulator;
        else if (op == 32) accumulator = bound01(operand);
        else if (op == 33) accumulator = bound_mp1(operand);
        else if (op == 36) { if (get_neuron(neuron_id, 1) < operand) set_neuron(neuron_id, 1, 0.0); }
        else if (op == 37) tend_rate = operand;
        else if (op == 38) set_neuron(neuron_id, 1, get_neuron(neuron_id, 1) * (1.0 - tend_rate) + operand * tend_rate);
        else if (op == 39) set_neuron(neuron_id, 1, get_neuron(neuron_id, 1) * operand);
        else if (op == 40) set_neuron(neuron_id, 0, get_neuron(neuron_id, 1) * (1.0 - operand) + get_neuron(neuron_id, 0) * operand);
        else if (op == 41) set_neuron(neuron_id, 0, get_neuron(neuron_id, 0));
        else if (op == 50) { if (operand != 0.0) { accumulator /= operand; set_neuron(neuron_id, 1, bound_mp1(get_neuron(neuron_id, 1) + accumulator)); } }
        else if (op == 51) { accumulator *= operand; set_neuron(neuron_id, 1, bound_mp1(get_neuron(neuron_id, 1) + accumulator)); }
        else if (op == 63) set_neuron(neuron_id, 4, get_neuron(neuron_id, var_index(to_int(operand))));
        else if (op == 64) set_neuron(neuron_id, var_index(to_int(operand)), get_neuron(neuron_id, 4));
        else if (op == 65) set_spare(spare_index, 4, get_spare(spare_index, var_index(to_int(operand)), dummy_spare), dummy_spare);
        else if (op == 66) set_spare(spare_index, var_index(to_int(operand)), get_spare(spare_index, 4, dummy_spare), dummy_spare);
    }

    return rc;
}

void main() {
    if (gl_GlobalInvocationID.x != 0) {
        return;
    }

    float dummy_spare[8];
    for (int i = 0; i < 8; i++) {
        dummy_spare[i] = 0.0;
    }

    int spare_index = -1;
    int winning = 0;

    for (int neuron_id = 0; neuron_id < params.neuron_count; neuron_id++) {
        invalid_vars[0] = neuron_inputs[neuron_id];

        bool flag_as_spare = false;
        if (params.run_init != 0) {
            if (process_rule(0, neuron_id, spare_index, dummy_spare) == 1) {
                flag_as_spare = true;
            }
        }

        if (process_rule(1, neuron_id, spare_index, dummy_spare) == 1) {
            flag_as_spare = true;
        }

        if (flag_as_spare) {
            spare_index = neuron_id;
            winning = neuron_id;
        }
    }

    result[0] = spare_index >= 0 ? winning : 0;
}
""";
}
