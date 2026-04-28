using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Sim.Genome;

public sealed record C3DsGenomeImportOptions(
    int Sex = GeneConstants.MALE,
    byte Age = 0,
    int Variant = 0,
    string Moniker = "",
    BiochemistryCompatibilityMode BiochemistryMode = BiochemistryCompatibilityMode.C3DS);

public sealed record CreatureCompatibilityProfile(BiochemistryCompatibilityMode BiochemistryMode);

public sealed record GenomeCompatibilityGene(
    GeneIdentity Identity,
    GenePayloadKind PayloadKind,
    int PayloadLength,
    int ExpectedPayloadLength,
    string Message);

public sealed class GenomeCompatibilityReport
{
    public GenomeCompatibilityReport(
        IReadOnlyList<GeneValidationIssue> validationIssues,
        IReadOnlyList<GenomeCompatibilityGene> supportedGenes,
        IReadOnlyList<GenomeCompatibilityGene> preservedRawGenes)
    {
        ValidationIssues = validationIssues;
        SupportedGenes = supportedGenes;
        PreservedRawGenes = preservedRawGenes;
    }

    public IReadOnlyList<GeneValidationIssue> ValidationIssues { get; }
    public IReadOnlyList<GenomeCompatibilityGene> SupportedGenes { get; }
    public IReadOnlyList<GenomeCompatibilityGene> PreservedRawGenes { get; }
    public bool HasErrors => ValidationIssues.Any(issue => issue.Severity == GeneValidationSeverity.Error);
}

public sealed record GeneExpressionTrace(int FromAge, int ToAge, IReadOnlyList<GeneIdentity> ExpressedGenes);

public sealed class GenomeExpressionPlan
{
    public GenomeExpressionPlan(IReadOnlyList<GeneRecord> records)
    {
        Records = records;
    }

    public IReadOnlyList<GeneRecord> Records { get; }

    public IReadOnlyList<GeneRecord> GenesEligibleAt(byte age, int sex, int variant)
        => Records
            .Where(record => IsEligible(record, age, sex, variant))
            .ToArray();

    private static bool IsEligible(GeneRecord record, byte age, int sex, int variant)
    {
        GeneHeader header = record.Header;
        if (header.IsSilent)
            return false;
        if (header.MaleLinked && sex != GeneConstants.MALE)
            return false;
        if (header.FemaleLinked && sex != GeneConstants.FEMALE)
            return false;
        if (header.Variant != 0 && header.Variant != variant)
            return false;
        return header.SwitchOnAge == age;
    }
}

public sealed record C3DsGenomeImportResult(
    Genome Genome,
    IReadOnlyList<GeneRecord> Records,
    GenomeCompatibilityReport Report,
    GenomeExpressionPlan ExpressionPlan,
    CreatureCompatibilityProfile CompatibilityProfile);

public static class C3DsGenomeImporter
{
    public static C3DsGenomeImportResult ImportFile(string path, C3DsGenomeImportOptions? options = null)
    {
        options ??= new C3DsGenomeImportOptions();

        byte[] fileBytes = File.ReadAllBytes(path);
        var genome = new Genome(new Rng(0));
        GenomeReader.Load(genome, fileBytes, options.Sex, options.Age, options.Variant, options.Moniker);
        return ImportAttachedGenome(genome, options);
    }

    public static C3DsGenomeImportResult ImportRaw(byte[] rawGenome, C3DsGenomeImportOptions? options = null)
    {
        options ??= new C3DsGenomeImportOptions();

        var genome = new Genome(new Rng(0));
        genome.AttachBytes(rawGenome.ToArray(), options.Sex, options.Age, options.Variant, options.Moniker);
        return ImportAttachedGenome(genome, options);
    }

    private static C3DsGenomeImportResult ImportAttachedGenome(Genome genome, C3DsGenomeImportOptions options)
    {
        byte[] rawGenome = genome.AsSpan().ToArray();
        IReadOnlyList<GeneRecord> records = GeneDecoder.Decode(genome);
        IReadOnlyList<GeneValidationIssue> validationIssues = GeneValidator.ValidateRaw(rawGenome);
        var supported = new List<GenomeCompatibilityGene>();
        var preservedRaw = new List<GenomeCompatibilityGene>();

        foreach (GeneRecord record in records)
        {
            if (GeneSchemaCatalog.TryGet(record.Payload.Kind, out GenePayloadSchema? schema)
                && schema != null
                && record.Payload.Length == schema.ExactLength)
            {
                supported.Add(new GenomeCompatibilityGene(
                    record.Identity,
                    record.Payload.Kind,
                    record.Payload.Length,
                    schema.ExactLength,
                    "Payload matches the standard C3/DS schema."));
                continue;
            }

            preservedRaw.Add(new GenomeCompatibilityGene(
                record.Identity,
                record.Payload.Kind,
                record.Payload.Length,
                schema?.ExactLength ?? -1,
                schema == null
                    ? "No standard schema is defined; raw payload is preserved."
                    : "Payload length differs from the standard schema; raw payload is preserved."));
        }

        return new C3DsGenomeImportResult(
            genome,
            records,
            new GenomeCompatibilityReport(validationIssues, supported, preservedRaw),
            new GenomeExpressionPlan(records),
            new CreatureCompatibilityProfile(options.BiochemistryMode));
    }
}
