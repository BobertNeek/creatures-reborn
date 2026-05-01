using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.Brain;

public enum BrainExecutionMode
{
    CpuOnly = 0,
    GpuShadowValidate = 1,
    GpuPreferred = 2,
}

public enum BrainExecutionBackendKind
{
    Cpu = 0,
    Gpu = 1,
    Other = 2,
}

public sealed record BrainExecutionStatus(
    string BackendName,
    BrainExecutionBackendKind Kind,
    bool UsedGpu,
    string? FallbackReason)
{
    public static BrainExecutionStatus Cpu(string? fallbackReason = null)
        => new(CpuBrainExecutionBackend.Instance.Name, BrainExecutionBackendKind.Cpu, UsedGpu: false, fallbackReason);

    public static BrainExecutionStatus Used(IBrainExecutionBackend backend)
        => new(backend.Name, backend.Kind, UsedGpu: backend.Kind == BrainExecutionBackendKind.Gpu, FallbackReason: null);

    public static BrainExecutionStatus CpuAfterGpuShadow(string? fallbackReason)
        => new(CpuBrainExecutionBackend.Instance.Name, BrainExecutionBackendKind.Cpu, UsedGpu: true, fallbackReason);
}

public interface IBrainExecutionBackend
{
    string Name { get; }
    BrainExecutionBackendKind Kind { get; }
    bool IsAvailable { get; }

    /// <summary>
    /// Return false before mutating brain state when this backend cannot process the
    /// current topology, opcodes, RNG needs, or trace requirements exactly.
    /// </summary>
    bool Supports(BrainExecutionContext context, out string? reason);

    /// <summary>
    /// Update one brain tick. Non-CPU backends must commit atomically; Brain restores
    /// a checkpoint and falls back to CPU if this throws.
    /// </summary>
    void Update(BrainExecutionContext context);
}

public interface ICpuAuthoritativeShadowBrainBackend : IBrainExecutionBackend
{
    string? LastShadowValidationFailure { get; }
    void UpdateCpuAuthoritativeShadow(BrainExecutionContext context);
}

public sealed class CpuBrainExecutionBackend : IBrainExecutionBackend
{
    public static CpuBrainExecutionBackend Instance { get; } = new();

    private CpuBrainExecutionBackend()
    {
    }

    public string Name => "classic cpu";
    public BrainExecutionBackendKind Kind => BrainExecutionBackendKind.Cpu;
    public bool IsAvailable => true;

    public bool Supports(BrainExecutionContext context, out string? reason)
    {
        reason = null;
        return true;
    }

    public void Update(BrainExecutionContext context)
        => context.RunClassicCpuUpdate();
}

public sealed class BrainExecutionContext
{
    private readonly Func<int, bool> _isLobeTokenShadowed;
    private readonly Action<LearningTrace?, int> _runClassicCpuUpdate;

    internal BrainExecutionContext(
        Brain brain,
        IReadOnlyList<BrainComponent> components,
        IReadOnlyList<IBrainModule> modules,
        LearningTrace? trace,
        int tick,
        Func<int, bool> isLobeTokenShadowed,
        Action<LearningTrace?, int> runClassicCpuUpdate)
    {
        Brain = brain;
        Components = components;
        Modules = modules;
        Trace = trace;
        Tick = tick;
        _isLobeTokenShadowed = isLobeTokenShadowed;
        _runClassicCpuUpdate = runClassicCpuUpdate;
    }

    public Brain Brain { get; }
    public IReadOnlyList<BrainComponent> Components { get; }
    public IReadOnlyList<IBrainModule> Modules { get; }
    public LearningTrace? Trace { get; }
    public int Tick { get; }

    public bool IsLobeTokenShadowed(int token)
        => _isLobeTokenShadowed(token);

    public void RunClassicCpuUpdate()
        => _runClassicCpuUpdate(Trace, Tick);
}
