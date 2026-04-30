using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Util;
using CreatureSim = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Sim.Genome;

public sealed record C3DsFixtureGenome(string Path, byte[] Bytes)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

public sealed record C3DsFixtureSet(string RootPath, IReadOnlyList<C3DsFixtureGenome> Genomes)
{
    public static C3DsFixtureSet Discover(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return new C3DsFixtureSet(rootPath ?? string.Empty, []);

        string[] paths = Directory.GetFiles(rootPath, "*.gen", SearchOption.AllDirectories)
            .OrderBy(path => System.IO.Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new C3DsFixtureSet(
            rootPath,
            paths.Select(path => new C3DsFixtureGenome(path, File.ReadAllBytes(path))).ToArray());
    }

    public static C3DsFixtureSet FromEnvironment(string variableName = "CREATURES_C3DS_FIXTURES")
        => Discover(Environment.GetEnvironmentVariable(variableName));
}

public enum C3DsParityIssueKind
{
    Unsupported,
    DecodedButNotExpressed,
    ExpressedButUnverified,
    BehaviorMismatch
}

public sealed record C3DsParityIssue(C3DsParityIssueKind Kind, string Message, GeneIdentity? Gene = null);

public sealed record C3DsParityReport(
    string GenomeName,
    bool ImportSucceeded,
    bool BootSucceeded,
    BiochemistryCompatibilityMode BiochemistryMode,
    int GeneCount,
    int SupportedGeneCount,
    int PreservedRawGeneCount,
    int UnsupportedGeneCount,
    int StimulusGeneCount,
    int OrganCount,
    int NeuroEmitterCount,
    int NonZeroChemicalCount,
    int LobeCount,
    int TractCount,
    int InstinctsRemaining,
    int FirstTickChemicalDeltaCount,
    IReadOnlyList<C3DsParityIssue> Issues)
{
    public static C3DsParityReport Create(string genomeName, byte[] rawGenome)
    {
        var issues = new List<C3DsParityIssue>();
        C3DsGenomeImportResult import;
        try
        {
            import = C3DsGenomeImporter.ImportRaw(
                rawGenome,
                new C3DsGenomeImportOptions(Moniker: System.IO.Path.GetFileNameWithoutExtension(genomeName)));
        }
        catch (Exception ex)
        {
            issues.Add(new C3DsParityIssue(C3DsParityIssueKind.BehaviorMismatch, $"Import failed: {ex.Message}"));
            return Empty(genomeName, importSucceeded: false, issues);
        }

        foreach (GenomeCompatibilityGene raw in import.Report.PreservedRawGenes)
        {
            issues.Add(new C3DsParityIssue(
                C3DsParityIssueKind.Unsupported,
                raw.Message,
                raw.Identity));
        }

        foreach (GeneValidationIssue validation in import.Report.ValidationIssues)
        {
            if (validation.Severity == GeneValidationSeverity.Error)
            {
                issues.Add(new C3DsParityIssue(
                    C3DsParityIssueKind.BehaviorMismatch,
                    validation.Message));
            }
        }

        try
        {
            CreatureSim creature = CreatureSim.CreateFromGenome(
                import.Genome,
                new Rng(10_501),
                new CreatureImportOptions(
                    import.Genome.Sex,
                    import.Genome.Age,
                    import.Genome.Variant,
                    import.Genome.Moniker,
                    BiochemistryCompatibilityMode.C3DS));
            CreatureTickTrace trace = creature.Tick(new CreatureTraceOptions(
                IncludeBiochemistryTrace: true,
                IncludeBrainSnapshot: true,
                IncludeLearningTrace: true));
            if (trace.Biochemistry != null)
            {
                foreach (ChemicalDelta delta in trace.Biochemistry.Deltas)
                {
                    if (delta.Source is ChemicalDeltaSource.Metabolism
                        or ChemicalDeltaSource.Fatigue
                        or ChemicalDeltaSource.InjuryRecovery
                        or ChemicalDeltaSource.Environment
                        or ChemicalDeltaSource.Respiration
                        or ChemicalDeltaSource.Immune
                        or ChemicalDeltaSource.Toxin)
                    {
                        issues.Add(new C3DsParityIssue(
                            C3DsParityIssueKind.BehaviorMismatch,
                            $"C3/DS mode emitted modern helper chemical delta from {delta.Source}."));
                    }
                }
            }
            BrainSnapshot brain = creature.Brain.CreateSnapshot(new BrainSnapshotOptions(
                MaxNeuronsPerLobe: 0,
                MaxDendritesPerTract: 0));
            float[] chemicals = creature.Biochemistry.GetChemicalArray();

            return new C3DsParityReport(
                genomeName,
                ImportSucceeded: true,
                BootSucceeded: true,
                creature.Biochemistry.CompatibilityMode,
                import.Records.Count,
                import.Report.SupportedGenes.Count,
                import.Report.PreservedRawGenes.Count,
                import.Report.PreservedRawGenes.Count,
                import.Records.Count(record => record.Payload.Kind == GenePayloadKind.CreatureStimulus),
                creature.Biochemistry.OrganCount,
                creature.Biochemistry.NeuroEmitterCount,
                chemicals.Count(value => value != 0.0f),
                brain.Lobes.Count,
                brain.Tracts.Count,
                brain.InstinctsRemaining,
                trace.Biochemistry?.Deltas.Count ?? 0,
                issues);
        }
        catch (Exception ex)
        {
            issues.Add(new C3DsParityIssue(C3DsParityIssueKind.BehaviorMismatch, $"Boot/tick failed: {ex.Message}"));
            return new C3DsParityReport(
                genomeName,
                ImportSucceeded: true,
                BootSucceeded: false,
                import.CompatibilityProfile.BiochemistryMode,
                import.Records.Count,
                import.Report.SupportedGenes.Count,
                import.Report.PreservedRawGenes.Count,
                import.Report.PreservedRawGenes.Count,
                import.Records.Count(record => record.Payload.Kind == GenePayloadKind.CreatureStimulus),
                OrganCount: 0,
                NeuroEmitterCount: 0,
                NonZeroChemicalCount: 0,
                LobeCount: 0,
                TractCount: 0,
                InstinctsRemaining: 0,
                FirstTickChemicalDeltaCount: 0,
                issues);
        }
    }

    private static C3DsParityReport Empty(string genomeName, bool importSucceeded, IReadOnlyList<C3DsParityIssue> issues)
        => new(
            genomeName,
            importSucceeded,
            BootSucceeded: false,
            BiochemistryCompatibilityMode.C3DS,
            GeneCount: 0,
            SupportedGeneCount: 0,
            PreservedRawGeneCount: 0,
            UnsupportedGeneCount: 0,
            StimulusGeneCount: 0,
            OrganCount: 0,
            NeuroEmitterCount: 0,
            NonZeroChemicalCount: 0,
            LobeCount: 0,
            TractCount: 0,
            InstinctsRemaining: 0,
            FirstTickChemicalDeltaCount: 0,
            issues);
}

public static class C3DsParityReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ToJson(IReadOnlyList<C3DsParityReport> reports)
        => JsonSerializer.Serialize(reports, JsonOptions);

