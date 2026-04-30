using System;
using System.Collections.Generic;
using CreaturesReborn.Sim.Genome;

namespace CreaturesReborn.Sim.Biochemistry;

/// <summary>
/// One biological organ: owns reactions, receptors, and emitters loaded from genome.
/// Tracks life-force (health), consumes ATP, and processes chemistry each tick.
/// Direct port of c2e's <c>Organ</c> class (Organ.h / Organ.cpp).
/// </summary>
public sealed class Organ
{
    // ---- Statics (mirrors c2e Organ statics) ----
    private const float BaseADPCost    = BiochemConst.BaseADPCost;    // 0.0078f
    private const float RateOfDecay    = BiochemConst.RateOfDecay;    // 1e-5f
    private const float BaseLifeForce  = BiochemConst.BaseLifeForce;  // 1e6f
    private const float MinLifeForce   = BiochemConst.MinLifeForce;   // 0.5f

    // ---- Locus fields (receptors / emitters can bind here via ORGAN_ORGAN) ----
    /// <summary>Internal ticks-per-update, genetically determined.</summary>
    public readonly FloatLocus LocClockRate          = new() { Value = 0.5f };
    /// <summary>Life-force expressed as a 0..1 fraction (shortterm/initial).</summary>
    public readonly FloatLocus LocLifeForce          = new() { Value = 0.5f };
    /// <summary>Modifier to long-term repair rate.</summary>
    public readonly FloatLocus LocLongTermRateOfRepair = new() { Value = 0.0f };
    /// <summary>Damage applied externally (cleared each clock tick).</summary>
    public readonly FloatLocus LocInjuryToApply      = new() { Value = 0.0f };

    // ---- Life-force state ----
    private float _initialLifeForce;
    private float _shortTermLifeForce;
    private float _longTermLifeForce;
    private float _longTermRateOfRepair;   // genetically determined base
    private float _energyCost;
    private float _damageDueToZeroEnergy;
    private bool  _energyAvailableFlag;

    // ---- Clock ----
    private float _clock;

    // ---- Reaction, receptor, emitter arrays ----
    private readonly Reaction[] _reactions   = new Reaction[BiochemConst.MAXREACTIONS];
    private readonly Emitter[]  _emitters    = new Emitter[BiochemConst.MAXEMITTERS];
    private readonly List<List<Receptor>> _receptorGroups = new();

    private int _numReactions;
    private int _numEmitters;
    private int _numReceptors;
    private int _numReceptorGroups;

    // ---- Owner ----
    private Biochemistry? _owner;

    // ---- Public accessors ----
    public bool Failed      => _longTermLifeForce <= MinLifeForce;
    public bool Functioning => _energyAvailableFlag && !Failed;

    public float InitialLifeForce        => _initialLifeForce;
    public float ShortTermLifeForce      => _shortTermLifeForce;
    public float LongTermLifeForce       => _longTermLifeForce;
    public float LongTermRateOfRepair    => _longTermRateOfRepair;
    public float EnergyCost              => _energyCost;
    public float DamageDueToZeroEnergy   => _damageDueToZeroEnergy;
    public int   ReactionCount           => _numReactions;
    public int   EmitterCount            => _numEmitters;
    public int   ReceptorCount           => _numReceptors;

    public void SetOwner(Biochemistry owner) => _owner = owner;

    public OrganSnapshot CreateSnapshot(int index)
        => new(
            index,
            Failed ? OrganHealthState.Failed : (_energyAvailableFlag ? OrganHealthState.Functioning : OrganHealthState.EnergyStarved),
            _initialLifeForce,
            _shortTermLifeForce,
            _longTermLifeForce,
            _longTermRateOfRepair,
            _energyCost,
            _damageDueToZeroEnergy,
            _numReactions,
            _numEmitters,
            _numReceptors);

