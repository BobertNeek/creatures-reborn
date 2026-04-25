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
    {
        if (chem == 0) return;
        _chemConcs[chem] = Math.Clamp(amount, 0.0f, 1.0f);
    }

    public void AddChemical(int chem, float amount)
    {
        if (chem == 0) return;
        _chemConcs[chem] = Math.Clamp(_chemConcs[chem] + amount, 0.0f, 1.0f);
    }

    public void SubChemical(int chem, float amount)
    {
        if (chem == 0) return;
        _chemConcs[chem] = Math.Clamp(_chemConcs[chem] - amount, 0.0f, 1.0f);
    }

    public ReadOnlySpan<float> GetChemicalConcs() => _chemConcs;

    /// <summary>
    /// Internal access to the mutable chemical concentrations array.
    /// Used by <c>Creature</c> to pass the same array to <c>Brain.RegisterBiochemistry</c>.
    /// </summary>
    internal float[] GetChemicalArray() => _chemConcs;

    // -------------------------------------------------------------------------
    // Organ access
    // -------------------------------------------------------------------------
    public int   OrganCount            => _numOrgans;
    public Organ GetOrgan(int i)       => _organs[i];
    public int   NeuroEmitterCount     => _numNeuroEmitters;

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
                    AddChemical(n.ChemEmissions[o].ChemId, n.ChemEmissions[o].Amount * product);
            }
        }

        // 2. Organs
        for (int i = 0; i < _numOrgans; i++)
            _organs[i].Update();

        // 3. Half-life decay
        for (int i = 0; i < BiochemConst.NUMCHEM; i++)
            _chemConcs[i] *= _decayRates[i];
    }
}
