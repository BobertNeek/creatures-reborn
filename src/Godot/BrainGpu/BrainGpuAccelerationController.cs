using CreaturesReborn.Sim.Brain;
using Godot;

namespace CreaturesReborn.Godot.BrainGpu;

public sealed class BrainGpuAccelerationController : System.IDisposable
{
    private const int ShadowParityTicksBeforePromotion = 30;

    private readonly GodotRenderingDeviceBrainBackend _backend;
    private int _shadowParityTicks;
    private bool _promoted;
    private bool _disposed;

    private BrainGpuAccelerationController(GodotRenderingDeviceBrainBackend backend)
    {
        _backend = backend;
    }

    public static BrainGpuAccelerationController? TryCreate()
    {
        if (string.Equals(DisplayServer.GetName(), "headless", System.StringComparison.OrdinalIgnoreCase))
        {
            GD.Print("[BrainGPU] RenderingDevice backend disabled: headless renderer.");
            return null;
        }

        GodotRenderingDeviceBrainBackend? backend = GodotRenderingDeviceBrainBackend.TryCreate(out string? reason);
        if (backend == null)
        {
            GD.Print($"[BrainGPU] RenderingDevice backend disabled: {reason ?? "unavailable"}");
            return null;
        }

        GD.Print("[BrainGPU] RenderingDevice backend ready; starting in shadow validation mode.");
        return new BrainGpuAccelerationController(backend);
    }

    public void Attach(Brain brain)
    {
        _shadowParityTicks = 0;
        _promoted = false;
        brain.ConfigureExecutionBackend(_backend, BrainExecutionMode.GpuShadowValidate);
    }

    public void ObserveTick(Brain brain)
    {
        if (_disposed)
            return;

        BrainExecutionStatus status = brain.LastExecutionStatus;
        if (!_promoted)
        {
            if (brain.ExecutionMode != BrainExecutionMode.GpuShadowValidate)
                return;

            if (status.UsedGpu
                && status.FallbackReason == null
                && _backend.LastCapabilityReport.CoverageCompleteForPromotion)
            {
                _shadowParityTicks++;
                if (_shadowParityTicks >= ShadowParityTicksBeforePromotion)
                {
                    brain.ConfigureExecutionBackend(_backend, BrainExecutionMode.GpuPreferred);
                    _promoted = true;
                    GD.Print("[BrainGPU] Shadow validation reached exact parity; promoted to GPU preferred mode.");
                }
            }
            else
            {
                if (status.FallbackReason != null)
                    GD.Print($"[BrainGPU] Shadow validation kept CPU state: {status.FallbackReason}");
                _shadowParityTicks = 0;
            }

            return;
        }

        if (brain.ExecutionMode == BrainExecutionMode.GpuPreferred && status.FallbackReason != null)
        {
            GD.Print($"[BrainGPU] GPU preferred fell back to CPU; returning to shadow validation: {status.FallbackReason}");
            brain.ConfigureExecutionBackend(_backend, BrainExecutionMode.GpuShadowValidate);
            _promoted = false;
            _shadowParityTicks = 0;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _backend.Dispose();
        _disposed = true;
    }
}
