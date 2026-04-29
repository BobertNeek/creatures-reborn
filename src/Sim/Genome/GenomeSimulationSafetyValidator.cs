using System;
using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;

namespace CreaturesReborn.Sim.Genome;

public enum GenomeSimulationSafetySeverity
{
    Info = 0,
    WeakButLiving = 1,
    QuarantineOnly = 2,
    HardInvalid = 3
}

public enum GenomeSimulationSafetyCode
{
    MalformedGenome,
    MissingBrainInterface,
    MissingRequiredLobe,
    ZeroSizedRequiredLobe,
    MissingRequiredBrainRoute,
    NoFallibleLifeSupport,
    WeakBrainInterface
}

public sealed record GenomeSimulationSafetyIssue(
    GenomeSimulationSafetySeverity Severity,
    GenomeSimulationSafetyCode Code,
    string Message,
    GeneIdentity? Gene = null);

public sealed record MinimumBrainInterfaceSpec(
    IReadOnlyList<string> RequiredLobes,
    IReadOnlyList<(string Source, string Destination)> RequiredRoutes,
    int MinimumHealthyLobeNeurons = 4,
    int MaximumLobeNeurons = 4096)
{
    public static MinimumBrainInterfaceSpec Default { get; } = new(
        RequiredLobes: ["driv", "decn", "verb", "noun", "attn"],
        RequiredRoutes: [("driv", "decn")]);
}

public sealed record MinimumBiologyInterfaceSpec(
    bool RequireOrgan = true,
    bool RequireEnergyReaction = true,
    bool RequireDeathOrInjuryRoute = true)
{
    public static MinimumBiologyInterfaceSpec Default { get; } = new();
}

public sealed record GenomeSimulationSafetyOptions(
    int Sex = GeneConstants.MALE,
    byte Age = 0,
    int Variant = 0,
    bool AllowQuarantineOnlyToHatch = false,
    MinimumBrainInterfaceSpec? Brain = null,
    MinimumBiologyInterfaceSpec? Biology = null)
{
    public MinimumBrainInterfaceSpec BrainSpec => Brain ?? MinimumBrainInterfaceSpec.Default;
    public MinimumBiologyInterfaceSpec BiologySpec => Biology ?? MinimumBiologyInterfaceSpec.Default;
}

public sealed class GenomeSimulationSafetyReport
{
    public GenomeSimulationSafetyReport(IReadOnlyList<GenomeSimulationSafetyIssue> issues)
    {
        Issues = issues;
    }

    public IReadOnlyList<GenomeSimulationSafetyIssue> Issues { get; }

    public bool HasHardInvalid => Issues.Any(issue => issue.Severity == GenomeSimulationSafetySeverity.HardInvalid);

    public bool HasQuarantineOnly => Issues.Any(issue => issue.Severity == GenomeSimulationSafetySeverity.QuarantineOnly);

    public bool CanHatch => !HasHardInvalid;
}

public static class GenomeSimulationSafetyValidator
{
    public static GenomeSimulationSafetyReport Validate(Genome genome, GenomeSimulationSafetyOptions? options = null)
        => ValidateRecords(GeneDecoder.Decode(genome), GeneValidator.Validate(genome), options ?? OptionsFromGenome(genome));

    public static GenomeSimulationSafetyReport ValidateRaw(byte[] rawGenome, GenomeSimulationSafetyOptions? options = null)
        => ValidateRecords(GeneDecoder.DecodeRaw(rawGenome), GeneValidator.ValidateRaw(rawGenome), options ?? new GenomeSimulationSafetyOptions());

    private static GenomeSimulationSafetyOptions OptionsFromGenome(Genome genome)
        => new(genome.Sex, genome.Age, genome.Variant);

    private static GenomeSimulationSafetyReport ValidateRecords(
        IReadOnlyList<GeneRecord> records,
        IReadOnlyList<GeneValidationIssue> validationIssues,
        GenomeSimulationSafetyOptions options)
    {
        var issues = new List<GenomeSimulationSafetyIssue>();
        foreach (GeneValidationIssue validationIssue in validationIssues)
        {
            if (validationIssue.Severity == GeneValidationSeverity.Error)
            {
                issues.Add(new(
                    GenomeSimulationSafetySeverity.HardInvalid,
                    GenomeSimulationSafetyCode.MalformedGenome,
                    validationIssue.Message));
            }
        }

        IReadOnlyList<GeneRecord> expressed = new GenomeExpressionPlan(records)
            .GenesEligibleAt(options.Age, options.Sex, options.Variant);

        ValidateBrain(expressed, options.BrainSpec, issues);
        ValidateBiology(expressed, options.BiologySpec, issues);
        return new GenomeSimulationSafetyReport(issues);
    }