    public IReadOnlyList<ReactionDefinitionView> GetReactionDefinitionViews()
    {
        var reactions = new List<ReactionDefinitionView>(_numReactions);
        for (int i = 0; i < _numReactions; i++)
        {
            Reaction reaction = _reactions[i];
            reactions.Add(new(
                i,
                reaction.R1,
                reaction.propR1,
                reaction.R2,
                reaction.propR2,
                reaction.P1,
                reaction.propP1,
                reaction.P2,
                reaction.propP2,
                reaction.Rate.Value));
        }

        return reactions;
    }

    public IReadOnlyList<ReactionParitySnapshot> CreateReactionParitySnapshots()
    {
        var reactions = new List<ReactionParitySnapshot>(_numReactions);
        for (int i = 0; i < _numReactions; i++)
        {
            Reaction reaction = _reactions[i];
            reactions.Add(new(
                i,
                reaction.R1,
                reaction.propR1,
                reaction.R2,
                reaction.propR2,
                reaction.P1,
                reaction.propP1,
                reaction.P2,
                reaction.propP2,
                reaction.Rate.Value));
        }

        return reactions;
    }

    public IReadOnlyList<ReceptorParitySnapshot> CreateReceptorParitySnapshots()
    {
        var receptors = new List<ReceptorParitySnapshot>(_numReceptors);
        int index = 0;
        foreach (List<Receptor> group in _receptorGroups)
        {
            foreach (Receptor receptor in group)
            {
                receptors.Add(new(
                    index++,
                    receptor.IDOrgan,
                    receptor.IDTissue,
                    receptor.IDLocus,
                    receptor.Chem,
                    receptor.Threshold,
                    receptor.Nominal,
                    receptor.Gain,
                    receptor.Effect,
                    receptor.isClockRateReceptor));
            }
        }

        return receptors;
    }

    public IReadOnlyList<EmitterParitySnapshot> CreateEmitterParitySnapshots()
    {
        var emitters = new List<EmitterParitySnapshot>(_numEmitters);
        for (int i = 0; i < _numEmitters; i++)
        {
            Emitter emitter = _emitters[i];
            emitters.Add(new(
                i,
                emitter.IDOrgan,
                emitter.IDTissue,
                emitter.IDLocus,
                emitter.Chem,
                emitter.Threshold,
                emitter.bioTickRate,
                emitter.Gain,
                emitter.Effect));
        }

        return emitters;
    }

    public OrganDefinitionView CreateDefinitionView(int index)
        => new(index, CreateSnapshot(index), GetReactionDefinitionViews());

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------
    public Organ()
    {
        _energyAvailableFlag = true;
        LocClockRate.Value = 0.5f;
        _clock = 0.0f;

        LocLifeForce.Value = 0.5f;
        _initialLifeForce  = LocLifeForce.Value * BaseLifeForce;
        _shortTermLifeForce = _initialLifeForce;
        _longTermLifeForce  = _initialLifeForce;

        _longTermRateOfRepair = 10.0f / 255.0f;
        LocLongTermRateOfRepair.Value = 0.0f;
        LocInjuryToApply.Value        = 0.0f;

        _energyCost            = 0;
        _damageDueToZeroEnergy = _initialLifeForce / 128.0f;

        for (int i = 0; i < BiochemConst.MAXREACTIONS; i++) _reactions[i] = new Reaction();
        for (int i = 0; i < BiochemConst.MAXEMITTERS;  i++) _emitters[i]  = new Emitter();
    }

    // -------------------------------------------------------------------------
    // Init (from explicit ORGANGENE parameters)
    // -------------------------------------------------------------------------
    public void Init(float clockRate, float rateOfRepair, float lifeForce,
                     float initialClock, float damageDueToZeroEnergy)
    {
        LocClockRate.Value = clockRate;
        _clock = initialClock;

        lifeForce = MathF.Max(1.0f, lifeForce);
        LocLifeForce.Value  = lifeForce;
        _initialLifeForce   = lifeForce * BaseLifeForce;
        _shortTermLifeForce = _initialLifeForce;
        _longTermLifeForce  = _initialLifeForce;

        _longTermRateOfRepair = rateOfRepair;
        damageDueToZeroEnergy = Math.Min(1.0f, damageDueToZeroEnergy);
        _damageDueToZeroEnergy = (_initialLifeForce * damageDueToZeroEnergy) / 255.0f;
    }

