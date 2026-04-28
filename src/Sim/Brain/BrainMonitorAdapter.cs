using System;
using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreatureSim = CreaturesReborn.Sim.Creature.Creature;

namespace CreaturesReborn.Sim.Brain;

public sealed record BrainMonitorOptions(
    int MaxNeuronsPerLobe = 16,
    int MaxDendritesPerTract = 16,
    int MaxTableRows = 512,
    IReadOnlyList<int>? WatchedChemicals = null)
{
    public IReadOnlyList<int> ResolveWatchedChemicals()
        => WatchedChemicals ?? DefaultWatchedChemicals;

    public static IReadOnlyList<int> DefaultWatchedChemicals { get; } =
    [
        ChemID.ATP,
        ChemID.Glycogen,
        ChemID.Reward,
        ChemID.Punishment,
        ChemID.Pain,
        ChemID.Tiredness,
        ChemID.Sleepiness
    ];
}

public sealed record BrainMonitorFrame(
    IReadOnlyList<BrainLobeMonitorRow> Lobes,
    IReadOnlyList<BrainNeuronMonitorRow> Neurons,
    IReadOnlyList<BrainTractMonitorRow> Tracts,
    IReadOnlyList<BrainModuleDescriptor> Modules,
    IReadOnlyList<BrainPort> Ports,
    IReadOnlyList<BrainDriveMonitorRow> Drives,
    IReadOnlyList<BrainChemicalMonitorRow> Chemicals,
    int MotorVerb,
    int MotorNoun)
{
    public static BrainMonitorFrame Create(CreatureSim creature, BrainMonitorOptions? options = null)
    {
        options ??= new BrainMonitorOptions();
        BrainSnapshot snapshot = creature.Brain.CreateSnapshot(new BrainSnapshotOptions(
            Math.Max(0, options.MaxNeuronsPerLobe),
            Math.Max(0, options.MaxDendritesPerTract)));

        var lobes = snapshot.Lobes.Select(CreateLobeRow).ToArray();
        var neurons = snapshot.Lobes
            .SelectMany(CreateNeuronRows)
            .Take(Math.Max(0, options.MaxTableRows))
            .ToArray();
        var tracts = snapshot.Tracts
            .Select(CreateTractRow)
            .Take(Math.Max(0, options.MaxTableRows))
            .ToArray();
        var drives = CreateDriveRows(creature).ToArray();
        var chemicals = CreateChemicalRows(creature, options.ResolveWatchedChemicals()).ToArray();

        return new BrainMonitorFrame(
            lobes,
            neurons,
            tracts,
            snapshot.Modules,
            BrainPortRegistry.CreateDefault().Ports,
            drives,
            chemicals,
            creature.Motor.CurrentVerb,
            creature.Motor.CurrentNoun);
    }

    private static BrainLobeMonitorRow CreateLobeRow(LobeSnapshot lobe)
    {
        float activation = 0.0f;
        if (lobe.Neurons.Count > 0)
            activation = lobe.Neurons.Max(neuron => Math.Clamp(neuron.States[NeuronVar.State], -1.0f, 1.0f));

        return new BrainLobeMonitorRow(
            lobe.Index,
            lobe.Token,
            lobe.TokenText,
            lobe.TissueId,
            lobe.X,
            lobe.Y,
            lobe.Width,
            lobe.Height,
            lobe.NeuronCount,
            lobe.WinningNeuronId,
            activation,
            lobe.UpdateAtTime);
    }

    private static IEnumerable<BrainNeuronMonitorRow> CreateNeuronRows(LobeSnapshot lobe)
    {
        foreach (NeuronSnapshot neuron in lobe.Neurons)
        {
            yield return new BrainNeuronMonitorRow(
                lobe.Index,
                lobe.TokenText,
                neuron.Index,
                neuron.States[NeuronVar.State],
                neuron.States[NeuronVar.Output],
                neuron.States.ToArray());
        }
    }

    private static BrainTractMonitorRow CreateTractRow(TractSnapshot tract)
        => new(
            tract.Index,
            tract.SourceToken,
            tract.SourceTokenText,
            tract.DestinationToken,
            tract.DestinationTokenText,
            tract.DendriteCount,
            tract.STtoLTRate,
            tract.Dendrites.Select(dendrite => new BrainDendriteMonitorRow(
                dendrite.Index,
                dendrite.SourceNeuronId,
                dendrite.DestinationNeuronId,
                dendrite.Weights[DendriteVar.WeightST],
                dendrite.Weights[DendriteVar.WeightLT],
                dendrite.Weights.ToArray())).ToArray());

    private static IEnumerable<BrainDriveMonitorRow> CreateDriveRows(CreatureSim creature)
    {
        for (int i = 0; i < DriveId.NumDrives; i++)
            yield return new BrainDriveMonitorRow(i, DriveName(i), creature.GetDriveLevel(i));
    }

    private static IEnumerable<BrainChemicalMonitorRow> CreateChemicalRows(CreatureSim creature, IReadOnlyList<int> watched)
    {
        foreach (int id in watched)
        {
            ChemicalDefinition definition = ChemicalCatalog.Get(id);
            yield return new BrainChemicalMonitorRow(id, definition.Token, definition.DisplayName, creature.GetChemical(id));
        }
    }

    private static string DriveName(int id)
        => id switch
        {
            DriveId.Pain => "Pain",
            DriveId.HungerForProtein => "Hunger Protein",
            DriveId.HungerForCarb => "Hunger Carb",
            DriveId.HungerForFat => "Hunger Fat",
            DriveId.Coldness => "Coldness",
            DriveId.Hotness => "Hotness",
            DriveId.Tiredness => "Tiredness",
            DriveId.Sleepiness => "Sleepiness",
            DriveId.Loneliness => "Loneliness",
            DriveId.Crowdedness => "Crowdedness",
            DriveId.Fear => "Fear",
            DriveId.Boredom => "Boredom",
            DriveId.Anger => "Anger",
            DriveId.SexDrive => "Sex Drive",
            DriveId.Comfort => "Comfort",
            DriveId.Up => "Up",
            DriveId.Down => "Down",
            DriveId.Exit => "Exit",
            DriveId.Enter => "Enter",
            DriveId.Wait => "Wait",
            _ => $"Drive {id}"
        };
}

