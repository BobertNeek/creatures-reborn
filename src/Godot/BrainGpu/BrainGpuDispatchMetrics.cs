using System;

namespace CreaturesReborn.Godot.BrainGpu;

public sealed record BrainGpuDispatchMetrics(
    int DispatchCount,
    int BufferRebuildCount,
    TimeSpan DispatchElapsed,
    TimeSpan ReadbackElapsed)
{
    public static BrainGpuDispatchMetrics Empty { get; } = new(
        DispatchCount: 0,
        BufferRebuildCount: 0,
        DispatchElapsed: TimeSpan.Zero,
        ReadbackElapsed: TimeSpan.Zero);
}
