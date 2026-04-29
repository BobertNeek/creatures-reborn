using System;
using System.IO;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Lab;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class SimulationReportFormatterTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    [Fact]
    public void FormatSafetyReport_ProducesStableHumanReadableText()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(C3DsBiologyParityTests.Organ());
        GenomeSimulationSafetyReport report = GenomeSimulationSafetyValidator.Validate(genome);

        string text = SimulationReportFormatters.FormatSafetyReport(report);

        Assert.Contains("Safety report", text);
        Assert.Contains(nameof(GenomeSimulationSafetyCode.MissingBrainInterface), text);
    }

    [Fact]
    public void FormatChemicalAndLearningReports_IncludeSignalSources()
    {
        var trace = new BiochemistryTrace();
        trace.Record(ChemID.HungerForCarb, 0.8f, -0.3f, 0.5f, ChemicalDeltaSource.Metabolism, "satiety");
        ChemicalReinforcementTrace reinforcement = ChemicalReinforcementBus.Evaluate(trace, ChemicalReinforcementProfile.Default);
        var learning = new LearningTrace();
        learning.RecordChemicalReinforcement(ChemicalLearningAdapter.ToBrainInput(reinforcement, BrainLearningMode.ChemicalBusClassic));

        string reinforcementText = SimulationReportFormatters.FormatChemicalReinforcement(reinforcement);
        string learningText = SimulationReportFormatters.FormatLearningTrace(learning);

        Assert.Contains("Hunger", reinforcementText);
        Assert.Contains("chemical", learningText);
    }

    [Fact]
    public void FormatEcologyRun_AndJsonExport_IncludeLineageSummary()
    {
        EcologyRunResult result = new EcologyRunner().Run(new EcologyRunConfig(
            Seed: 800,
            Generations: 1,
            TicksPerGeneration: 2,
            Founders:
            [
                new LabCreatureSeed(Path.GetFullPath(StarterGenomePath), "report-mum", GeneConstants.FEMALE),
                new LabCreatureSeed(Path.GetFullPath(StarterGenomePath), "report-dad", GeneConstants.MALE)
            ]));

        string text = SimulationReportFormatters.FormatEcologyRun(result);
        string json = SimulationReportFormatters.ExportJournalJson(result.Journal);

        Assert.Contains("Ecology run", text);
        Assert.Contains("births", json);
        Assert.Contains("events", json);
    }
}
