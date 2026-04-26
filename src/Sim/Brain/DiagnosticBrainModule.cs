using System.Collections.Generic;

namespace CreaturesReborn.Sim.Brain;

public sealed record BrainPortReading(BrainPort Port, float Value);

public sealed record DiagnosticBrainModuleSnapshot(
    int Tick,
    IReadOnlyList<BrainPortReading> Readings);

public sealed class DiagnosticBrainModule : IBrainModule
{
    private readonly BrainPortRegistry _registry;
    private int _tick;

    public DiagnosticBrainModule()
        : this(BrainPortRegistry.CreateDefault())
    {
    }

    public DiagnosticBrainModule(BrainPortRegistry registry)
    {
        _registry = registry;
    }

    public int? ShadowedLobeToken => null;
    public DiagnosticBrainModuleSnapshot? LatestSnapshot { get; private set; }

    public void Initialise(Brain brain)
    {
        LatestSnapshot = CreateSnapshot(brain);
    }

    public void Update(Brain brain)
    {
        _tick++;
        LatestSnapshot = CreateSnapshot(brain);
    }

    private DiagnosticBrainModuleSnapshot CreateSnapshot(Brain brain)
    {
        var readings = new List<BrainPortReading>(_registry.Ports.Count);
        foreach (BrainPort port in _registry.Ports)
            readings.Add(new BrainPortReading(port, brain.ReadPort(port)));

        return new DiagnosticBrainModuleSnapshot(_tick, readings);
    }
}
