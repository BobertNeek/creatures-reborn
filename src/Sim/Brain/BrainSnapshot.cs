using System.Collections.Generic;

namespace CreaturesReborn.Sim.Brain;

public sealed record BrainSnapshotOptions(int MaxNeuronsPerLobe = 32, int MaxDendritesPerTract = 32);

public sealed record BrainSnapshot(
    IReadOnlyList<LobeSnapshot> Lobes,
    IReadOnlyList<TractSnapshot> Tracts,
    IReadOnlyList<BrainModuleDescriptor> Modules,
    int InstinctsRemaining,
    bool IsProcessingInstincts);

public sealed record LobeSnapshot(
    int Index,
    int Token,
    string TokenText,
    int TissueId,
    int Width,
    int Height,
    int UpdateAtTime,
    int NeuronCount,
    int WinningNeuronId,
    IReadOnlyList<NeuronSnapshot> Neurons);

public sealed record NeuronSnapshot(int Index, float[] States);

public sealed record TractSnapshot(
    int Index,
    int UpdateAtTime,
    int SourceToken,
    string SourceTokenText,
    int DestinationToken,
    string DestinationTokenText,
    int DendriteCount,
    float STtoLTRate,
    IReadOnlyList<DendriteSnapshot> Dendrites);

public sealed record DendriteSnapshot(
    int Index,
    int SourceNeuronId,
    int DestinationNeuronId,
    float[] Weights);

public sealed record BrainModuleDescriptor(
    string Name,
    int? ShadowedLobeToken,
    string? ShadowedLobeTokenText,
    bool IsPassive,
    string Description)
{
    public static BrainModuleDescriptor FromModule(IBrainModule module)
        => new(
            module.GetType().Name,
            module.ShadowedLobeToken,
            module.ShadowedLobeToken.HasValue ? Brain.TokenToString(module.ShadowedLobeToken.Value) : null,
            module.ShadowedLobeToken == null,
            module.ShadowedLobeToken == null
                ? "Runs after the classic lobe/tract stack without shadowing a lobe."
                : "Shadows the named classic lobe token.");
}
