using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Creature;

public enum HatchOutcome
{
    LivingCreature = 0,
    StillbornCreature = 1,
    Quarantined = 2
}

public enum StillbornReason
{
    HardInvalidGenome = 0,
    QuarantineOnlyGenome = 1
}

public sealed record EggGenomePayload(
    byte[] GenomeBytes,
    int Sex,
    int Variant,
    string Moniker,
    BiochemistryCompatibilityMode BiochemistryMode = BiochemistryCompatibilityMode.C3DS)
{
    public byte[] CopyGenomeBytes() => (byte[])GenomeBytes.Clone();
}

public sealed record HatchAttemptContext(
    string ChildMoniker,
    string? MotherMoniker,
    string? FatherMoniker,
    int BirthTick,
    int Generation);

public sealed record StillbornRecord(
    string ChildMoniker,
    string? MotherMoniker,
    string? FatherMoniker,
    byte[] GenomeBytes,
    int Sex,
    int Variant,
    int BirthTick,
    int Generation,
    StillbornReason Reason,
    GenomeSimulationSafetyReport SafetyReport);

public sealed record HatchResult(
    HatchOutcome Outcome,
    Creature? Creature,
    StillbornRecord? Stillborn,
    GenomeSimulationSafetyReport SafetyReport);

public static class CreatureHatchService
{
    public static HatchResult AttemptHatch(
        EggGenomePayload payload,
        HatchAttemptContext context,
        IRng rng,
        GenomeSimulationSafetyOptions? safetyOptions = null)
    {
        safetyOptions ??= new GenomeSimulationSafetyOptions(
            Sex: payload.Sex,
            Age: 0,
            Variant: payload.Variant);

        GenomeSimulationSafetyReport report = GenomeSimulationSafetyValidator.ValidateRaw(
            payload.GenomeBytes,
            safetyOptions);

        return CreateResultFromSafetyReport(payload, context, report, rng, safetyOptions);
    }

    public static HatchResult CreateResultFromSafetyReport(
        EggGenomePayload payload,
        HatchAttemptContext context,
        GenomeSimulationSafetyReport report,
        IRng rng,
        GenomeSimulationSafetyOptions? safetyOptions = null)
    {
        safetyOptions ??= new GenomeSimulationSafetyOptions(
            Sex: payload.Sex,
            Age: 0,
            Variant: payload.Variant);

        if (report.HasHardInvalid)
            return Stillborn(payload, context, report, StillbornReason.HardInvalidGenome);

        if (report.HasQuarantineOnly && !safetyOptions.AllowQuarantineOnlyToHatch)
            return Stillborn(payload, context, report, StillbornReason.QuarantineOnlyGenome);

        G genome = new(rng);
        genome.AttachBytes(payload.CopyGenomeBytes(), payload.Sex, age: 0, payload.Variant, payload.Moniker);

        Creature creature = Creature.CreateFromGenome(
            genome,
            rng,
            new CreatureImportOptions(
                payload.Sex,
                Age: 0,
                payload.Variant,
                payload.Moniker,
                payload.BiochemistryMode));

        return new HatchResult(HatchOutcome.LivingCreature, creature, null, report);
    }

    private static HatchResult Stillborn(
        EggGenomePayload payload,
        HatchAttemptContext context,
        GenomeSimulationSafetyReport report,
        StillbornReason reason)
    {
        var stillborn = new StillbornRecord(
            context.ChildMoniker,
            context.MotherMoniker,
            context.FatherMoniker,
            payload.CopyGenomeBytes(),
            payload.Sex,
            payload.Variant,
            context.BirthTick,
            context.Generation,
            reason,
            report);

        return new HatchResult(HatchOutcome.StillbornCreature, null, stillborn, report);
    }
}
