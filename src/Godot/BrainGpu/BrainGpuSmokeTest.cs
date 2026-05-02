using System;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Godot;
using C = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Godot.BrainGpu;

public partial class BrainGpuSmokeTest : Node
{
    public override void _Ready()
    {
        string[] args = OS.GetCmdlineArgs();
        bool smoke = args.Any(arg => string.Equals(arg, "--brain-gpu-smoke", StringComparison.OrdinalIgnoreCase));
        bool soak = args.Any(arg => string.Equals(arg, "--brain-gpu-soak", StringComparison.OrdinalIgnoreCase));
        if (!smoke && !soak)
            return;

        try
        {
            if (soak)
            {
                RunSoak(ParseSoakTicks(args));
                GD.Print("[BrainGPU Soak] PASS");
            }
            else
            {
                RunSmoke();
                GD.Print("[BrainGPU Smoke] PASS");
            }
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[BrainGPU Smoke] FAIL: {ex}");
            GetTree().Quit(1);
        }
    }

    private static void RunSmoke()
    {
        using GodotRenderingDeviceBrainBackend? backend = GodotRenderingDeviceBrainBackend.TryCreate(out string? reason);
        if (backend == null)
            throw new InvalidOperationException($"RenderingDevice backend was not created: {reason}");

        string genomePath = ProjectSettings.GlobalizePath("res://data/genomes/starter.gen");
        C creature = C.LoadFromFile(
            genomePath,
            new StatefulRng(1234),
            GeneConstants.MALE,
            age: 128,
            variant: 0,
            moniker: "gpu-smoke");
        creature.SetChemical(ChemID.ATP, 1.0f);

        creature.Brain.ConfigureExecutionBackend(backend, BrainExecutionMode.GpuShadowValidate);
        for (int i = 0; i < 30; i++)
        {
            creature.Tick();
            BrainExecutionStatus status = creature.Brain.LastExecutionStatus;
            if (!status.UsedGpu)
                throw new InvalidOperationException($"Shadow tick {i} did not use the GPU backend: {status.FallbackReason}");
            if (status.FallbackReason != null)
                throw new InvalidOperationException($"Shadow tick {i} failed parity after accelerating {backend.AcceleratedLobeTokensLastTick}: {status.FallbackReason}");
            if (backend.AcceleratedLobesLastTick <= 0)
                throw new InvalidOperationException($"Shadow tick {i} did not accelerate any deterministic lobes.");
            if (backend.AcceleratedTractsLastTick <= 0)
                throw new InvalidOperationException($"Shadow tick {i} did not accelerate any deterministic tracts.");
            if (i > 0 && backend.LastDispatchMetrics.BufferRebuildCount != 0)
                throw new InvalidOperationException($"Shadow tick {i} rebuilt {backend.LastDispatchMetrics.BufferRebuildCount} GPU buffers despite stable topology.");
        }

        creature.Brain.ConfigureExecutionBackend(backend, BrainExecutionMode.GpuPreferred);
        creature.Tick();
        BrainExecutionStatus preferred = creature.Brain.LastExecutionStatus;
        if (!preferred.UsedGpu || preferred.FallbackReason != null)
            throw new InvalidOperationException($"GPU preferred did not stay active: {preferred.FallbackReason}");
        if (backend.AcceleratedLobesLastTick <= 0)
            throw new InvalidOperationException("GPU preferred did not accelerate any deterministic lobes.");
        if (backend.AcceleratedTractsLastTick <= 0)
            throw new InvalidOperationException("GPU preferred did not accelerate any deterministic tracts.");
        if (backend.LastDispatchMetrics.BufferRebuildCount != 0)
            throw new InvalidOperationException($"GPU preferred rebuilt {backend.LastDispatchMetrics.BufferRebuildCount} GPU buffers despite stable topology.");
    }

    private static void RunSoak(int ticks)
    {
        using GodotRenderingDeviceBrainBackend? backend = GodotRenderingDeviceBrainBackend.TryCreate(out string? reason);
        if (backend == null)
            throw new InvalidOperationException($"RenderingDevice backend was not created: {reason}");

        string genomePath = ProjectSettings.GlobalizePath("res://data/genomes/starter.gen");
        C creature = C.LoadFromFile(
            genomePath,
            new StatefulRng(4321),
            GeneConstants.MALE,
            age: 128,
            variant: 0,
            moniker: "gpu-soak");
        creature.SetChemical(ChemID.ATP, 1.0f);
        creature.Brain.ConfigureExecutionBackend(backend, BrainExecutionMode.GpuShadowValidate);

        long dispatchTicks = 0;
        long readbackTicks = 0;
        int dispatches = 0;
        int rebuilds = 0;
        for (int i = 0; i < ticks; i++)
        {
            creature.Tick();
            BrainExecutionStatus status = creature.Brain.LastExecutionStatus;
            if (!status.UsedGpu || status.FallbackReason != null)
                throw new InvalidOperationException($"Soak tick {i} failed GPU shadow parity: {status.FallbackReason}");

            BrainGpuDispatchMetrics metrics = backend.LastDispatchMetrics;
            dispatchTicks += metrics.DispatchElapsed.Ticks;
            readbackTicks += metrics.ReadbackElapsed.Ticks;
            dispatches += metrics.DispatchCount;
            rebuilds += metrics.BufferRebuildCount;
        }

        GD.Print($"[BrainGPU Soak] ticks={ticks} dispatches={dispatches} buffer_rebuilds={rebuilds} dispatch_ms={TimeSpan.FromTicks(dispatchTicks).TotalMilliseconds:F3} readback_ms={TimeSpan.FromTicks(readbackTicks).TotalMilliseconds:F3}");
    }

    private static int ParseSoakTicks(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--ticks", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out int ticks))
            {
                return Math.Max(1, ticks);
            }
        }

        return 10000;
    }
}
