using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Sim.Genome;

public enum GenomeExtensionGeneKind
{
    BrainProfileSelection = 0,
    LobeSizeMultiplier,
    TractDensityMultiplier,
    ChemicalReinforcementWeight,
    PlasticityLearningRate,
    ModuleEnablement,
    MemoryCapacity,
    CuriositySensitivity,
    SocialSensitivity,
    DangerSensitivity,
    LabOnlySafetyExemption
}

public sealed record GenomeExtensionGene(
    string Key,
    GenomeExtensionGeneKind Kind,
    float Value,
    bool Enabled = true);

public sealed record GenomeExtensionDocument(
    int SchemaVersion,
    IReadOnlyList<GenomeExtensionGene> Genes)
{
    public const int CurrentSchemaVersion = 1;

    public static GenomeExtensionDocument Empty { get; } = new(CurrentSchemaVersion, []);

    public string ToJson()
        => JsonSerializer.Serialize(this, JsonOptions);

    public static GenomeExtensionDocument FromJson(string json)
        => JsonSerializer.Deserialize<GenomeExtensionDocument>(json, JsonOptions)
           ?? Empty;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

public sealed record GenomeExtensionCrossoverReport(
    IReadOnlyList<string> InheritedFromMother,
    IReadOnlyList<string> InheritedFromFather);

public static class GenomeExtensionCrossover
{
    public static (GenomeExtensionDocument Child, GenomeExtensionCrossoverReport Report) Cross(
        GenomeExtensionDocument mother,
        GenomeExtensionDocument father,
        IRng rng)
    {
        var motherByKey = mother.Genes.ToDictionary(gene => gene.Key, StringComparer.OrdinalIgnoreCase);
        var fatherByKey = father.Genes.ToDictionary(gene => gene.Key, StringComparer.OrdinalIgnoreCase);
        var allKeys = motherByKey.Keys.Concat(fatherByKey.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var child = new List<GenomeExtensionGene>(allKeys.Length);
        var fromMother = new List<string>();
        var fromFather = new List<string>();
        foreach (string key in allKeys)
        {
            bool hasMother = motherByKey.TryGetValue(key, out GenomeExtensionGene? motherGene);
            bool hasFather = fatherByKey.TryGetValue(key, out GenomeExtensionGene? fatherGene);
            bool chooseMother = hasMother && (!hasFather || rng.Rnd(1) == 0);
            GenomeExtensionGene chosen = chooseMother ? motherGene! : fatherGene!;
            child.Add(chosen);
            if (chooseMother)
                fromMother.Add(key);
            else
                fromFather.Add(key);
        }

        return (
            new GenomeExtensionDocument(GenomeExtensionDocument.CurrentSchemaVersion, child),
            new GenomeExtensionCrossoverReport(fromMother, fromFather));
    }
}

public sealed record GenomeExtensionMutationReport(IReadOnlyList<string> MutatedGenes);

public static class GenomeExtensionMutation
{
    public static (GenomeExtensionDocument Mutated, GenomeExtensionMutationReport Report) Mutate(
        GenomeExtensionDocument document,
        IRng rng,
        float mutationChance = 0.05f,
        float mutationDegree = 0.10f)
    {
        mutationChance = Math.Clamp(mutationChance, 0.0f, 1.0f);
        mutationDegree = Math.Clamp(mutationDegree, 0.0f, 4.0f);
        var genes = new List<GenomeExtensionGene>(document.Genes.Count);
        var mutatedKeys = new List<string>();

        foreach (GenomeExtensionGene gene in document.Genes)
        {
            if (rng.RndFloat() >= mutationChance)
            {
                genes.Add(gene);
                continue;
            }

            float delta = (rng.RndFloat() * 2.0f - 1.0f) * mutationDegree;
            float value = ClampValue(gene.Kind, gene.Value + delta);
            GenomeExtensionGene mutated = gene with { Value = value };
            genes.Add(mutated);
            mutatedKeys.Add(gene.Key);
        }

        return (
            new GenomeExtensionDocument(document.SchemaVersion, genes),
            new GenomeExtensionMutationReport(mutatedKeys));
    }

    public static float ClampValue(GenomeExtensionGeneKind kind, float value)
        => kind switch
        {
            GenomeExtensionGeneKind.BrainProfileSelection => Math.Clamp(value, 0.0f, 1.0f),
            GenomeExtensionGeneKind.LobeSizeMultiplier => Math.Clamp(value, 0.25f, 4.0f),
            GenomeExtensionGeneKind.TractDensityMultiplier => Math.Clamp(value, 0.25f, 4.0f),
            GenomeExtensionGeneKind.ChemicalReinforcementWeight => Math.Clamp(value, 0.0f, 16.0f),
            GenomeExtensionGeneKind.PlasticityLearningRate => Math.Clamp(value, 0.0f, 4.0f),
            GenomeExtensionGeneKind.ModuleEnablement => Math.Clamp(value, 0.0f, 1.0f),
            GenomeExtensionGeneKind.MemoryCapacity => Math.Clamp(value, 0.25f, 8.0f),
            GenomeExtensionGeneKind.CuriositySensitivity => Math.Clamp(value, 0.0f, 8.0f),
            GenomeExtensionGeneKind.SocialSensitivity => Math.Clamp(value, 0.0f, 8.0f),
            GenomeExtensionGeneKind.DangerSensitivity => Math.Clamp(value, 0.0f, 8.0f),
            GenomeExtensionGeneKind.LabOnlySafetyExemption => Math.Clamp(value, 0.0f, 1.0f),
            _ => value
        };
}

public sealed record GenomeExtensionValidationIssue(string Key, string Message);

public static class GenomeExtensionValidator
{
    public static IReadOnlyList<GenomeExtensionValidationIssue> Validate(GenomeExtensionDocument document)
    {
        var issues = new List<GenomeExtensionValidationIssue>();
        if (document.SchemaVersion != GenomeExtensionDocument.CurrentSchemaVersion)
            issues.Add(new("schemaVersion", $"Unsupported extension schema version {document.SchemaVersion}."));

        foreach (GenomeExtensionGene gene in document.Genes)
        {
            if (string.IsNullOrWhiteSpace(gene.Key))
                issues.Add(new(gene.Key, "Extension gene key must be non-empty."));
            if (float.IsNaN(gene.Value) || float.IsInfinity(gene.Value))
                issues.Add(new(gene.Key, "Extension gene value must be finite."));

            float clamped = GenomeExtensionMutation.ClampValue(gene.Kind, gene.Value);
            if (Math.Abs(clamped - gene.Value) > 0.0001f)
                issues.Add(new(gene.Key, $"Extension gene value {gene.Value:0.###} is outside the valid range for {gene.Kind}."));
        }

        return issues;
    }
}

public static class GenomeExtensionPhenotypeSummarizer
{
    public static PhenotypeSection Summarize(GenomeExtensionDocument document)
    {
        var lines = new List<string>
        {
            "Modern extension genes configure opt-in expanded brain, reinforcement, plasticity, memory, and lab-only traits."
        };
        lines.AddRange(document.Genes
            .OrderBy(gene => gene.Kind)
            .ThenBy(gene => gene.Key, StringComparer.OrdinalIgnoreCase)
            .Select(gene => $"{gene.Kind}/{gene.Key}: value {gene.Value:0.###}, {(gene.Enabled ? "enabled" : "disabled")}"));
        return new PhenotypeSection("modern extensions", lines);
    }
}
