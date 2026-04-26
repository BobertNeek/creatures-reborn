using System;
using CreaturesReborn.Sim.Genome;

namespace CreaturesReborn.Sim.Biochemistry;

/// <summary>
/// Container for all biochemistry: 256-chemical array, organs (reactions + receptors + emitters),
/// neuroemitters, and half-life decay rates.
/// Direct port of c2e's <c>Biochemistry</c> class (Biochemistry.h / Biochemistry.cpp).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Update"/> must be called once per world tick (20 Hz in c2e). The update order
/// mirrors c2e Biochemistry::Update (lines 320-352):
/// 1. NeuroEmitters (multiply brain outputs → emit chemicals)
/// 2. Organ.Update() for each organ (emitters + reactions + receptors)
/// 3. Half-life decay for all 256 chemicals
/// </para>
/// <para>
/// The creature locus table (<see cref="GetCreatureLocus"/>) stores 2-D FloatLocus values
/// keyed by (tissue, locus). Receptors and emitters bind to these after <see cref="ReadFromGenome"/>.
/// Drive loci (TISSUE_DRIVES) are the primary I/O point between biochemistry and the brain.
/// </para>
/// </remarks>
public sealed class Biochemistry
{
    private const float HungerAdjustmentRate = 0.080f;
    private const float StorageToAtpRate = 0.040f;
    private const float AwakeTirednessRise = 0.006f;
    private const float SleepinessRiseRate = 0.020f;
    private const float SleepThreshold = 0.75f;
    private const float SleepTirednessRecovery = 0.045f;
    private const float SleepinessRecovery = 0.060f;
    private const float SleepAtpRecovery = 0.025f;
    private const int InvoluntarySleepLocusOffset = 5;
    private const float PainFromInjuryRate = 0.050f;
    private const float MaxPainFromInjuryPerTick = 0.040f;
    private const float BasePainRecovery = 0.035f;
    private const float EnvironmentAdjustmentRate = 0.120f;
    private const float TemperaturePunishmentRate = 0.045f;
    private const float RadiationPunishmentRate = 0.050f;
    private const float RadiationFearRate = 0.040f;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private readonly float[] _chemConcs    = new float[BiochemConst.NUMCHEM];
    private readonly float[] _decayRates   = new float[BiochemConst.NUMCHEM];

    private readonly NeuroEmitter[] _neuroEmitters = new NeuroEmitter[BiochemConst.MAX_NEUROEMITTERS];
    private int _numNeuroEmitters;

    private readonly Organ[] _organs = new Organ[BiochemConst.MAXORGANS];
    private int _numOrgans;

    // Creature locus table: [tissue, locus] → FloatLocus
    private readonly FloatLocus[,] _creatureLoci =
        new FloatLocus[(int)CreatureTissue.Count, BiochemConst.MAX_LOCI_PER_TISSUE];

    private BiochemistryTrace? _activeTrace;

    /// <summary>
    /// Pluggable brain locus provider.  Set this when the Brain is constructed so that
    /// neuroemitters and emitters can read real neuron outputs.
    /// If null, brain loci default to 1.0 (fully active stub).
    /// </summary>
    public IBrainLocusProvider? BrainLocusProvider { get; set; }