public sealed record BrainLobeMonitorRow(
    int Index,
    int Token,
    string TokenText,
    int TissueId,
    int X,
    int Y,
    int Width,
    int Height,
    int NeuronCount,
    int WinningNeuronId,
    float Activation,
    int UpdateAtTime);

public sealed record BrainNeuronMonitorRow(
    int LobeIndex,
    string LobeToken,
    int NeuronIndex,
    float State,
    float Output,
    float[] States);

public sealed record BrainTractMonitorRow(
    int Index,
    int SourceToken,
    string SourceTokenText,
    int DestinationToken,
    string DestinationTokenText,
    int DendriteCount,
    float STtoLTRate,
    IReadOnlyList<BrainDendriteMonitorRow> Dendrites);

public sealed record BrainDendriteMonitorRow(
    int Index,
    int SourceNeuronId,
    int DestinationNeuronId,
    float ShortTermWeight,
    float LongTermWeight,
    float[] Weights);

public sealed record BrainDriveMonitorRow(int Id, string Name, float Value);

public sealed record BrainChemicalMonitorRow(int Id, string Token, string DisplayName, float Value);

public sealed record BrainMonitorSeries(int Id, string Name, IReadOnlyList<float> Values);

public sealed class BrainMonitorHistory
{
    private readonly int _capacity;
    private readonly Dictionary<int, Queue<float>> _chemicals = new();
    private readonly Dictionary<int, string> _chemicalNames = new();
    private readonly Dictionary<int, Queue<float>> _drives = new();
    private readonly Dictionary<int, string> _driveNames = new();

    public BrainMonitorHistory(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "History capacity must be positive.");
        _capacity = capacity;
    }

    public IReadOnlyList<BrainMonitorSeries> ChemicalSeries
        => ToSeries(_chemicals, _chemicalNames);

    public IReadOnlyList<BrainMonitorSeries> DriveSeries
        => ToSeries(_drives, _driveNames);

    public void Record(BrainMonitorFrame frame)
    {
        foreach (BrainChemicalMonitorRow chemical in frame.Chemicals)
        {
            _chemicalNames[chemical.Id] = chemical.DisplayName;
            Append(_chemicals, chemical.Id, chemical.Value);
        }

        foreach (BrainDriveMonitorRow drive in frame.Drives)
        {
            _driveNames[drive.Id] = drive.Name;
            Append(_drives, drive.Id, drive.Value);
        }
    }

    private void Append(Dictionary<int, Queue<float>> target, int id, float value)
    {
        if (!target.TryGetValue(id, out Queue<float>? queue))
        {
            queue = new Queue<float>(_capacity);
            target[id] = queue;
        }

        queue.Enqueue(value);
        while (queue.Count > _capacity)
            queue.Dequeue();
    }

    private static IReadOnlyList<BrainMonitorSeries> ToSeries(
        Dictionary<int, Queue<float>> values,
        Dictionary<int, string> names)
        => values
            .OrderBy(pair => pair.Key)
            .Select(pair => new BrainMonitorSeries(
                pair.Key,
                names.TryGetValue(pair.Key, out string? name) ? name : pair.Key.ToString(),
                pair.Value.ToArray()))
            .ToArray();
}
