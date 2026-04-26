using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Genome;

namespace CreaturesReborn.Sim.Lab;

public sealed record EvolutionHookBuildOptions(
    int MaxSourceNeurons = 8,
    int MaxTargetNeurons = 8,
    float LearningRate = 0.05f,
    bool IncludePlasticityCandidates = true);

public enum EvolutionModuleCandidateKind
{
    Plasticity
}

public sealed record EvolutionLobeAnchor(
    GeneIdentity Gene,
    int Token,
    string TokenText,
    int Width,
    int Height,
    int NeuronCount);

public sealed record EvolutionTractAnchor(
    GeneIdentity Gene,
    int SourceToken,
    string SourceTokenText,
    int SourceNeuronMin,
    int SourceNeuronMax,
    int TargetToken,
    string TargetTokenText,
    int TargetNeuronMin,
    int TargetNeuronMax);

public sealed record EvolutionModuleCandidate(
    int InnovationId,
    EvolutionModuleCandidateKind Kind,
    bool OfflineOnly,
    bool EnabledByDefault,
    GeneIdentity ConnectionGene,
    int SourceLobeToken,
    string SourceLobeTokenText,
    int TargetLobeToken,
    string TargetLobeTokenText,
    int SourceNeuronCount,
    int TargetNeuronCount,
    float LearningRate)
{
    public PlasticityBrainModuleOptions ToPlasticityOptions(
        bool enabled = false,
        bool shadowTargetLobe = false,
        bool writeTargetInputs = false)
        => new()
        {
            Enabled = enabled,
            ShadowedLobeToken = enabled && shadowTargetLobe ? TargetLobeToken : null,
            SourceLobeToken = SourceLobeToken,
            TargetLobeToken = TargetLobeToken,
            SourceNeuronCount = SourceNeuronCount,
            TargetNeuronCount = TargetNeuronCount,
            LearningRate = LearningRate,
            WriteTargetInputs = enabled && writeTargetInputs
        };
}

