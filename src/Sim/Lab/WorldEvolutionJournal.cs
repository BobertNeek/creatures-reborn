using System.Collections.Generic;
using System.Linq;

namespace CreaturesReborn.Sim.Lab;

public enum NaturalSelectionEventKind
{
    Birth = 0,
    Stillbirth,
    Death,
    Reproduction,
    Mutation,
    Crossover,
    Starvation,
    ToxinDeath,
    OrganFailure,
    OldAgeDeath,
    SuccessfulFeeding,
    SocialInteraction,
    EggLaid,
    EggHatched,
    ChildSurvivedFirstLifeStage
}

public sealed record NaturalSelectionEvent(
    int Tick,
    NaturalSelectionEventKind Kind,
    string Moniker,
    string? RelatedMoniker = null,
    string Detail = "");

public sealed record SurvivalMetricFrame(
    int Tick,
    int LivingPopulation,
    int DeadCount,
    int StillbornCount);

public sealed record ReproductionMetricFrame(
    int Tick,
    int Births,
    int Stillbirths,
    int LivingChildren);

public sealed record LineageOutcomeSummary(
    int Births,
    int LivingBirths,
    int Stillbirths,
    int Deaths,
    int ReproductionEvents);

public sealed class WorldEvolutionJournal
{
    private readonly List<NaturalSelectionEvent> _events = new();
    private readonly List<SurvivalMetricFrame> _survivalFrames = new();
    private readonly List<ReproductionMetricFrame> _reproductionFrames = new();

    public IReadOnlyList<NaturalSelectionEvent> Events => _events;
    public IReadOnlyList<SurvivalMetricFrame> SurvivalFrames => _survivalFrames;
    public IReadOnlyList<ReproductionMetricFrame> ReproductionFrames => _reproductionFrames;

    public void Record(NaturalSelectionEvent selectionEvent)
        => _events.Add(selectionEvent);

    public void RecordSurvivalFrame(SurvivalMetricFrame frame)
        => _survivalFrames.Add(frame);

    public void RecordReproductionFrame(ReproductionMetricFrame frame)
        => _reproductionFrames.Add(frame);

    public LineageOutcomeSummary CreateSummary()
    {
        int births = _events.Count(e => e.Kind == NaturalSelectionEventKind.Birth);
        int stillbirths = _events.Count(e => e.Kind == NaturalSelectionEventKind.Stillbirth);
        return new LineageOutcomeSummary(
            births,
            LivingBirths: births,
            stillbirths,
            Deaths: _events.Count(e => e.Kind == NaturalSelectionEventKind.Death),
            ReproductionEvents: _events.Count(e => e.Kind == NaturalSelectionEventKind.Reproduction));
    }
}
