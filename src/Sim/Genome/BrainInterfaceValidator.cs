using System;
using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Genome;

public sealed record RequiredBrainPort(
    string Name,
    string LobeToken,
    int MinimumNeurons = 1);

public sealed record RequiredBrainRoute(
    string SourceLobe,
    string DestinationLobe);

public sealed record BrainLobeMinimum(
    string Token,
    int MinimumHealthyNeurons,
    int MaximumNeurons);

public sealed record BrainRouteMinimum(
    string SourceLobe,
    string DestinationLobe,
    int MinimumValidTracts = 1);

public sealed class BrainInterfaceReport
{
    public BrainInterfaceReport(IReadOnlyList<GenomeSimulationSafetyIssue> issues)
    {
        Issues = issues;
    }

    public IReadOnlyList<GenomeSimulationSafetyIssue> Issues { get; }
    public bool HasHardInvalid => Issues.Any(issue => issue.Severity == GenomeSimulationSafetySeverity.HardInvalid);
    public bool CanTick => !HasHardInvalid;
}

public static class BrainInterfaceValidator
{
    public static BrainInterfaceReport Validate(
        IReadOnlyList<GeneRecord> expressedGenes,
        MinimumBrainInterfaceSpec? spec = null)
    {
        spec ??= MinimumBrainInterfaceSpec.Default;
        var issues = new List<GenomeSimulationSafetyIssue>();
        Dictionary<string, DecodedLobe> lobes = expressedGenes
            .Where(record => record.Payload.Kind == GenePayloadKind.BrainLobe)
            .Select(record => DecodeLobe(record))
            .Where(lobe => lobe.Token.Length > 0)
            .GroupBy(lobe => lobe.Token)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        if (lobes.Count == 0)
        {
            issues.Add(new(
                GenomeSimulationSafetySeverity.HardInvalid,
                GenomeSimulationSafetyCode.MissingBrainInterface,
                "Genome has no expressed brain lobe genes."));
            return new BrainInterfaceReport(issues);
        }

        ValidateLobes(lobes, spec, issues);
        ValidateRoutes(expressedGenes, lobes, spec, issues);
        return new BrainInterfaceReport(issues);
    }

    private static void ValidateLobes(
        IReadOnlyDictionary<string, DecodedLobe> lobes,
        MinimumBrainInterfaceSpec spec,
        List<GenomeSimulationSafetyIssue> issues)
    {
        foreach (string requiredLobe in spec.RequiredLobes)
        {
            if (!lobes.TryGetValue(requiredLobe, out DecodedLobe lobe))
            {
                issues.Add(new(
                    GenomeSimulationSafetySeverity.HardInvalid,
                    GenomeSimulationSafetyCode.MissingRequiredLobe,
                    $"Required engine brain lobe '{requiredLobe}' is missing."));
                continue;
            }

            ValidateLobeShape(lobe, required: true, spec, issues);
        }

        foreach ((string token, DecodedLobe lobe) in lobes)
        {
            if (spec.RequiredLobes.Contains(token, StringComparer.OrdinalIgnoreCase))
                continue;

            ValidateLobeShape(lobe, required: false, spec, issues);
        }
    }

    private static void ValidateLobeShape(
        DecodedLobe lobe,
        bool required,
        MinimumBrainInterfaceSpec spec,
        List<GenomeSimulationSafetyIssue> issues)
    {
        int neurons = lobe.Width * lobe.Height;
        if (neurons <= 0)
        {
            issues.Add(new(
                required ? GenomeSimulationSafetySeverity.HardInvalid : GenomeSimulationSafetySeverity.QuarantineOnly,
                GenomeSimulationSafetyCode.ZeroSizedRequiredLobe,
                $"{(required ? "Required" : "Optional")} brain lobe '{lobe.Token}' has no neurons.",
                lobe.Record.Identity));
        }
        else if (neurons < spec.MinimumHealthyLobeNeurons)
        {
            issues.Add(new(
                GenomeSimulationSafetySeverity.WeakButLiving,
                GenomeSimulationSafetyCode.WeakBrainInterface,
                $"{(required ? "Required engine" : "Optional")} brain lobe '{lobe.Token}' has only {neurons} neuron(s).",
                lobe.Record.Identity));
        }
        else if (neurons > spec.MaximumLobeNeurons)
        {
            issues.Add(new(
                GenomeSimulationSafetySeverity.HardInvalid,
                GenomeSimulationSafetyCode.ZeroSizedRequiredLobe,
                $"Brain lobe '{lobe.Token}' has {neurons} neurons, beyond the bounded runtime limit.",
                lobe.Record.Identity));
        }
    }

