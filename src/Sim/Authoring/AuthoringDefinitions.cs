using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Lab;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Sim.Authoring;

public enum AuthoringValidationSeverity
{
    Info,
    Warning,
    Error
}

public enum AuthoringValidationCode
{
    MissingName,
    DuplicateChemicalId,
    InvalidChemicalId,
    InvalidRange,
    DuplicateCaChannel,
    InvalidCaChannel,
    InvalidCaValue,
    DuplicateAffordanceToken,
    InvalidAffordanceToken,
    InvalidAffordanceVerb,
    InvalidLabTicks,
    EmptyLabPopulation,
    InvalidLabGenomePath,
    InvalidLabWorldValue
}

public sealed record AuthoringValidationIssue(
    AuthoringValidationSeverity Severity,
    AuthoringValidationCode Code,
    string Path,
    string Message);

public sealed record AuthoringValidationResult(IReadOnlyList<AuthoringValidationIssue> Issues)
{
    public bool HasErrors
    {
        get
        {
            foreach (AuthoringValidationIssue issue in Issues)
            {
                if (issue.Severity == AuthoringValidationSeverity.Error)
                    return true;
            }

            return false;
        }
    }
}

public sealed record CaPresetChannelValue(int ChannelIndex, float Value);

public sealed record CaPresetDefinition(string Name, IReadOnlyList<CaPresetChannelValue> Channels)
{
    public void ApplyTo(Room room)
    {
        foreach (CaPresetChannelValue channel in Channels)
        {
            if ((uint)channel.ChannelIndex >= CaIndex.Count)
                continue;

            room.CA[channel.ChannelIndex] = Math.Clamp(channel.Value, 0.0f, 1.0f);
        }
    }

    public AuthoringValidationResult Validate()
        => AuthoringValidator.ValidateCaPreset(this, "caPresets[0]");
}

public sealed record AuthoringDefinitionBundle(
    IReadOnlyList<ChemicalDefinition> Chemicals,
    IReadOnlyList<CaPresetDefinition> CaPresets,
    IReadOnlyList<AgentAffordance> AgentAffordances,
    IReadOnlyList<LabRunConfig> LabConfigs)
{
    public static AuthoringDefinitionBundle CreateBuiltIn()
        => new(
            ChemicalCatalog.All,
            new[] { BuiltInCaPresets.Neutral },
            AgentAffordanceCatalog.All,
            Array.Empty<LabRunConfig>());

    public AuthoringValidationResult Validate()
        => AuthoringValidator.Validate(this);
}

public static class BuiltInCaPresets
{
    public static CaPresetDefinition Neutral { get; } = new(
        "neutral",
        new[]
        {
            new CaPresetChannelValue(CaIndex.Temperature, 0.5f),
            new CaPresetChannelValue(CaIndex.Light, 0.5f),
            new CaPresetChannelValue(CaIndex.Radiation, 0.0f)
        });
}

public static class AuthoringValidator
{
    public static AuthoringValidationResult Validate(AuthoringDefinitionBundle bundle)
    {
        var issues = new List<AuthoringValidationIssue>();
        ValidateChemicals(bundle.Chemicals, issues);
        ValidateCaPresets(bundle.CaPresets, issues);
        ValidateAffordances(bundle.AgentAffordances, issues);
        ValidateLabConfigs(bundle.LabConfigs, issues);
        return new AuthoringValidationResult(issues);
    }

    public static AuthoringValidationResult ValidateCaPreset(CaPresetDefinition preset, string path)
    {
        var issues = new List<AuthoringValidationIssue>();
        ValidateCaPreset(preset, path, issues);
        return new AuthoringValidationResult(issues);
    }

    private static void ValidateChemicals(
        IReadOnlyList<ChemicalDefinition> chemicals,
        List<AuthoringValidationIssue> issues)
    {
        var seenIds = new HashSet<int>();
        for (int i = 0; i < chemicals.Count; i++)
        {
            ChemicalDefinition chemical = chemicals[i];
            string path = $"chemicals[{chemical.Id}]";

            if ((uint)chemical.Id >= BiochemConst.NUMCHEM)
            {
                Add(issues, AuthoringValidationCode.InvalidChemicalId, path,
                    $"Chemical id {chemical.Id} is outside 0..{BiochemConst.NUMCHEM - 1}.");
                continue;
            }

            if (!seenIds.Add(chemical.Id))
            {
                Add(issues, AuthoringValidationCode.DuplicateChemicalId, path,
                    $"Chemical id {chemical.Id} is defined more than once.");
            }

            ValidateName(chemical.Token, $"{path}.token", issues);
            ValidateName(chemical.DisplayName, $"{path}.displayName", issues);
            ValidateRange(chemical.Range, $"{path}.range", issues);
        }
    }

    private static void ValidateCaPresets(
        IReadOnlyList<CaPresetDefinition> presets,
        List<AuthoringValidationIssue> issues)
    {
        for (int i = 0; i < presets.Count; i++)
            ValidateCaPreset(presets[i], $"caPresets[{i}]", issues);
    }

