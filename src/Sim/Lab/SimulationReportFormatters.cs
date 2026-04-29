using System.Linq;
using System.Text;
using System.Text.Json;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;

namespace CreaturesReborn.Sim.Lab;

public static class SimulationReportFormatters
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string FormatSafetyReport(GenomeSimulationSafetyReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Safety report: hardInvalid={report.HasHardInvalid}, quarantineOnly={report.HasQuarantineOnly}, canHatch={report.CanHatch}");
        foreach (GenomeSimulationSafetyIssue issue in report.Issues)
            builder.AppendLine($"- {issue.Severity} {issue.Code}: {issue.Message}");
        return builder.ToString().TrimEnd();
    }

    public static string FormatStillbornReport(StillbornRecord record)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Stillborn: {record.ChildMoniker}");
        builder.AppendLine($"Parents: {record.MotherMoniker ?? "unknown"} x {record.FatherMoniker ?? "unknown"}");
        builder.AppendLine($"Generation: {record.Generation}, tick: {record.BirthTick}, reason: {record.Reason}");
        builder.AppendLine(FormatSafetyReport(record.SafetyReport));
        return builder.ToString().TrimEnd();
    }

    public static string FormatChemicalReinforcement(ChemicalReinforcementTrace trace)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Chemical reinforcement signals: {trace.Signals.Count}");
        foreach (ChemicalReinforcementSignal signal in trace.Signals.OrderByDescending(signal => signal.Strength))
        {
            builder.AppendLine(
                $"- {signal.Valence} {signal.Domain} {signal.Chemical.DisplayName} delta={signal.Delta:0.###} strength={signal.Strength:0.###} reason={signal.Reason}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatLearningTrace(LearningTrace trace)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Learning trace: classic={trace.Reinforcements.Count}, chemical={trace.ChemicalSignals.Count}, instincts={trace.Instincts.Count}");
        foreach (ChemicalReinforcementSignal signal in trace.ChemicalSignals.OrderByDescending(signal => signal.Strength).Take(8))
            builder.AppendLine($"- chemical {signal.Valence} {signal.Domain} {signal.Chemical.DisplayName} strength={signal.Strength:0.###}");
        foreach (ReinforcementTrace reinforcement in trace.Reinforcements.Take(8))
            builder.AppendLine($"- classic {reinforcement.Kind} tract={reinforcement.TractIndex} chem={reinforcement.ChemicalId} {reinforcement.BeforeWeight:0.###}->{reinforcement.AfterWeight:0.###}");
        return builder.ToString().TrimEnd();
    }

    public static string FormatEvolutionJournal(WorldEvolutionJournal journal)
    {
        LineageOutcomeSummary summary = journal.CreateSummary();
        var builder = new StringBuilder();
        builder.AppendLine($"Evolution journal: births={summary.Births}, stillbirths={summary.Stillbirths}, deaths={summary.Deaths}, reproductions={summary.ReproductionEvents}");
        foreach (NaturalSelectionEvent selectionEvent in journal.Events.Take(16))
            builder.AppendLine($"- tick {selectionEvent.Tick}: {selectionEvent.Kind} {selectionEvent.Moniker} {selectionEvent.Detail}".TrimEnd());
        return builder.ToString().TrimEnd();
    }

    public static string FormatEcologyRun(EcologyRunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Ecology run: generations={result.Summary.GenerationsRun}, living={result.Summary.LivingPopulation}, stillborn={result.Summary.StillbornCount}, deaths={result.Summary.DeathCount}, extinct={result.Summary.Extinct}");
        builder.AppendLine(FormatEvolutionJournal(result.Journal));
        return builder.ToString().TrimEnd();
    }

    public static string ExportJournalJson(WorldEvolutionJournal journal)
        => JsonSerializer.Serialize(new
        {
            events = journal.Events,
            survivalFrames = journal.SurvivalFrames,
            reproductionFrames = journal.ReproductionFrames,
            summary = journal.CreateSummary()
        }, JsonOptions);

    public static string ExportEcologyJson(EcologyRunResult result)
        => JsonSerializer.Serialize(result, JsonOptions);
}