    public void InitFromPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 5)
            return;

        Init(
            payload[0] / 255f,
            payload[1] / 255f,
            payload[2] / 255f,
            payload[3] / 255f,
            payload[4] / 255f);
    }

    public bool ApplyReceptorGene(ReadOnlySpan<byte> payload, int currentReactionNo = -1)
    {
        if (payload.Length < 8 || _numReceptors >= BiochemConst.MAXRECEPTORS)
            return false;

        var r = new Receptor();
        r.IDOrgan = payload[0] % OrganID.NUM_RECEPTOR_ORGANS;
        r.IDTissue = payload[1];
        if (r.IDOrgan == OrganID.ORGAN_REACTION)
            r.IDTissue = currentReactionNo;

        r.IDLocus = payload[2];
        r.Chem = payload[3] % BiochemConst.NUMCHEM;
        r.Threshold = payload[4] / 255f;
        r.Nominal = payload[5] / 255f;
        r.Gain = payload[6] / 255f;
        r.Effect = payload[7];

        int groupIndex = 0;
        for (; groupIndex < _numReceptorGroups; groupIndex++)
        {
            Receptor first = _receptorGroups[groupIndex][0];
            if (first.IDOrgan == r.IDOrgan &&
                first.IDTissue == r.IDTissue &&
                first.IDLocus == r.IDLocus)
                break;
        }

        if (groupIndex == _numReceptorGroups)
        {
            if (_numReceptorGroups >= BiochemConst.MAXRECEPTORGROUPS)
                return false;

            _receptorGroups.Add(new List<Receptor>());
            _numReceptorGroups++;
        }

        _numReceptors++;
        _receptorGroups[groupIndex].Add(r);
        BindToLoci();
        InitEnergyCost();
        return true;
    }

    public bool ApplyReactionGene(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 9 || _numReactions >= BiochemConst.MAXREACTIONS - 1)
            return false;

        Reaction reaction = _reactions[_numReactions++];
        reaction.propR1 = Math.Clamp((int)payload[0], 1, BiochemConst.MAXREACTANTS);
        reaction.R1 = payload[1];
        reaction.propR2 = Math.Clamp((int)payload[2], 1, BiochemConst.MAXREACTANTS);
        reaction.R2 = payload[3];
        reaction.propP1 = Math.Clamp((int)payload[4], 1, BiochemConst.MAXREACTANTS);
        reaction.P1 = payload[5];
        reaction.propP2 = Math.Clamp((int)payload[6], 1, BiochemConst.MAXREACTANTS);
        reaction.P2 = payload[7];
        reaction.Rate.Value = 1.0f - payload[8] / 255f;

        BindToLoci();
        InitEnergyCost();
        return true;
    }

    public bool ApplyEmitterGene(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8 || _numEmitters >= BiochemConst.MAXEMITTERS)
            return false;

        int organ = payload[0] % OrganID.NUM_EMITTER_ORGANS;
        int tissue = payload[1];
        int locus = payload[2];
        int chem = payload[3] % BiochemConst.NUMCHEM;

        Emitter? emitter = null;
        for (int i = 0; i < _numEmitters; i++)
        {
            if (_emitters[i].IDOrgan == organ &&
                _emitters[i].IDTissue == tissue &&
                _emitters[i].IDLocus == locus &&
                _emitters[i].Chem == chem)
            {
                emitter = _emitters[i];
                break;
            }
        }

        if (emitter == null)
        {
            emitter = _emitters[_numEmitters++];
            emitter.IDOrgan = organ;
            emitter.IDTissue = tissue;
            emitter.IDLocus = locus;
            emitter.Chem = chem;
        }

        emitter.Threshold = payload[4] / 255f;
        emitter.bioTickRate = 1.0f / Math.Clamp((int)payload[5], 1, 255);
        emitter.Gain = payload[6] / 255f;
        emitter.Effect = payload[7];
        BindToLoci();
        InitEnergyCost();
        return true;
    }

    // -------------------------------------------------------------------------
    // InitFromGenome — reads receptor/reaction/emitter genes for this organ.
    // Mirrors c2e Organ::InitFromGenome (Organ.cpp:~200).
    // -------------------------------------------------------------------------
    public void InitFromGenome(Genome.Genome genome)
    {
        genome.Store();

        int thisReactionNo = -1;
        while (true)
        {
            genome.Store2();

            // ---- Receptors (stop at next REACTION or ORGAN gene) ----
            while (genome.GetGeneType(
                       (int)GeneType.BIOCHEMISTRYGENE,
                       (int)BiochemSubtype.G_RECEPTOR,
                       BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES,
                       GeneSwitchOverride.SWITCH_AGE,
                       (int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_REACTION,
                       (int)GeneType.ORGANGENE))
            {
                if (_numReceptors >= BiochemConst.MAXRECEPTORS) break;

                var r = new Receptor();
                _numReceptors++;

                r.IDOrgan  = genome.GetCodon(0, OrganID.NUM_RECEPTOR_ORGANS - 1);
                r.IDTissue = genome.GetCodon(0, 255);

                if (r.IDOrgan == OrganID.ORGAN_REACTION)
                    r.IDTissue = thisReactionNo;   // bind to current reaction index

                r.IDLocus    = genome.GetCodon(0, 255);
                r.Chem       = genome.GetCodon(0, BiochemConst.NUMCHEM - 1);
                r.Threshold  = genome.GetFloat();
                r.Nominal    = genome.GetFloat();
                r.Gain       = genome.GetFloat();
                r.Effect     = genome.GetByte();

                // ---- Group receptors sharing the same locus ----
                int g = 0;
                for (; g < _numReceptorGroups; g++)
                {
                    Receptor first = _receptorGroups[g][0];
                    if (first.IDOrgan  == r.IDOrgan  &&
                        first.IDTissue == r.IDTissue &&
                        first.IDLocus  == r.IDLocus)
                        break;
                }
                if (g == _numReceptorGroups)
                {
                    // New group.
                    if (_numReceptorGroups >= BiochemConst.MAXRECEPTORGROUPS) continue;
                    _receptorGroups.Add(new List<Receptor>());
                    _numReceptorGroups++;
                }
                _receptorGroups[g].Add(r);
            }
            genome.Restore2();

            // ---- One REACTION gene (stop at ORGAN gene) ----
            if (!genome.GetGeneType(
                    (int)GeneType.BIOCHEMISTRYGENE,
                    (int)BiochemSubtype.G_REACTION,
                    BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES,
                    GeneSwitchOverride.SWITCH_AGE,
                    endType: (int)GeneType.ORGANGENE))
                break;

            if (_numReactions >= BiochemConst.MAXREACTIONS - 1) break;

            thisReactionNo = _numReactions++;
            Reaction rn = _reactions[thisReactionNo];
            rn.propR1 = genome.GetCodon(1, BiochemConst.MAXREACTANTS);
            rn.R1     = genome.GetByte();
            rn.propR2 = genome.GetCodon(1, BiochemConst.MAXREACTANTS);
            rn.R2     = genome.GetByte();
            rn.propP1 = genome.GetCodon(1, BiochemConst.MAXREACTANTS);
            rn.P1     = genome.GetByte();
            rn.propP2 = genome.GetCodon(1, BiochemConst.MAXREACTANTS);
            rn.P2     = genome.GetByte();
            rn.Rate.Value = 1.0f - genome.GetFloat();  // 0=fast, 1=slow
        }

        // ---- Emitters (restart scan from stored position) ----
        genome.Restore();
        while (genome.GetGeneType(
                   (int)GeneType.BIOCHEMISTRYGENE,
                   (int)BiochemSubtype.G_EMITTER,
                   BiochemSubtypeInfo.NUMBIOCHEMSUBTYPES,
                   GeneSwitchOverride.SWITCH_AGE,
                   endType: (int)GeneType.ORGANGENE))
        {
            if (_numEmitters >= BiochemConst.MAXEMITTERS) break;

            int o = genome.GetCodon(0, OrganID.NUM_EMITTER_ORGANS - 1);
            int t = genome.GetByte();
            int l = genome.GetByte();
            int c = genome.GetCodon(0, BiochemConst.NUMCHEM - 1);

            // Replace existing emitter at same locus+chem, or append.
            Emitter? e = null;
            for (int i = 0; i < _numEmitters; i++)
            {
                if (_emitters[i].IDOrgan  == o &&
                    _emitters[i].IDTissue == t &&
                    _emitters[i].IDLocus  == l &&
                    _emitters[i].Chem     == c)
                {
                    e = _emitters[i];
                    break;
                }
            }
            if (e == null)
            {
                e = _emitters[_numEmitters++];
                e.IDOrgan  = o;
                e.IDTissue = t;
                e.IDLocus  = l;
                e.Chem     = c;
            }

            e.Threshold   = genome.GetFloat();
            e.bioTickRate = 1.0f / genome.GetCodon(1, 255);
            e.Gain        = genome.GetFloat();
            e.Effect      = genome.GetCodon(0, 255);
        }

        genome.Restore();
        InitEnergyCost();
    }

    // -------------------------------------------------------------------------
    // BindToLoci — resolve locus addresses after all organs are loaded.
    // Mirrors c2e Organ::BindToLoci (Organ.cpp:86).
    // -------------------------------------------------------------------------
    public void BindToLoci()
    {
        // Receptors
        foreach (List<Receptor> group in _receptorGroups)
        {
            foreach (Receptor r in group)
            {
                r.Dest = GetLocusAddress((int)LocusType.Receptor, r.IDOrgan, r.IDTissue, r.IDLocus);
                r.isClockRateReceptor =
                    r.IDOrgan == OrganID.ORGAN_ORGAN &&
                    r.IDLocus == OrganReceptorLocus.ClockRate;
            }
        }

        // Emitters
        for (int i = 0; i < _numEmitters; i++)
        {
            Emitter e = _emitters[i];
            e.Source = GetLocusAddress((int)LocusType.Emitter, e.IDOrgan, e.IDTissue, e.IDLocus);
        }
    }

    // -------------------------------------------------------------------------
    // Update — called every biochem tick.
    // Mirrors c2e Organ::Update (Organ.cpp:~340).
    // -------------------------------------------------------------------------
    public bool Update()
    {
        if (Failed) return false;

        bool workDone = false;
        _clock += LocClockRate.Value;

        if (_clock >= 1.0f)
        {
            _clock -= 1.0f;
            _energyAvailableFlag = ConsumeEnergy();

            if (_energyAvailableFlag)
            {
                workDone = true;
                workDone |= ProcessAll();
            }
            else
            {
                Injure(_damageDueToZeroEnergy);
            }

            workDone |= RepairInjury(_energyAvailableFlag);

            if (LocInjuryToApply.Value != 0.0f)
            {
                Injure(LocToLf(LocInjuryToApply.Value) / 10.0f);
                LocInjuryToApply.Value = 0.0f;
            }

            ProcessReceptors(false);  // all receptors
        }
        else
        {
            ProcessReceptors(true);   // only clock-rate receptors
        }

        if (workDone) DecayLifeForce();
        return true;
    }

    // -------------------------------------------------------------------------
    // Injure
    // -------------------------------------------------------------------------
    public void Injure(float damage)
    {
        _shortTermLifeForce = BoundedSub(_shortTermLifeForce, damage);
        LocLifeForce.Value  = _shortTermLifeForce / _initialLifeForce;
        _owner?.AddChemical(ChemID.Injury, LfToLoc(damage), ChemicalDeltaSource.OrganInjury, "organ injury");
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------
    private void DecayLifeForce()
    {
        _shortTermLifeForce -= _shortTermLifeForce * RateOfDecay;
        _longTermLifeForce  -= _longTermLifeForce  * RateOfDecay;
        LocLifeForce.Value   = _shortTermLifeForce / _initialLifeForce;
    }

    private bool RepairInjury(bool energyAvailable)
    {
        float delta = _longTermLifeForce - _shortTermLifeForce;
        _longTermLifeForce -= delta * _longTermRateOfRepair;

        if (energyAvailable)
        {
            float repair = delta * LocLongTermRateOfRepair.Value;
            _shortTermLifeForce += repair;
            _owner?.SubChemical(ChemID.Injury, LfToLoc(repair), ChemicalDeltaSource.OrganInjury, "organ repair");
        }

        LocLifeForce.Value = _shortTermLifeForce / _initialLifeForce;
        return delta > 0.5f && energyAvailable;
    }

    private void InitEnergyCost()
    {
        if (_owner != null)
            _owner.ATPRequirement = MathF.Max(0.0f, _owner.ATPRequirement - _energyCost * LocClockRate.Value);

        _energyCost = BaseADPCost + (_numReceptors + _numEmitters + _numReactions) / 2550.0f;
        if (_owner != null)
            _owner.ATPRequirement += _energyCost * LocClockRate.Value;
    }

    private bool ConsumeEnergy()
    {
        if (_owner == null) return true;
        if (_owner.GetChemical(ChemID.ATP) >= _energyCost)
        {
            _owner.SubChemical(ChemID.ATP, _energyCost, ChemicalDeltaSource.OrganEnergy, "organ ATP cost");
            _owner.AddChemical(ChemID.ADP, _energyCost, ChemicalDeltaSource.OrganEnergy, "organ ADP output");
            return true;
        }
        return false;
    }

    private bool ProcessAll()
    {
        if (_owner == null) return false;
        bool work = false;

        // Emitters: read source locus, emit chemical
        for (int i = 0; i < _numEmitters; i++)
        {
            Emitter e = _emitters[i];
            float sig = ((e.Effect & (int)EmitterFlags.EM_INVERT) != 0)
                ? 1.0f - e.Source.Value
                : e.Source.Value;

            e.bioTick += e.bioTickRate;
            if (e.bioTick > 1.0f)
            {
                e.bioTick -= 1.0f;
                if (sig > 0.0f)
                {
                    float conc = sig - e.Threshold;
                    if (conc > 0.0f)
                    {
                        if ((e.Effect & (int)EmitterFlags.EM_DIGITAL) != 0)
                            _owner.AddChemical(e.Chem, e.Gain, ChemicalDeltaSource.Emitter, $"emitter:{i}");
                        else
                            _owner.AddChemical(e.Chem, conc * e.Gain, ChemicalDeltaSource.Emitter, $"emitter:{i}");

                        if ((e.Effect & (int)EmitterFlags.EM_REMOVE) != 0)
                            e.Source.Value = 0.0f;

                        work = true;
                    }
                }
            }
        }

        // Reactions
        for (int i = 0; i < _numReactions; i++)
            work |= ProcessReaction(i);

        return work;
    }

    private bool ProcessReaction(int idx)
    {
        if (_owner == null) return false;
        Reaction rn = _reactions[idx];

        float avail = 1.0f, avail2 = 1.0f;
        if (rn.R1 != 0) avail  = _owner.GetChemical(rn.R1) / rn.propR1;
        if (rn.R2 != 0) avail2 = _owner.GetChemical(rn.R2) / rn.propR2;
        if (avail2 < avail) avail = avail2;

        if (avail <= 0.0f) return false;

        // Convert stored Rate (0=fast, 1=slow) back to per-tick fraction.
        float inputFloat     = (1.0f - rn.Rate.Value) * 32.0f;
        float halfLifeInTicks = MathF.Pow(2.2f, inputFloat);
        float rate           = 1.0f - MathF.Pow(0.5f, 1.0f / halfLifeInTicks);

        avail *= rate;

        _owner.SubChemical(rn.R1, avail * rn.propR1, ChemicalDeltaSource.Reaction, $"reaction:{idx}:reactant1");
        _owner.SubChemical(rn.R2, avail * rn.propR2, ChemicalDeltaSource.Reaction, $"reaction:{idx}:reactant2");
        _owner.AddChemical(rn.P1, avail * rn.propP1, ChemicalDeltaSource.Reaction, $"reaction:{idx}:product1");
        _owner.AddChemical(rn.P2, avail * rn.propP2, ChemicalDeltaSource.Reaction, $"reaction:{idx}:product2");

        return true;
    }

    private bool ProcessReceptors(bool onlyClockRate)
    {
        bool work = false;
        if (_owner == null) return false;

        foreach (List<Receptor> group in _receptorGroups)
        {
            if (group.Count == 0) continue;

            // Check if we should skip non-clock-rate groups
            if (onlyClockRate && !group[0].isClockRateReceptor) continue;

            int   receptorsProcessed   = 0;
            float totalNominals        = 0.0f;
            int   addTerms = 0, subTerms = 0;
            float addSum   = 0.0f, subSum = 0.0f;

            foreach (Receptor r in group)
            {
                if (r.Chem == 0) continue;

                receptorsProcessed++;
                totalNominals += r.Nominal;

                float signal = _owner.GetChemical(r.Chem) - r.Threshold;
                if (signal < 0.0f) signal = 0.0f;

                if (signal > 0.0f)
                {
                    signal = ((r.Effect & (int)ReceptorFlags.RE_DIGITAL) != 0)
                        ? r.Gain
                        : signal * r.Gain;
                }

                if ((r.Effect & (int)ReceptorFlags.RE_REDUCE) != 0)
                { subSum += signal; subTerms++; }
                else
                { addSum += signal; addTerms++; }

                work = true;
            }

            if (onlyClockRate && receptorsProcessed == 0) continue;

            float result = receptorsProcessed > 0
                ? totalNominals / receptorsProcessed
                : 0.0f;
            if (addTerms > 0) result = BoundedAdd(result, addSum / addTerms);
            if (subTerms > 0) result = BoundedSub(result, subSum / subTerms);

            // Special case: LOC_DIE uses OR logic
            Receptor first = group[0];
            if (first.IDOrgan  == OrganID.ORGAN_CREATURE &&
                first.IDTissue == (int)CreatureTissue.Immune &&
                first.IDLocus  == ImmuneLocus.Die)
            {
                result = (totalNominals + addSum - subSum) > 0.0f ? 1.0f : 0.0f;
            }

            first.Dest.Value = result;
        }
        return work;
    }

    // -------------------------------------------------------------------------
    // GetLocusAddress — resolve a locus reference to a FloatLocus.
    // Mirrors c2e Organ::GetLocusAddress (Organ.cpp:~430).
    // -------------------------------------------------------------------------
    public FloatLocus GetLocusAddress(int type, int organ, int tissue, int locus)
    {
        switch (organ)
        {
            case OrganID.ORGAN_ORGAN:
                return GetInternalLocusAddress(type, locus);

            case OrganID.ORGAN_REACTION:
                if (tissue < 0 || tissue >= _numReactions) return FloatLocus.Invalid;
                return _reactions[tissue].Rate;

            default:
                return _owner?.GetCreatureLocusAddress(type, organ, tissue, locus)
                       ?? FloatLocus.Invalid;
        }
    }

    private FloatLocus GetInternalLocusAddress(int type, int locus)
    {
        if (type == (int)LocusType.Receptor)
        {
            return locus switch
            {
                OrganReceptorLocus.ClockRate    => LocClockRate,
                OrganReceptorLocus.RateOfRepair => LocLongTermRateOfRepair,
                OrganReceptorLocus.Injury       => LocInjuryToApply,
                _                               => FloatLocus.Invalid,
            };
        }
        else
        {
            return locus switch
            {
                OrganEmitterLocus.ClockRate    => LocClockRate,
                OrganEmitterLocus.RateOfRepair => LocLongTermRateOfRepair,
                OrganEmitterLocus.LifeForce    => LocLifeForce,
                _                              => FloatLocus.Invalid,
            };
        }
    }

    // ---- Math helpers ----
    private float LocToLf(float loc) => _initialLifeForce * loc;
    private float LfToLoc(float lf)  => _initialLifeForce > 0.0f ? lf / _initialLifeForce : 0.0f;

    private static float BoundedAdd(float a, float b) => MathF.Min(1.0f, a + b);
    private static float BoundedSub(float a, float b) => MathF.Max(0.0f, a - b);
}