    private static void ValidateCaPreset(
        CaPresetDefinition preset,
        string path,
        List<AuthoringValidationIssue> issues)
    {
        ValidateName(preset.Name, $"{path}.name", issues);
        var seenChannels = new HashSet<int>();
        for (int i = 0; i < preset.Channels.Count; i++)
        {
            CaPresetChannelValue channel = preset.Channels[i];
            string channelPath = $"{path}.channels[{i}]";
            if ((uint)channel.ChannelIndex >= CaIndex.Count)
            {
                Add(issues, AuthoringValidationCode.InvalidCaChannel, $"{channelPath}.channelIndex",
                    $"CA channel index {channel.ChannelIndex} is outside 0..{CaIndex.Count - 1}.");
            }
            else if (!seenChannels.Add(channel.ChannelIndex))
            {
                Add(issues, AuthoringValidationCode.DuplicateCaChannel, $"{channelPath}.channelIndex",
                    $"CA channel {channel.ChannelIndex} appears more than once in preset '{preset.Name}'.");
            }

            if (channel.Value < 0.0f || channel.Value > 1.0f || float.IsNaN(channel.Value))
            {
                Add(issues, AuthoringValidationCode.InvalidCaValue, $"{channelPath}.value",
                    $"CA channel value {channel.Value} must be in the inclusive range 0..1.");
            }
        }
    }

    private static void ValidateAffordances(
        IReadOnlyList<AgentAffordance> affordances,
        List<AuthoringValidationIssue> issues)
    {
        var seenTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < affordances.Count; i++)
        {
            AgentAffordance affordance = affordances[i];
            string path = $"agentAffordances[{i}]";
            if (string.IsNullOrWhiteSpace(affordance.Token))
            {
                Add(issues, AuthoringValidationCode.InvalidAffordanceToken, $"{path}.token",
                    "Agent affordance token is required.");
            }
            else if (!seenTokens.Add(affordance.Token))
            {
                Add(issues, AuthoringValidationCode.DuplicateAffordanceToken, $"{path}.token",
                    $"Agent affordance token '{affordance.Token}' is defined more than once.");
            }

            if (affordance.VerbId < 0)
            {
                Add(issues, AuthoringValidationCode.InvalidAffordanceVerb, $"{path}.verbId",
                    $"Agent affordance verb id {affordance.VerbId} cannot be negative.");
            }
        }
    }

    private static void ValidateLabConfigs(
        IReadOnlyList<LabRunConfig> configs,
        List<AuthoringValidationIssue> issues)
    {
        for (int i = 0; i < configs.Count; i++)
        {
            LabRunConfig config = configs[i];
            string path = $"labConfigs[{i}]";
            if (config.Ticks < 0)
            {
                Add(issues, AuthoringValidationCode.InvalidLabTicks, $"{path}.ticks",
                    $"Lab tick count {config.Ticks} cannot be negative.");
            }

            if (config.Population == null || config.Population.Count == 0)
            {
                Add(issues, AuthoringValidationCode.EmptyLabPopulation, $"{path}.population",
                    "Lab config must define at least one creature seed.");
            }
            else
            {
                for (int p = 0; p < config.Population.Count; p++)
                {
                    if (string.IsNullOrWhiteSpace(config.Population[p].GenomePath))
                    {
                        Add(issues, AuthoringValidationCode.InvalidLabGenomePath, $"{path}.population[{p}].genomePath",
                            "Lab creature seed genome path is required.");
                    }
                }
            }

            ValidateLabWorldPreset(config.WorldPreset, $"{path}.worldPreset", issues);
        }
    }

    private static void ValidateLabWorldPreset(
        LabWorldPreset? preset,
        string path,
        List<AuthoringValidationIssue> issues)
    {
        if (preset == null)
            return;

        ValidateName(preset.Name, $"{path}.name", issues);
        ValidateUnitValue(preset.Temperature, $"{path}.temperature", issues);
        ValidateUnitValue(preset.Light, $"{path}.light", issues);
        ValidateUnitValue(preset.Radiation, $"{path}.radiation", issues);
        ValidateUnitValue(preset.AirQuality, $"{path}.airQuality", issues);
    }

    private static void ValidateRange(
        ChemicalRange range,
        string path,
        List<AuthoringValidationIssue> issues)
    {
        if (range.HardMin > range.HardMax)
        {
            Add(issues, AuthoringValidationCode.InvalidRange, path,
                "Chemical hard minimum cannot be greater than hard maximum.");
        }

        if (range.NormalMin > range.NormalMax)
        {
            Add(issues, AuthoringValidationCode.InvalidRange, path,
                "Chemical normal minimum cannot be greater than normal maximum.");
        }

        if (range.NormalMin < range.HardMin || range.NormalMax > range.HardMax)
        {
            Add(issues, AuthoringValidationCode.InvalidRange, path,
                "Chemical normal range must fit inside the hard range.");
        }
    }

    private static void ValidateName(
        string name,
        string path,
        List<AuthoringValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(name))
            Add(issues, AuthoringValidationCode.MissingName, path, "Name or token is required.");
    }

    private static void ValidateUnitValue(
        float value,
        string path,
        List<AuthoringValidationIssue> issues)
    {
        if (value < 0.0f || value > 1.0f || float.IsNaN(value))
            Add(issues, AuthoringValidationCode.InvalidLabWorldValue, path, "Lab world preset value must be in 0..1.");
    }

    private static void Add(
        List<AuthoringValidationIssue> issues,
        AuthoringValidationCode code,
        string path,
        string message)
        => issues.Add(new AuthoringValidationIssue(
            AuthoringValidationSeverity.Error,
            code,
            path,
            message));
}