    private static void ValidateRoutes(
        IReadOnlyList<GeneRecord> expressedGenes,
        IReadOnlyDictionary<string, DecodedLobe> lobes,
        MinimumBrainInterfaceSpec spec,
        List<GenomeSimulationSafetyIssue> issues)
    {
        IReadOnlyList<DecodedTract> tracts = expressedGenes
            .Where(record => record.Payload.Kind == GenePayloadKind.BrainTract)
            .Select(DecodeTract)
            .ToArray();

        foreach ((string Source, string Destination) requiredRoute in spec.RequiredRoutes)
        {
            bool routeExists = tracts.Any(tract =>
                string.Equals(tract.Source, requiredRoute.Source, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(tract.Destination, requiredRoute.Destination, StringComparison.OrdinalIgnoreCase) &&
                TractHasValidEndpoints(tract, lobes));

            if (!routeExists)
            {
                issues.Add(new(
                    GenomeSimulationSafetySeverity.HardInvalid,
                    GenomeSimulationSafetyCode.MissingRequiredBrainRoute,
                    $"Required brain route '{requiredRoute.Source}' -> '{requiredRoute.Destination}' is missing or has no valid endpoints."));
            }
        }
    }

    private static bool TractHasValidEndpoints(DecodedTract tract, IReadOnlyDictionary<string, DecodedLobe> lobes)
    {
        if (!lobes.TryGetValue(tract.Source, out DecodedLobe source))
            return false;
        if (!lobes.TryGetValue(tract.Destination, out DecodedLobe destination))
            return false;

        return RangeTouchesLobe(tract.SourceMin, tract.SourceMax, source.Neurons)
               && RangeTouchesLobe(tract.DestinationMin, tract.DestinationMax, destination.Neurons)
               && tract.SourceDendritesPerNeuron > 0
               && tract.DestinationDendritesPerNeuron > 0;
    }

    private static bool RangeTouchesLobe(int min, int max, int neurons)
        => neurons > 0 && max >= 0 && min < neurons && max >= min;

    private static DecodedLobe DecodeLobe(GeneRecord record)
    {
        byte[] bytes = record.Payload.Bytes;
        string token = bytes.Length >= 4 ? ReadToken(bytes, 0) : string.Empty;
        int width = bytes.Length > 10 ? bytes[10] : 0;
        int height = bytes.Length > 11 ? bytes[11] : 0;
        return new DecodedLobe(record, token, width, height);
    }

    private static DecodedTract DecodeTract(GeneRecord record)
    {
        byte[] bytes = record.Payload.Bytes;
        return new DecodedTract(
            ReadTokenIfPresent(bytes, 2),
            ReadInt16IfPresent(bytes, 6),
            ReadInt16IfPresent(bytes, 8),
            ReadInt16IfPresent(bytes, 10),
            ReadTokenIfPresent(bytes, 12),
            ReadInt16IfPresent(bytes, 16),
            ReadInt16IfPresent(bytes, 18),
            ReadInt16IfPresent(bytes, 20));
    }

    private static string ReadTokenIfPresent(byte[] bytes, int offset)
        => bytes.Length >= offset + 4 ? ReadToken(bytes, offset) : string.Empty;

    private static int ReadInt16IfPresent(byte[] bytes, int offset)
        => bytes.Length >= offset + 2 ? bytes[offset] * 256 + bytes[offset + 1] : 0;

    private static string ReadToken(byte[] bytes, int offset)
    {
        Span<char> chars = stackalloc char[4];
        for (int i = 0; i < 4; i++)
            chars[i] = (char)bytes[offset + i];
        return new string(chars).Trim();
    }

    private readonly record struct DecodedLobe(GeneRecord Record, string Token, int Width, int Height)
    {
        public int Neurons => Width * Height;
    }

    private readonly record struct DecodedTract(
        string Source,
        int SourceMin,
        int SourceMax,
        int SourceDendritesPerNeuron,
        string Destination,
        int DestinationMin,
        int DestinationMax,
        int DestinationDendritesPerNeuron);
}
