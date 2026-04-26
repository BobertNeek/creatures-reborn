using System;
using System.Linq;
using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.Authoring;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Lab;
using CreaturesReborn.Sim.World;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class AuthoringDefinitionTests
{
    [Fact]
    public void BuiltInAuthoringDefinitions_ValidateCleanly()
    {
        AuthoringValidationResult result = AuthoringDefinitionBundle.CreateBuiltIn().Validate();

        Assert.False(result.HasErrors);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void AuthoringValidator_ReportsActionableChemicalIssues()
    {
        ChemicalDefinition invalidRange = new(
            ChemID.ATP,
            "atp",
            "ATP",
            ChemicalCategory.Energy,
            new ChemicalRange(0.8f, 0.2f, 0.0f, 1.0f),
            "#56a6d6");
        ChemicalDefinition duplicate = invalidRange with { DisplayName = "Duplicate ATP" };
        AuthoringDefinitionBundle bundle = new(
            Chemicals: new[] { invalidRange, duplicate },
            CaPresets: Array.Empty<CaPresetDefinition>(),
            AgentAffordances: Array.Empty<AgentAffordance>(),
            LabConfigs: Array.Empty<LabRunConfig>());

        AuthoringValidationResult result = bundle.Validate();

        Assert.True(result.HasErrors);
        Assert.Contains(result.Issues, issue =>
            issue.Code == AuthoringValidationCode.DuplicateChemicalId &&
            issue.Path == "chemicals[35]");
        Assert.Contains(result.Issues, issue =>
            issue.Code == AuthoringValidationCode.InvalidRange &&
            issue.Message.Contains("normal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AuthoringValidator_ReportsInvalidCaPresetAndAffordanceIssues()
    {
        CaPresetDefinition badPreset = new(
            "bad-ca",
            new[]
            {
                new CaPresetChannelValue(CaIndex.Temperature, 0.5f),
                new CaPresetChannelValue(CaIndex.Temperature, 0.6f),
                new CaPresetChannelValue(CaIndex.Count + 1, 1.2f)
            });
        AgentAffordance duplicateA = AgentAffordanceCatalog.ForKind(AgentAffordanceKind.Eat) with { Token = "eat" };
        AgentAffordance duplicateB = AgentAffordanceCatalog.ForKind(AgentAffordanceKind.Push) with { Token = "eat" };
        AuthoringDefinitionBundle bundle = new(
            Chemicals: ChemicalCatalog.All,
            CaPresets: new[] { badPreset },
            AgentAffordances: new[] { duplicateA, duplicateB },
            LabConfigs: Array.Empty<LabRunConfig>());

        AuthoringValidationResult result = bundle.Validate();

        Assert.Contains(result.Issues, issue => issue.Code == AuthoringValidationCode.DuplicateCaChannel);
        Assert.Contains(result.Issues, issue => issue.Code == AuthoringValidationCode.InvalidCaChannel);
        Assert.Contains(result.Issues, issue => issue.Code == AuthoringValidationCode.InvalidCaValue);
        Assert.Contains(result.Issues, issue => issue.Code == AuthoringValidationCode.DuplicateAffordanceToken);
    }

    [Fact]
    public void AuthoringValidator_ReportsInvalidLabConfigs()
    {
        LabRunConfig badConfig = new(
            Seed: 150,
            Ticks: -3,
            Population: Array.Empty<LabCreatureSeed>(),
            WorldPreset: new LabWorldPreset("", Temperature: 1.5f, Light: -0.2f, Radiation: 0.0f, AirQuality: 2.0f));
        AuthoringDefinitionBundle bundle = AuthoringDefinitionBundle.CreateBuiltIn() with
        {
            LabConfigs = new[] { badConfig }
        };

        AuthoringValidationResult result = bundle.Validate();

        Assert.Contains(result.Issues, issue => issue.Code == AuthoringValidationCode.InvalidLabTicks);
        Assert.Contains(result.Issues, issue => issue.Code == AuthoringValidationCode.EmptyLabPopulation);
        Assert.Contains(result.Issues, issue => issue.Code == AuthoringValidationCode.MissingName);
        Assert.Contains(result.Issues, issue => issue.Path == "labConfigs[0].worldPreset.airQuality");
    }

    [Fact]
    public void CaPresetDefinition_AppliesValidatedValuesToRoom()
    {
        var room = new Room { Id = 1, MetaRoomId = 1 };
        CaPresetDefinition preset = new(
            "warm-light",
            new[]
            {
                new CaPresetChannelValue(CaIndex.Temperature, 0.75f),
                new CaPresetChannelValue(CaIndex.Light, 0.40f)
            });

        preset.ApplyTo(room);

        Assert.Equal(0.75f, room.CA[CaIndex.Temperature]);
        Assert.Equal(0.40f, room.CA[CaIndex.Light]);
        Assert.Equal(0.0f, room.CA[CaIndex.Radiation]);
        Assert.DoesNotContain(preset.Validate().Issues, issue => issue.Severity == AuthoringValidationSeverity.Error);
    }
}
