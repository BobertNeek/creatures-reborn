using System;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Brain;
using CreaturesReborn.Sim.Formats;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Sim.Creature;

using G        = CreaturesReborn.Sim.Genome.Genome;
using BC       = CreaturesReborn.Sim.Biochemistry.Biochemistry;
using BrainCls = CreaturesReborn.Sim.Brain.Brain;

/// <summary>
/// Top-level creature: composes Genome + Biochemistry + Brain + MotorFaculty.
/// <c>Tick()</c> runs one simulation step at the c2e world rate (nominally 20 Hz).
/// </summary>
public sealed class Creature
{
    public const int TicksPerAgeStep = 20 * 45;

    // -------------------------------------------------------------------------
    // Subsystems
    // -------------------------------------------------------------------------
    public  G            Genome       { get; }
    public  BC           Biochemistry { get; }
    public  BrainCls     Brain        { get; }
    public  MotorFaculty Motor        { get; }

    private readonly IRng _rng;
    private int _ageTickAccumulator;

    // Additional drive inputs contributed by the Godot layer (wall aversion, home smell,
    // social proximity, etc.).  Mixed into FeedDriveInputs each tick then reset to zero.
    private readonly float[] _additionalDriveInputs = new float[DriveId.NumDrives];

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------
    private Creature(G genome, IRng rng)
    {
        Genome = genome;
        _rng   = rng;

        // Biochemistry (owns the chemical array)
        Biochemistry = new BC();
        Biochemistry.ReadFromGenome(genome);

        float[] chemicals = Biochemistry.GetChemicalArray();

        // Brain (shares Biochemistry's chemical array)
        Brain = new BrainCls();
        Brain.ReadFromGenome(genome, rng);
        Brain.RegisterBiochemistry(chemicals);

        // Wire brain loci so neuroemitters can read neuron outputs
        Biochemistry.BrainLocusProvider = Brain;

        // Motor sits on top of the brain
        Motor = new MotorFaculty(Brain);

        // Process all instinct genes (c2e REM-sleep birth phase).
        // Instincts do initial reinforcement of the driv→decn tract weights so the
        // creature makes sensible decisions from tick 1.  Without this the tracts
        // start near-zero and the WTA winner is arbitrary.
        ProcessBirthInstincts();
    }

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------
    public static Creature LoadFromFile(
        string genPath,
        IRng rng,
        int sex = GeneConstants.MALE,
        byte age = 0,
        int variant = 0,
        string moniker = "")
    {
        // Boot structural brain/biochemistry from baby-safe genes, then apply
        // the requested runtime age for visual phenotype and reproduction.
        // The current readers do not yet rebuild late-switching organs/lobes
        // incrementally, so direct adult boot can drop the starter brain.
        var genome = GenomeReader.LoadNew(rng, genPath, sex, age: 0, variant, moniker);
        var creature = new Creature(genome, rng);
        creature.Genome.Age = age;
        return creature;
    }

    public static Creature CreateFromGenome(G genome, IRng rng)
        => new(genome, rng);

    // -------------------------------------------------------------------------
    // Tick
    // -------------------------------------------------------------------------
    /// <summary>
    /// One simulation step:
    ///   1. Push drive levels into "driv" lobe inputs
    ///   2. Biochemistry.Update()
    ///   3. Brain.Update()
    ///   4. MotorFaculty.Resolve() → CurrentVerb / CurrentNoun
    /// </summary>
    public void Tick()
    {
        TickInternal(collector: null);
    }

    public CreatureTickTrace Tick(CreatureTraceOptions options)
    {
        var collector = new CreatureTraceCollector(options);
        TickInternal(collector);
        return collector.Trace;
    }

    private void TickInternal(CreatureTraceCollector? collector)
    {
        if (collector != null)
        {
            collector.Trace.AgeBefore = Genome.Age;
            collector.Record(CreatureTickStage.Context, "Begin creature tick.");
        }

        FeedDriveInputs();
        collector?.Record(CreatureTickStage.Drives, "Copied creature drive loci into brain drive inputs.");

        BiochemistryTrace? biochemistryTrace = collector?.Options.IncludeBiochemistryTrace == true
            ? new BiochemistryTrace()
            : null;
        Biochemistry.Update(biochemistryTrace);
        if (collector != null)
        {
            collector.Trace.Biochemistry = biochemistryTrace;
            collector.Record(CreatureTickStage.Biochemistry, "Updated neuroemitters, organs, and chemical half-lives.");
        }

        if (collector?.Options.IncludeBrainSnapshot == true)
            collector.Trace.BrainBefore = Brain.CreateSnapshot();

        LearningTrace? learningTrace = collector?.Options.IncludeLearningTrace == true
            ? new LearningTrace()
            : null;
        Brain.Update(learningTrace);
        if (collector != null)
        {
            if (collector.Options.IncludeBrainSnapshot)
                collector.Trace.BrainAfter = Brain.CreateSnapshot();
            collector.Trace.Learning = learningTrace;
            collector.Record(CreatureTickStage.Brain, "Updated brain lobes, tracts, instincts, and modules.");
            collector.Record(CreatureTickStage.Learning, "Learning signals are available through brain and biochemistry traces when enabled.");
        }

        Motor.Resolve();
        if (collector != null)
        {
            collector.Trace.MotorVerb = Motor.CurrentVerb;
            collector.Trace.MotorNoun = Motor.CurrentNoun;
            collector.Record(CreatureTickStage.Motor, "Resolved motor verb, noun, gait, and pose from brain outputs.");
            collector.Record(CreatureTickStage.Action, "Action application is handled by caller-facing world/Godot layers.");
            collector.Record(CreatureTickStage.Reproduction, "Reproduction state is unchanged by core tick unless external systems invoke it.");
        }

        AdvanceAge(1);
        if (collector != null)
        {
            collector.Trace.AgeAfter = Genome.Age;
            collector.Record(CreatureTickStage.Age, "Advanced age accumulator by one tick.");
        }
    }