public sealed record GenomeEvolutionHookSet(
    string GenomeMoniker,
    bool OfflineOnly,
    IReadOnlyList<EvolutionLobeAnchor> Lobes,
    IReadOnlyList<EvolutionTractAnchor> Tracts,
    IReadOnlyList<EvolutionModuleCandidate> ModuleCandidates)
{
    public static GenomeEvolutionHookSet Create(Genome.Genome genome)
        => Create(genome, new EvolutionHookBuildOptions());

    public static GenomeEvolutionHookSet Create(Genome.Genome genome, EvolutionHookBuildOptions options)
    {
        options = Validate(options);
        IReadOnlyList<GeneRecord> genes = GeneDecoder.Decode(genome);
        var lobes = ExtractLobes(genes);
        var tracts = ExtractTracts(genes);
        var candidates = options.IncludePlasticityCandidates
            ? CreatePlasticityCandidates(lobes, tracts, options)
            : new List<EvolutionModuleCandidate>();

        return new GenomeEvolutionHookSet(
            genome.Moniker,
            OfflineOnly: true,
            lobes,
            tracts,
            candidates);
    }

    private static List<EvolutionLobeAnchor> ExtractLobes(IReadOnlyList<GeneRecord> genes)
    {
        var lobes = new List<EvolutionLobeAnchor>();
        foreach (GeneRecord gene in genes)
        {
            if (gene.Payload.Kind != GenePayloadKind.BrainLobe || gene.Payload.Bytes.Length < 12)
                continue;

            byte[] payload = gene.Payload.Bytes;
            int token = ReadToken(payload, 0);
            int width = payload[10];
            int height = payload[11];
            int neuronCount = width * height;
            if (token == 0 || neuronCount <= 0)
                continue;

            lobes.Add(new EvolutionLobeAnchor(
                gene.Identity,
                token,
                Brain.Brain.TokenToString(token),
                width,
                height,
                neuronCount));
        }

        return lobes;
    }

    private static List<EvolutionTractAnchor> ExtractTracts(IReadOnlyList<GeneRecord> genes)
    {
        var tracts = new List<EvolutionTractAnchor>();
        foreach (GeneRecord gene in genes)
        {
            if (gene.Payload.Kind != GenePayloadKind.BrainTract || gene.Payload.Bytes.Length < 22)
                continue;

            byte[] payload = gene.Payload.Bytes;
            int sourceToken = ReadToken(payload, 2);
            int targetToken = ReadToken(payload, 12);
            if (sourceToken == 0 || targetToken == 0)
                continue;

            tracts.Add(new EvolutionTractAnchor(
                gene.Identity,
                sourceToken,
                Brain.Brain.TokenToString(sourceToken),
                ReadInt(payload, 6),
                ReadInt(payload, 8),
                targetToken,
                Brain.Brain.TokenToString(targetToken),
                ReadInt(payload, 16),
                ReadInt(payload, 18)));
        }

        return tracts;
    }

    private static List<EvolutionModuleCandidate> CreatePlasticityCandidates(
        IReadOnlyList<EvolutionLobeAnchor> lobes,
        IReadOnlyList<EvolutionTractAnchor> tracts,
        EvolutionHookBuildOptions options)
    {
        var lobeByToken = new Dictionary<int, EvolutionLobeAnchor>();
        foreach (EvolutionLobeAnchor lobe in lobes)
            lobeByToken.TryAdd(lobe.Token, lobe);

        var candidates = new List<EvolutionModuleCandidate>();
        foreach (EvolutionTractAnchor tract in tracts)
        {
            if (!lobeByToken.TryGetValue(tract.SourceToken, out EvolutionLobeAnchor? sourceLobe) ||
                !lobeByToken.TryGetValue(tract.TargetToken, out EvolutionLobeAnchor? targetLobe))
            {
                continue;
            }

            int sourceCount = BoundedCount(
                tract.SourceNeuronMin,
                tract.SourceNeuronMax,
                sourceLobe.NeuronCount,
                options.MaxSourceNeurons);
            int targetCount = BoundedCount(
                tract.TargetNeuronMin,
                tract.TargetNeuronMax,
                targetLobe.NeuronCount,
                options.MaxTargetNeurons);
            if (sourceCount <= 0 || targetCount <= 0)
                continue;

            candidates.Add(new EvolutionModuleCandidate(
                InnovationId: CreateInnovationId(EvolutionModuleCandidateKind.Plasticity, tract.Gene, tract.SourceToken, tract.TargetToken),
                Kind: EvolutionModuleCandidateKind.Plasticity,
                OfflineOnly: true,
                EnabledByDefault: false,
                ConnectionGene: tract.Gene,
                SourceLobeToken: tract.SourceToken,
                SourceLobeTokenText: tract.SourceTokenText,
                TargetLobeToken: tract.TargetToken,
                TargetLobeTokenText: tract.TargetTokenText,
                SourceNeuronCount: sourceCount,
                TargetNeuronCount: targetCount,
                LearningRate: options.LearningRate));
        }

        return candidates;
    }

    private static int BoundedCount(int min, int max, int lobeNeuronCount, int configuredMaximum)
    {
        int range = max >= min ? (max - min + 1) : lobeNeuronCount;
        int available = Math.Min(Math.Max(0, range), lobeNeuronCount);
        return Math.Min(available, configuredMaximum);
    }

    private static int ReadInt(byte[] bytes, int offset)
        => (bytes[offset] * 256) + bytes[offset + 1];

    private static int ReadToken(byte[] bytes, int offset)
        => bytes[offset]
           | (bytes[offset + 1] << 8)
           | (bytes[offset + 2] << 16)
           | (bytes[offset + 3] << 24);

    private static int CreateInnovationId(
        EvolutionModuleCandidateKind kind,
        GeneIdentity gene,
        int sourceToken,
        int targetToken)
    {
        unchecked
        {
            uint hash = 2166136261;
            Add(ref hash, (int)kind);
            Add(ref hash, gene.Type);
            Add(ref hash, gene.Subtype);
            Add(ref hash, gene.Id);
            Add(ref hash, gene.Generation);
            Add(ref hash, sourceToken);
            Add(ref hash, targetToken);
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    private static void Add(ref uint hash, int value)
    {
        unchecked
        {
            for (int i = 0; i < 4; i++)
            {
                hash ^= (byte)(value >> (i * 8));
                hash *= 16777619;
            }
        }
    }

    private static EvolutionHookBuildOptions Validate(EvolutionHookBuildOptions options)
    {
        ValidateTrackedCount(options.MaxSourceNeurons, nameof(options.MaxSourceNeurons));
        ValidateTrackedCount(options.MaxTargetNeurons, nameof(options.MaxTargetNeurons));
        if (float.IsNaN(options.LearningRate) || float.IsInfinity(options.LearningRate))
            throw new ArgumentOutOfRangeException(nameof(options.LearningRate));

        return options;
    }

    private static void ValidateTrackedCount(int value, string parameterName)
    {
        if (value < 1 || value > PlasticityBrainModule.MaxTrackedNeurons)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
