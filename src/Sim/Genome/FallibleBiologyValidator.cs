using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;

namespace CreaturesReborn.Sim.Genome;

public sealed record LifeSupportPathReport(
    bool HasOrgan,
    bool HasEnergyDependency,
    bool HasDeathOrInjuryRoute);

public sealed record DeathPathReport(
    bool HasEnergyFailurePath,
    bool HasInjuryPath,
    bool HasPainOrWoundedPath);

public sealed record OrganFailurePathReport(
    bool HasOrgan,
    bool HasOrganDamageRoute,
    bool HasOrganEnergyRoute);

public sealed class FallibleBiologyReport
{
    public FallibleBiologyReport(
        LifeSupportPathReport lifeSupport,
        DeathPathReport deathPath,
        OrganFailurePathReport organFailure,
        IReadOnlyList<GenomeSimulationSafetyIssue> issues)
    {
        LifeSupport = lifeSupport;
        DeathPath = deathPath;
        OrganFailure = organFailure;
        Issues = issues;
    }

    public LifeSupportPathReport LifeSupport { get; }
    public DeathPathReport DeathPath { get; }
    public OrganFailurePathReport OrganFailure { get; }
    public IReadOnlyList<GenomeSimulationSafetyIssue> Issues { get; }
    public bool HasHardInvalid => Issues.Any(issue => issue.Severity == GenomeSimulationSafetySeverity.HardInvalid);
}

public static class FallibleBiologyValidator
{
    public static FallibleBiologyReport Validate(
        IReadOnlyList<GeneRecord> expressedGenes,
        MinimumBiologyInterfaceSpec? spec = null)
    {
        spec ??= MinimumBiologyInterfaceSpec.Default;
        var issues = new List<GenomeSimulationSafetyIssue>();

        bool hasOrgan = expressedGenes.Any(record => record.Payload.Kind is GenePayloadKind.Organ or GenePayloadKind.BrainOrgan);
        bool hasEnergyDependency = expressedGenes
            .Where(IsBiochemistryRuntimeGene)
            .Any(record => PayloadMentions(record, ChemID.ATP) || PayloadMentions(record, ChemID.ADP));
        bool hasEnergyReaction = expressedGenes
            .Where(record => record.Payload.Kind == GenePayloadKind.BiochemistryReaction)
            .Any(record => PayloadMentions(record, ChemID.ATP) || PayloadMentions(record, ChemID.ADP));
        bool hasInjuryPath = expressedGenes
            .Where(IsBiochemistryRuntimeGene)
            .Any(record => PayloadMentions(record, ChemID.Injury));
        bool hasPainOrWoundedPath = expressedGenes
            .Where(IsBiochemistryRuntimeGene)
            .Any(record => PayloadMentions(record, ChemID.Pain) || PayloadMentions(record, ChemID.Wounded));
        bool hasDeathOrInjuryRoute = hasInjuryPath || hasPainOrWoundedPath || hasEnergyDependency;

        var lifeSupport = new LifeSupportPathReport(
            hasOrgan,
            hasEnergyDependency,
            hasDeathOrInjuryRoute);
        var deathPath = new DeathPathReport(
            HasEnergyFailurePath: hasEnergyDependency,
            HasInjuryPath: hasInjuryPath,
            HasPainOrWoundedPath: hasPainOrWoundedPath);
        var organFailure = new OrganFailurePathReport(
            hasOrgan,
            HasOrganDamageRoute: hasInjuryPath || hasPainOrWoundedPath,
            HasOrganEnergyRoute: hasEnergyDependency);

        if (spec.RequireOrgan && !hasOrgan)
        {
            issues.Add(new(
                GenomeSimulationSafetySeverity.HardInvalid,
                GenomeSimulationSafetyCode.NoFallibleLifeSupport,
                "Genome has no expressed organ host for fallible life-support chemistry."));
        }

        if (spec.RequireEnergyReaction && !hasEnergyReaction)
        {
            issues.Add(new(
                GenomeSimulationSafetySeverity.HardInvalid,
                GenomeSimulationSafetyCode.NoFallibleLifeSupport,
                "Genome has no ATP/ADP reaction evidence for energy-dependent life support."));
        }

        if (spec.RequireDeathOrInjuryRoute && !hasDeathOrInjuryRoute)
        {
            issues.Add(new(
                GenomeSimulationSafetySeverity.HardInvalid,
                GenomeSimulationSafetyCode.NoFallibleLifeSupport,
                "Genome has no conservative ATP, injury, pain, or wounded path that can degrade life support."));
        }

        return new FallibleBiologyReport(lifeSupport, deathPath, organFailure, issues);
    }

    private static bool IsBiochemistryRuntimeGene(GeneRecord record)
        => record.Payload.Kind is GenePayloadKind.BiochemistryReaction
            or GenePayloadKind.BiochemistryReceptor
            or GenePayloadKind.BiochemistryEmitter;

    private static bool PayloadMentions(GeneRecord record, int chemical)
        => record.Payload.Bytes.Contains((byte)chemical);
}