    private static void ValidateBrain(
        IReadOnlyList<GeneRecord> expressed,
        MinimumBrainInterfaceSpec spec,
        List<GenomeSimulationSafetyIssue> issues)
    {
        Dictionary<string, GeneRecord> lobes = expressed
            .Where(record => record.Payload.Kind == GenePayloadKind.BrainLobe)
            .Select(record => (Record: record, Lobe: DecodeLobe(record)))
            .Where(pair => pair.Lobe.Token.Length > 0)
            .GroupBy(pair => pair.Lobe.Token)
            .ToDictionary(group => group.Key, group => group.First().Record, StringComparer.OrdinalIgnoreCase);

        if (lobes.Count == 0)
        {
            issues.Add(new(
                GenomeSimulationSafetySeverity.HardInvalid,
                GenomeSimulationSafetyCode.MissingBrainInterface,
                "Genome has no expressed brain lobe genes."));
            return;
        }

        foreach (string requiredLobe in spec.RequiredLobes)
        {
            if (!lobes.TryGetValue(requiredLobe, out GeneRecord? record))
            {
                issues.Add(new(
                    GenomeSimulationSafetySeverity.HardInvalid,
                    GenomeSimulationSafetyCode.MissingRequiredLobe,
                    $"Required engine brain lobe '{requiredLobe}' is missing."));
                continue;
            }

            LobeShape shape = DecodeLobe(record);
            int neurons = shape.Width * shape.Height;
            if (neurons <= 0)
            {
                issues.Add(new(
                    GenomeSimulationSafetySeverity.HardInvalid,
                    GenomeSimulationSafetyCode.ZeroSizedRequiredLobe,
                    $"Required engine brain lobe '{requiredLobe}' has no neurons.",
                    record.Identity));
            }
            else if (neurons < spec.MinimumHealthyLobeNeurons)
            {
                issues.Add(new(
                    GenomeSimulationSafetySeverity.WeakButLiving,
                    GenomeSimulationSafetyCode.WeakBrainInterface,
                    $"Required engine brain lobe '{requiredLobe}' has only {neurons} neuron(s).",
                    record.Identity));
            }
            else if (neurons > spec.MaximumLobeNeurons)
            {
                issues.Add(new(
                    GenomeSimulationSafetySeverity.HardInvalid,
                    GenomeSimulationSafetyCode.ZeroSizedRequiredLobe,
                    $"Required engine brain lobe '{requiredLobe}' has {neurons} neurons, beyond the bounded runtime limit.",
                    record.Identity));
            }
        }

        HashSet<(string Source, string Destination)> routes = expressed
            .Where(record => record.Payload.Kind == GenePayloadKind.BrainTract)
            .Select(DecodeTract)
            .ToHashSet();

        foreach ((string Source, string Destination) requiredRoute in spec.RequiredRoutes)
        {
            if (!routes.Contains(requiredRoute))
            {
                issues.Add(new(
                    GenomeSimulationSafetySeverity.HardInvalid,
                    GenomeSimulationSafetyCode.MissingRequiredBrainRoute,
                    $"Required brain route '{requiredRoute.Source}' -> '{requiredRoute.Destination}' is missing."));
            }
        }
    }

    private static void ValidateBiology(
        IReadOnlyList<GeneRecord> expressed,
        MinimumBiologyInterfaceSpec spec,
        List<GenomeSimulationSafetyIssue> issues)
    {
        bool hasOrgan = expressed.Any(record => record.Payload.Kind is GenePayloadKind.Organ or GenePayloadKind.BrainOrgan);
        bool hasEnergyReaction = expressed
            .Where(record => record.Payload.Kind == GenePayloadKind.BiochemistryReaction)
            .Any(ReactionMentionsEnergy);
        bool hasDeathOrInjuryRoute = expressed
            .Where(record => record.Payload.Kind is GenePayloadKind.BiochemistryReaction or GenePayloadKind.BiochemistryReceptor or GenePayloadKind.BiochemistryEmitter)
            .Any(record => PayloadMentions(record, ChemID.Injury)
                           || PayloadMentions(record, ChemID.Wounded)
                           || PayloadMentions(record, ChemID.Pain)
                           || PayloadMentions(record, ChemID.ATP)
                           || PayloadMentions(record, ChemID.ADP));

        if ((spec.RequireOrgan && !hasOrgan)
            || (spec.RequireEnergyReaction && !hasEnergyReaction)
            || (spec.RequireDeathOrInjuryRoute && !hasDeathOrInjuryRoute))
        {
            issues.Add(new(
                GenomeSimulationSafetySeverity.HardInvalid,
                GenomeSimulationSafetyCode.NoFallibleLifeSupport,
                "Genome has no conservative evidence of fallible life-support chemistry."));
        }
    }

    private static bool ReactionMentionsEnergy(GeneRecord record)
        => PayloadMentions(record, ChemID.ATP) || PayloadMentions(record, ChemID.ADP);

    private static bool PayloadMentions(GeneRecord record, int chemical)
        => record.Payload.Bytes.Contains((byte)chemical);

    private static LobeShape DecodeLobe(GeneRecord record)
    {
        byte[] bytes = record.Payload.Bytes;
        string token = bytes.Length >= 4 ? ReadToken(bytes, 0) : string.Empty;
        int width = bytes.Length > 10 ? bytes[10] : 0;
        int height = bytes.Length > 11 ? bytes[11] : 0;
        return new LobeShape(token, width, height);
    }

    private static (string Source, string Destination) DecodeTract(GeneRecord record)
    {
        byte[] bytes = record.Payload.Bytes;
        string source = bytes.Length >= 6 ? ReadToken(bytes, 2) : string.Empty;
        string destination = bytes.Length >= 16 ? ReadToken(bytes, 12) : string.Empty;
        return (source, destination);
    }

    private static string ReadToken(byte[] bytes, int offset)
    {
        Span<char> chars = stackalloc char[4];
        for (int i = 0; i < 4; i++)
            chars[i] = (char)bytes[offset + i];
        return new string(chars).Trim();
    }

    private readonly record struct LobeShape(string Token, int Width, int Height);
}