    // -------------------------------------------------------------------------
    // Chemical access
    // -------------------------------------------------------------------------
    public float GetChemical(int idx)          => Biochemistry.GetChemical(idx);
    public void  SetChemical(int idx, float v) => Biochemistry.SetChemical(idx, v);

    public void InjectChemical(int idx, float amount)
        => Biochemistry.AddChemical(idx, amount);

    public CreatureEnvironmentResponse ApplyEnvironment(Room room, BiochemistryTrace? trace = null)
        => ApplyEnvironment(CreatureEnvironmentContext.FromRoom(room), trace);

    public CreatureEnvironmentResponse ApplyEnvironment(
        CreatureEnvironmentContext context,
        BiochemistryTrace? trace = null)
    {
        CreatureEnvironmentResponse response = CreatureEnvironmentEffects.Evaluate(context);
        Biochemistry.ApplyEnvironmentSignals(
            response.Hotness,
            response.Coldness,
            response.Light,
            response.Radiation,
            response.ComfortNeed,
            response.Stress,
            trace);

        AddDriveInput(DriveId.Hotness, response.Hotness);
        AddDriveInput(DriveId.Coldness, response.Coldness);
        AddDriveInput(DriveId.Comfort, response.ComfortNeed);
        AddDriveInput(DriveId.Fear, response.Stress);

        return response;
    }

    public float ApplyAirQuality(float airQuality, BiochemistryTrace? trace = null)
    {
        float suffocation = Biochemistry.ApplyAirQuality(airQuality, trace);
        AddDriveInput(DriveId.Comfort, suffocation);
        AddDriveInput(DriveId.Fear, suffocation);
        return suffocation;
    }

    // -------------------------------------------------------------------------
    // Drive level readout
    // -------------------------------------------------------------------------
    public float GetDriveLevel(int driveId)
    {
        Lobe? lobe = FindLobe(BrainCls.TokenFromString("driv"));
        if (lobe == null || driveId >= lobe.GetNoOfNeurons()) return 0.0f;
        return lobe.GetNeuronState(driveId, NeuronVar.State);
    }

    public CreatureAgeStage AgeStage => CreatureAge.StageFromAge(Genome.Age);

    public void AdvanceAge(int tickCount)
    {
        if (tickCount <= 0 || Genome.Age == byte.MaxValue) return;

        _ageTickAccumulator += tickCount;
        while (_ageTickAccumulator >= TicksPerAgeStep && Genome.Age < byte.MaxValue)
        {
            _ageTickAccumulator -= TicksPerAgeStep;
            Genome.Age++;
        }

        if (Genome.Age == byte.MaxValue)
            _ageTickAccumulator = 0;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------
    private void ProcessBirthInstincts()
    {
        Brain.SetWhetherToProcessInstincts(true);
        // Each instinct takes at most a handful of ticks; 512 is well above the
        // number of instinct genes in any stock C3/DS genome.
        int guard = 512;
        while (Brain.IsProcessingInstincts && guard-- > 0)
            Brain.Update();
    }

    /// <summary>
    /// Add an extra signal to a drive neuron for this tick only.
    /// Positive values boost the drive; negative values suppress it.
    /// The array is reset to zero after every call to <see cref="Tick"/>.
    /// Use this from the Godot layer for wall-aversion, home-smell, social proximity, etc.
    /// </summary>
    public void AddDriveInput(int driveId, float amount)
    {
        if ((uint)driveId >= (uint)DriveId.NumDrives) return;
        _additionalDriveInputs[driveId] = Math.Clamp(
            _additionalDriveInputs[driveId] + amount, -1.0f, 1.0f);
    }

    private void FeedDriveInputs()
    {
        // Bridge the gap between Biochemistry and Brain:
        // Genome receptor genes write chemical concentrations into the creature-locus
        // table at tissue=Drives. Here we copy those locus values into the driv lobe's
        // neuron inputs so the brain can see them.  This mirrors what c2e's sensory
        // faculty does when it copies receptor outputs into TISSUE_DRIVES.
        //
        // Additionally mix in any contextual drive supplements queued by the Godot layer
        // (wall aversion, home smell, social proximity) then reset the supplement array.
        int drivToken = BrainCls.TokenFromString("driv");
        for (int d = 0; d < DriveId.NumDrives; d++)
        {
            float biochem  = Biochemistry.GetCreatureLocus((int)CreatureTissue.Drives, d).Value;
            float combined = Math.Clamp(biochem + _additionalDriveInputs[d], 0.0f, 1.0f);
            Brain.SetInput(drivToken, d, combined);
            _additionalDriveInputs[d] = 0.0f;
        }
    }

    private Lobe? FindLobe(int token)
    {
        for (int i = 0; i < Brain.LobeCount; i++)
        {
            Lobe? l = Brain.GetLobe(i);
            if (l?.Token == token) return l;
        }
        return null;
    }
}