    /// <summary>
    /// Running total of ATP demanded per tick by all active organs.
    /// Written by <see cref="Organ.InitEnergyCost"/>.
    /// </summary>
    public float ATPRequirement { get; set; }

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------
    public Biochemistry()
    {
        // Initialise creature locus table.
        for (int t = 0; t < (int)CreatureTissue.Count; t++)
            for (int l = 0; l < BiochemConst.MAX_LOCI_PER_TISSUE; l++)
                _creatureLoci[t, l] = new FloatLocus();

        // LOC_CONST (sensorimotor tissue) must always be 1.0 — it's a constant signal source.
        if (SensorimotorEmitterLocus.Const < BiochemConst.MAX_LOCI_PER_TISSUE)
            _creatureLoci[(int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Const].Value = 1.0f;

        for (int i = 0; i < BiochemConst.MAX_NEUROEMITTERS; i++)
            _neuroEmitters[i] = new NeuroEmitter();
    }

    // -------------------------------------------------------------------------
    // Chemical access
    // -------------------------------------------------------------------------

    public float GetChemical(int chem) => _chemConcs[chem];

    public void SetChemical(int chem, float amount)
        => SetChemical(chem, amount, ChemicalDeltaSource.DirectSet, null);

    public void SetChemical(
        int chem,
        float amount,
        ChemicalDeltaSource source,
        string? detail = null,
        BiochemistryTrace? trace = null)
    {
        if (chem == 0) return;
        float before = _chemConcs[chem];
        float after = Math.Clamp(amount, 0.0f, 1.0f);
        _chemConcs[chem] = after;
        RecordDelta(chem, before, after - before, after, source, detail, trace);
    }

    public void AddChemical(int chem, float amount)
        => AddChemical(chem, amount, ChemicalDeltaSource.DirectAdd, null);

    public void AddChemical(
        int chem,
        float amount,
        ChemicalDeltaSource source,
        string? detail = null,
        BiochemistryTrace? trace = null)
    {
        if (chem == 0) return;
        float before = _chemConcs[chem];
        float after = Math.Clamp(before + amount, 0.0f, 1.0f);
        _chemConcs[chem] = after;
        RecordDelta(chem, before, after - before, after, source, detail, trace);
    }

    public void SubChemical(int chem, float amount)
        => SubChemical(chem, amount, ChemicalDeltaSource.DirectSubtract, null);

    public void SubChemical(
        int chem,
        float amount,
        ChemicalDeltaSource source,
        string? detail = null,
        BiochemistryTrace? trace = null)
    {
        if (chem == 0) return;
        float before = _chemConcs[chem];
        float after = Math.Clamp(before - amount, 0.0f, 1.0f);
        _chemConcs[chem] = after;
        RecordDelta(chem, before, after - before, after, source, detail, trace);
    }

    public ReadOnlySpan<float> GetChemicalConcs() => _chemConcs;

    public ChemicalHalfLifeView GetHalfLifeView(int chem)
        => new(chem, ChemicalCatalog.Get(chem), _decayRates[chem]);

    public BiochemistryTrace BeginTrace()
    {
        _activeTrace = new BiochemistryTrace();
        return _activeTrace;
    }

    public BiochemistryTrace? EndTrace()
    {
        BiochemistryTrace? trace = _activeTrace;
        _activeTrace = null;
        return trace;
    }

    public void ApplyEnvironmentSignals(
        float hotness,
        float coldness,
        float light,
        float radiation,
        float comfortNeed,
        float stress,
        BiochemistryTrace? trace = null)
    {
        hotness = Math.Clamp(hotness, 0.0f, 1.0f);
        coldness = Math.Clamp(coldness, 0.0f, 1.0f);
        light = Math.Clamp(light, 0.0f, 1.0f);
        radiation = Math.Clamp(radiation, 0.0f, 1.0f);
        comfortNeed = Math.Clamp(comfortNeed, 0.0f, 1.0f);
        stress = Math.Clamp(stress, 0.0f, 1.0f);

        SetCreatureLocusValue((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Coldness, coldness);
        SetCreatureLocusValue((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Hotness, hotness);
        SetCreatureLocusValue((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.LightLevel, light);
        SetCreatureLocusValue((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Radiation, radiation);

        AdjustChemicalToward(
            ChemID.Hotness,
            hotness,
            EnvironmentAdjustmentRate,
            "environment:temperature:hotness",
            trace);
        AdjustChemicalToward(
            ChemID.Coldness,
            coldness,
            EnvironmentAdjustmentRate,
            "environment:temperature:coldness",
            trace);

        if (comfortNeed > 0.00001f)
        {
            AddChemical(
                ChemID.Punishment,
                comfortNeed * TemperaturePunishmentRate,
                ChemicalDeltaSource.Environment,
                "environment:temperature:punishment",
                trace);
        }

        if (stress > 0.00001f)
        {
            AddChemical(
                ChemID.Punishment,
                stress * RadiationPunishmentRate,
                ChemicalDeltaSource.Environment,
                "environment:radiation:punishment",
                trace);
            AddChemical(
                ChemID.Fear,
                stress * RadiationFearRate,
                ChemicalDeltaSource.Environment,
                "environment:radiation:fear",
                trace);
        }
    }

    private void RecordDelta(
        int chem,
        float before,
        float amount,
        float after,
        ChemicalDeltaSource source,
        string? detail,
        BiochemistryTrace? trace)
        => (trace ?? _activeTrace)?.Record(chem, before, amount, after, source, detail);

    /// <summary>
    /// Internal access to the mutable chemical concentrations array.
    /// Used by <c>Creature</c> to pass the same array to <c>Brain.RegisterBiochemistry</c>.
    /// </summary>
    internal float[] GetChemicalArray() => _chemConcs;

    private void SetCreatureLocusValue(int tissue, int locus, float value)
    {
        if ((uint)tissue >= (uint)(int)CreatureTissue.Count)
            return;
        if ((uint)locus >= (uint)BiochemConst.MAX_LOCI_PER_TISSUE)
            return;

        _creatureLoci[tissue, locus].Value = Math.Clamp(value, 0.0f, 1.0f);
    }

    private void AdjustChemicalToward(
        int chem,
        float target,
        float maxStep,
        string detail,
        BiochemistryTrace? trace)
    {
        float current = _chemConcs[chem];
        float delta = Math.Clamp(target - current, -maxStep, maxStep);
        if (delta > 0.00001f)
        {
            AddChemical(chem, delta, ChemicalDeltaSource.Environment, detail, trace);
        }
        else if (delta < -0.00001f)
        {
            SubChemical(chem, -delta, ChemicalDeltaSource.Environment, detail, trace);
        }
    }

    // -------------------------------------------------------------------------
    // Organ access
    // -------------------------------------------------------------------------
    public int   OrganCount            => _numOrgans;
    public Organ GetOrgan(int i)       => _organs[i];
    public int   NeuroEmitterCount     => _numNeuroEmitters;

    public OrganDefinitionView GetOrganDefinitionView(int i)
        => _organs[i].CreateDefinitionView(i);

    // -------------------------------------------------------------------------
    // Locus table
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolve a creature locus (ORGAN_CREATURE) to its FloatLocus.
    /// Returns <see cref="FloatLocus.Invalid"/> on out-of-range indices.
    /// </summary>
    public FloatLocus GetCreatureLocus(int tissue, int locus)
    {
        if ((uint)tissue >= (uint)(int)CreatureTissue.Count) return FloatLocus.Invalid;
        if ((uint)locus  >= (uint)BiochemConst.MAX_LOCI_PER_TISSUE) return FloatLocus.Invalid;
        return _creatureLoci[tissue, locus];
    }

    /// <summary>
    /// Resolve any locus by (type, organ, tissue, locus) — called by Organ.BindToLoci for
    /// non-internal, non-reaction loci.
    /// </summary>
    public FloatLocus GetCreatureLocusAddress(int type, int organ, int tissue, int locus)
    {
        switch (organ)
        {
            case OrganID.ORGAN_BRAIN:
                // tissue = lobeId, locus = neuronId * noOfVariablesAvailableAsLoci
                if (BrainLocusProvider != null)
                    return BrainLocusProvider.GetBrainLocus(tissue, locus);
                return FloatLocus.DefaultNeuronInput;

            case OrganID.ORGAN_CREATURE:
                return GetCreatureLocus(tissue, locus);

            default:
                return FloatLocus.Invalid;
        }
    }

    // -------------------------------------------------------------------------
    // ReadFromGenome — define all chemistry from DNA.
    // Mirrors c2e Biochemistry::ReadFromGenome (Biochemistry.cpp:~200).
    // -------------------------------------------------------------------------
    public void ReadFromGenome(Genome.Genome genome)
    {
        // ---- 1. NeuroEmitter genes ----
        genome.Reset();
        while (genome.GetGeneType(
                   (int)GeneType.BIOCHEMISTRYGENE,
                   (int)BiochemSubtype.G_NEUROEMITTER,
                   BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES,
                   GeneSwitchOverride.SWITCH_AGE,
                   endType: (int)GeneType.ORGANGENE))
        {
            if (_numNeuroEmitters == BiochemConst.MAX_NEUROEMITTERS) break;

            FloatLocus[] neuronInputs = new FloatLocus[NeuroEmitter.NumNeuronalInputs];
            for (int i = 0; i < NeuroEmitter.NumNeuronalInputs; i++)
            {
                int lobeId   = genome.GetByte();
                int neuronId = genome.GetByte();
                neuronInputs[i] = GetCreatureLocusAddress(
                    (int)LocusType.Emitter,
                    OrganID.ORGAN_BRAIN,
                    lobeId,
                    neuronId * BiochemConst.NoOfNeuronVariablesAsLoci);
            }

            // Replace an existing neuroemitter using the same neurons, or append.
            NeuroEmitter? n = null;
            for (int i = 0; i < _numNeuroEmitters; i++)
            {
                int matches = 0;
                for (int o = 0; o < NeuroEmitter.NumNeuronalInputs; o++)
                    if (_neuroEmitters[i].NeuronalInputs[o] == neuronInputs[o]) matches++;
                if (matches == NeuroEmitter.NumNeuronalInputs)
                { n = _neuroEmitters[i]; break; }
            }
            if (n == null)
            {
                n = _neuroEmitters[_numNeuroEmitters++];
                for (int o = 0; o < NeuroEmitter.NumNeuronalInputs; o++)
                    n.NeuronalInputs[o] = neuronInputs[o];
            }

            n.bioTickRate = genome.GetFloat();
            for (int o = 0; o < NeuroEmitter.NumChemEmissions; o++)
            {
                n.ChemEmissions[o].ChemId  = genome.GetByte();
                n.ChemEmissions[o].Amount  = genome.GetFloat();
            }
        }

        // ---- 2. HalfLife genes ----
        genome.Reset();
        while (genome.GetGeneType(
                   (int)GeneType.BIOCHEMISTRYGENE,
                   (int)BiochemSubtype.G_HALFLIFE,
                   BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES))
        {
            for (int c = 0; c < BiochemConst.NUMCHEM; c++)
            {
                float inputFloat = genome.GetFloat() * 32.0f;
                if (inputFloat == 0.0f)
                {
                    _decayRates[c] = 0.0f;
                }
                else
                {
                    float halfLifeInTicks = MathF.Pow(2.2f, inputFloat);
                    _decayRates[c] = MathF.Pow(0.5f, 1.0f / halfLifeInTicks);
                }
            }
        }

        // ---- 3. Inject genes (initial chemical concentrations) ----
        genome.Reset();
        while (genome.GetGeneType(
                   (int)GeneType.BIOCHEMISTRYGENE,
                   (int)BiochemSubtype.G_INJECT,
                   BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES))
        {
            int   c = genome.GetByte();
            float v = genome.GetFloat();
            _chemConcs[c] = v;
        }

        // ---- 4. Organs ----
        genome.Reset();

        // Hidden body organ (always organ 0, created once at baby time).
        int iOrgan = 0;
        iOrgan = _numOrgans;
        _organs[iOrgan] = new Organ();
        _organs[iOrgan].SetOwner(this);
        _numOrgans++;
        _organs[iOrgan].InitFromGenome(genome);
        _organs[iOrgan].BindToLoci();
        iOrgan++;

        // Explicit ORGANGENE organs: process in generation order so cloned genes
        // re-use the already-created organ slot.
        int iGen = 0;
        bool geneFound;
        do
        {
            geneFound = false;
            genome.Reset();
            while (genome.GetGeneType(
                       (int)GeneType.ORGANGENE,
                       (int)OrganSubtype.G_ORGAN,
                       OrganSubtypeInfo.NUMORGANSUBTYPES,
                       GeneSwitchOverride.SWITCH_ALWAYS))
            {
                if (_numOrgans >= BiochemConst.MAXORGANS) break;

                if (genome.Generation() == iGen)
                {
                    geneFound = true;

                    float clockRate   = genome.GetFloat();
                    float repairRate  = genome.GetFloat();
                    float lifeForce   = genome.GetFloat();
                    float initClock   = genome.GetFloat();
                    float zeroDamage  = genome.GetFloat();

                    _organs[iOrgan] = new Organ();
                    _organs[iOrgan].Init(clockRate, repairRate, lifeForce, initClock, zeroDamage);
                    _organs[iOrgan].SetOwner(this);
                    _organs[iOrgan].InitFromGenome(genome);
                    _organs[iOrgan].BindToLoci();
                    iOrgan++;
                    _numOrgans++;
                }
            }
            iGen++;
        }
        while (geneFound);
    }

    // -------------------------------------------------------------------------
    // Update — called every world tick.
    // Mirrors c2e Biochemistry::Update (Biochemistry.cpp:320-352).
    // -------------------------------------------------------------------------
    public void Update()
        => Update(null);

    public void Update(BiochemistryTrace? trace)
    {
        BiochemistryTrace? previousTrace = _activeTrace;
        if (trace != null)
            _activeTrace = trace;

        try
        {
        // 1. NeuroEmitters
        for (int i = 0; i < _numNeuroEmitters; i++)
        {
            NeuroEmitter n = _neuroEmitters[i];
            n.bioTick += n.bioTickRate;
            if (n.bioTick > 1.0f)
            {
                n.bioTick -= 1.0f;

                float product = 1.0f;
                for (int o = 0; o < NeuroEmitter.NumNeuronalInputs; o++)
                    product *= n.NeuronalInputs[o].Value;

                for (int o = 0; o < NeuroEmitter.NumChemEmissions; o++)
                    AddChemical(
                        n.ChemEmissions[o].ChemId,
                        n.ChemEmissions[o].Amount * product,
                        ChemicalDeltaSource.NeuroEmitter,
                        $"neuroemitter:{i}");
            }
        }

        // 2. Organs
        for (int i = 0; i < _numOrgans; i++)
            _organs[i].Update();

        // 3. Half-life decay
        for (int i = 0; i < BiochemConst.NUMCHEM; i++)
        {
            float before = _chemConcs[i];
            float after = before * _decayRates[i];
            _chemConcs[i] = after;
            RecordDelta(i, before, after - before, after, ChemicalDeltaSource.HalfLifeDecay, "half-life decay", null);
        }

        // 4. Core metabolism: stores drive hunger and can buffer low ATP.
        UpdateHungerAndEnergyMetabolism();

        // 5. Fatigue and sleep pressure are derived from chemistry.
        UpdateFatigueAndSleep();

        // 6. Pain reflects injury, while energy and endorphins support recovery.
        UpdatePainInjuryAndRecovery();
        }
        finally
        {
            _activeTrace = previousTrace;
        }
    }

    private void UpdateHungerAndEnergyMetabolism()
    {
        UpdateMacronutrientMetabolism(ChemID.HungerForCarb, ChemID.Glycogen, "carb");
        UpdateMacronutrientMetabolism(ChemID.HungerForProtein, ChemID.Muscle, "protein");
        UpdateMacronutrientMetabolism(ChemID.HungerForFat, ChemID.Adipose, "fat");
    }

    private void UpdateMacronutrientMetabolism(int hungerChem, int storageChem, string token)
    {
        float storage = _chemConcs[storageChem];
        float hunger = _chemConcs[hungerChem];
        float targetHunger = Math.Clamp(1.0f - storage, 0.0f, 1.0f);
        float hungerDelta = Math.Clamp(
            (targetHunger - hunger) * HungerAdjustmentRate,
            -HungerAdjustmentRate,
            HungerAdjustmentRate);

        if (hungerDelta > 0.00001f)
        {
            AddChemical(
                hungerChem,
                hungerDelta,
                ChemicalDeltaSource.Metabolism,
                $"metabolism:{token}:hunger-rise");
        }
        else if (hungerDelta < -0.00001f)
        {
            SubChemical(
                hungerChem,
                -hungerDelta,
                ChemicalDeltaSource.Metabolism,
                $"metabolism:{token}:satiety");
        }

        float atp = _chemConcs[ChemID.ATP];
        if (atp >= 0.65f || storage <= 0.02f)
            return;

        float transfer = Math.Min(StorageToAtpRate, Math.Min(storage, (0.65f - atp) * 0.35f));
        if (transfer <= 0.00001f)
            return;

        SubChemical(
            storageChem,
            transfer,
            ChemicalDeltaSource.Metabolism,
            $"metabolism:{token}:storage-to-atp");
        AddChemical(
            ChemID.ATP,
            transfer,
            ChemicalDeltaSource.Metabolism,
            $"metabolism:{token}:storage-to-atp");
    }

    private void UpdateFatigueAndSleep()
    {
        float tiredness = _chemConcs[ChemID.Tiredness];
        float sleepiness = _chemConcs[ChemID.Sleepiness];
        bool asleep = sleepiness >= SleepThreshold || (sleepiness >= 0.6f && tiredness >= 0.8f);
        SetSleepLoci(asleep);

        if (asleep)
        {
            SubChemical(
                ChemID.Tiredness,
                SleepTirednessRecovery,
                ChemicalDeltaSource.Fatigue,
                "fatigue:sleep:tiredness-recovery");
            SubChemical(
                ChemID.Sleepiness,
                SleepinessRecovery,
                ChemicalDeltaSource.Fatigue,
                "fatigue:sleep:sleepiness-recovery");
            AddChemical(
                ChemID.ATP,
                SleepAtpRecovery,
                ChemicalDeltaSource.Fatigue,
                "fatigue:sleep:atp-recovery");
            return;
        }

        float atpDeficit = Math.Clamp(0.5f - _chemConcs[ChemID.ATP], 0.0f, 0.5f);
        AddChemical(
            ChemID.Tiredness,
            AwakeTirednessRise + atpDeficit * 0.012f,
            ChemicalDeltaSource.Fatigue,
            "fatigue:awake:tiredness-rise");

        float sleepinessRise = Math.Clamp((tiredness - 0.35f) * SleepinessRiseRate, 0.0f, 0.018f);
        if (sleepinessRise > 0.00001f)
        {
            AddChemical(
                ChemID.Sleepiness,
                sleepinessRise,
                ChemicalDeltaSource.Fatigue,
                "fatigue:awake:sleepiness-rise");
        }
    }

    private void SetSleepLoci(bool asleep)
    {
        float value = asleep ? 1.0f : 0.0f;
        _creatureLoci[(int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Asleep].Value = value;

        int involuntarySleep = SensorimotorEmitterLocus.Involuntary0 + InvoluntarySleepLocusOffset;
        if (involuntarySleep < BiochemConst.MAX_LOCI_PER_TISSUE)
            _creatureLoci[(int)CreatureTissue.Sensorimotor, involuntarySleep].Value = value;
    }

    private void UpdatePainInjuryAndRecovery()
    {
        float injury = _chemConcs[ChemID.Injury];
        float endorphin = _chemConcs[ChemID.Endorphin];
        float atp = _chemConcs[ChemID.ATP];

        if (injury > 0.00001f)
        {
            float painRise = Math.Min(MaxPainFromInjuryPerTick, injury * PainFromInjuryRate);
            AddChemical(
                ChemID.Pain,
                painRise,
                ChemicalDeltaSource.InjuryRecovery,
                "injury:pain-signal");
        }

        float repair = Math.Clamp(atp * 0.025f + endorphin * 0.045f, 0.0f, 0.080f);
        if (repair > 0.00001f && injury > 0.00001f)
        {
            SubChemical(
                ChemID.Injury,
                repair,
                ChemicalDeltaSource.InjuryRecovery,
                "injury:chemical-repair");
        }

        float pain = _chemConcs[ChemID.Pain];
        if (pain <= 0.00001f)
            return;

        if (repair <= 0.015f && endorphin <= 0.050f)
            return;

        float painRelief = BasePainRecovery + endorphin * 0.080f + repair * 0.800f;
        SubChemical(
            ChemID.Pain,
            painRelief,
            ChemicalDeltaSource.InjuryRecovery,
            "injury:pain-relief");
    }
}
