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
        RequiredLobes: ["driv", "decn"],
        RequiredRoutes: []);
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

        IReadOnlyList<GeneRecord> expressed = records
            .Where(record => IsCompatibleWithCreature(record, options.Sex, options.Variant))
            .ToArray();

        ValidateBrain(expressed, options.BrainSpec, issues);
        ValidateBiology(expressed, options.BiologySpec, issues);
        return new GenomeSimulationSafetyReport(issues);
    }

    private static bool IsCompatibleWithCreature(GeneRecord record, int sex, int variant)
    {
        GeneHeader header = record.Header;
        if (header.IsSilent)
            return false;
        if (header.MaleLinked && sex != GeneConstants.MALE)
            return false;
        if (header.FemaleLinked && sex != GeneConstants.FEMALE)
            return false;
        return header.Variant == 0 || header.Variant == variant;
    }

    private static void ValidateBrain(
        IReadOnlyList<GeneRecord> expressed,
        MinimumBrainInterfaceSpec spec,
        List<GenomeSimulationSafetyIssue> issues)
    {
        issues.AddRange(BrainInterfaceValidator.Validate(expressed, spec).Issues);
    }

    private static void ValidateBiology(
        IReadOnlyList<GeneRecord> expressed,
        MinimumBiologyInterfaceSpec spec,
        List<GenomeSimulationSafetyIssue> issues)
    {
        issues.AddRange(FallibleBiologyValidator.Validate(expressed, spec).Issues);
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