    public static string ToMarkdown(IReadOnlyList<C3DsParityReport> reports)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# C3/DS Parity Report");
        builder.AppendLine();
        builder.AppendLine($"Generated reports: {reports.Count}");
        builder.AppendLine();
        foreach (C3DsParityReport report in reports)
        {
            builder.AppendLine($"## {report.GenomeName}");
            builder.AppendLine();
            builder.AppendLine($"- Import: {(report.ImportSucceeded ? "ok" : "failed")}");
            builder.AppendLine($"- Boot: {(report.BootSucceeded ? "ok" : "failed")}");
            builder.AppendLine($"- Genes: {report.GeneCount}, supported: {report.SupportedGeneCount}, unsupported: {report.UnsupportedGeneCount}");
            builder.AppendLine($"- Biology: organs={report.OrganCount}, neuroemitters={report.NeuroEmitterCount}, non-zero chemicals={report.NonZeroChemicalCount}");
            builder.AppendLine($"- Brain: lobes={report.LobeCount}, tracts={report.TractCount}, instincts={report.InstinctsRemaining}");
            builder.AppendLine($"- First tick chemical deltas: {report.FirstTickChemicalDeltaCount}");
            if (report.Issues.Count > 0)
            {
                builder.AppendLine("- Issues:");
                foreach (C3DsParityIssue issue in report.Issues)
                    builder.AppendLine($"  - {issue.Kind}: {issue.Message}");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public static void WriteAll(string directory, IReadOnlyList<C3DsParityReport> reports)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "c3ds-parity.json"), ToJson(reports));
        File.WriteAllText(Path.Combine(directory, "c3ds-parity.md"), ToMarkdown(reports));
    }
}
