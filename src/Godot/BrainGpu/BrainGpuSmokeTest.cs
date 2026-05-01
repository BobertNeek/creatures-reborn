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
        if (!OS.GetCmdlineArgs().Any(arg => string.Equals(arg, "--brain-gpu-smoke", StringComparison.OrdinalIgnoreCase)))
            return;

        try
        {
            RunSmoke();
            GD.Print("[BrainGPU Smoke] PASS");
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
        }

        creature.Brain.ConfigureExecutionBackend(backend, BrainExecutionMode.GpuPreferred);
        creature.Tick();
        BrainExecutionStatus preferred = creature.Brain.LastExecutionStatus;
        if (!preferred.UsedGpu || preferred.FallbackReason != null)
            throw new InvalidOperationException($"GPU preferred did not stay active: {preferred.FallbackReason}");
        if (backend.AcceleratedLobesLastTick <= 0)
            throw new InvalidOperationException("GPU preferred did not accelerate any deterministic lobes.");
    }
}
