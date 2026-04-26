using System;
using System.Collections.Generic;
using System.Text;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Sim.Brain;

using G = CreaturesReborn.Sim.Genome.Genome;

/// <summary>
/// Multi-lobe SVRule brain: container for all lobes, tracts, and instincts.
/// Implements <see cref="IBrainLocusProvider"/> so Biochemistry can bind neuron
/// state variables as loci for receptors and emitters.
/// Direct port of c2e's <c>Brain</c> class (Brain.h / Brain.cpp).
/// </summary>
public sealed class Brain : IBrainLocusProvider
{
    // Hardcoded chemical indices (Brain.catalogue in c2e)
    // Chemical 255 = instinct signal; 254 = pre-instinct signal reserved for pre-instinct state.
    private const int InstinctChemIndex    = 255;
    private const int PreInstinctChemIndex = 254;

    // -------------------------------------------------------------------------
    // Collections
    // -------------------------------------------------------------------------
    private readonly List<Lobe>           _lobes      = new();
    private readonly List<Tract>          _tracts     = new();
    private readonly List<BrainComponent> _components = new();  // sorted, lobes + tracts interleaved
    private readonly List<Instinct>       _instincts  = new();
    private readonly List<IBrainModule>   _modules    = new();

    // -------------------------------------------------------------------------
    // Biochemistry link
    // -------------------------------------------------------------------------
    private float[]? _chemicals;

    // FloatLocus cache for brain loci (backed directly by neuron state arrays)
    private readonly Dictionary<(int tissueId, int locusIdx), FloatLocus> _brainLoci = new();

    // -------------------------------------------------------------------------
    // Instinct control
    // -------------------------------------------------------------------------
    private bool _processingInstincts;
    private int _updateTick;

    // -------------------------------------------------------------------------
    // ReadFromGenome — mirroring Brain.cpp:77-158
    // -------------------------------------------------------------------------
    public void ReadFromGenome(G genome, IRng rng)
    {
        // Pass 1: Lobes
        genome.Reset();
        while (genome.GetGeneType((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE,
                                  BrainSubtypeInfo.NUMBRAINSUBTYPES))
        {
            if (_lobes.Count >= BrainConst.MaxLobes) break;
            try
            {
                var lobe = new Lobe(genome);
                lobe.IdInList = _lobes.Count > 0 ? _lobes[_lobes.Count - 1].IdInList + 1 : 0;
                lobe.RegisterRng(rng);
                _lobes.Add(lobe);
                _components.Add(lobe);
            }
            catch (Exception) { /* skip malformed lobe genes */ }
        }

        // Pass 2: Tracts
        genome.Reset();
        while (genome.GetGeneType((int)GeneType.BRAINGENE, (int)BrainSubtype.G_TRACT,
                                  BrainSubtypeInfo.NUMBRAINSUBTYPES))
        {
            if (_tracts.Count >= BrainConst.MaxTracts) break;
            try
            {
                var tract = new Tract(genome, _lobes, rng);
                tract.IdInList = _tracts.Count > 0 ? _tracts[_tracts.Count - 1].IdInList + 1 : 0;
                tract.RegisterRng(rng);
                _tracts.Add(tract);
                _components.Add(tract);
            }
            catch (Exception) { /* skip malformed / unresolvable tract genes */ }
        }

        // Sort by UpdateAtTime ascending
        _components.Sort(BrainComponent.CompareByUpdateTime);

        // Register biochemistry and initialise
        if (_chemicals != null)
        {
            foreach (var c in _components)
            {
                c.RegisterBiochemistry(_chemicals);
                c.Initialise();
            }
        }
        else
        {
            foreach (var c in _components)
                c.Initialise();
        }

        // Pass 3: Instinct genes
        genome.Reset();
        while (genome.GetGeneType((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_INSTINCT,
                                  CreatureSubtypeInfo.NUMCREATURESUBTYPES))
        {
            if (_instincts.Count >= BrainConst.MaxInstincts) break;
            try { _instincts.Add(new Instinct(genome, this)); }
            catch (Exception) { }
        }
    }

    // -------------------------------------------------------------------------
    // RegisterBiochemistry
    // -------------------------------------------------------------------------
    public void RegisterBiochemistry(float[] chemicals)
    {
        _chemicals = chemicals;
        foreach (var c in _components)
            c.RegisterBiochemistry(chemicals);
    }

    // -------------------------------------------------------------------------
    // Update — normal or instinct-processing tick
    // -------------------------------------------------------------------------
    public void Update()
        => Update(null);

    public void Update(LearningTrace? trace)
    {
        if (!_processingInstincts)
        {
            UpdateComponents(trace);
            _updateTick++;
            return;
        }

        // Process instincts one per tick
        if (_instincts.Count > 0)
        {
            Instinct inst = _instincts[_instincts.Count - 1];
            int remainingBefore = _instincts.Count;
            if (inst.Process())
            {
                _instincts.RemoveAt(_instincts.Count - 1);
                trace?.RecordInstinct(new InstinctTrace(_updateTick, remainingBefore - 1, Fired: true));
            }
            else
            {
                trace?.RecordInstinct(new InstinctTrace(_updateTick, remainingBefore, Fired: false));
            }
            _updateTick++;
            return;
        }

        // No instincts left — back to normal
        _processingInstincts = false;
        _updateTick++;
    }

    public void UpdateComponents()
        => UpdateComponents(null);

    public void UpdateComponents(LearningTrace? trace)
    {
        foreach (var c in _components)
        {
            // If a registered module shadows this lobe, skip the SVRule update.
            if (c is Lobe lobe && IsLobeTokenShadowed(lobe.Token))
                continue;
            if (c is Tract tract)
                tract.DoUpdate(trace, _updateTick);
            else
                c.DoUpdate();
        }

        // Run plugged-in modules (after default lobe stack, or as shadow replacements).
        foreach (var m in _modules)
            m.Update(this);
    }

    // -------------------------------------------------------------------------
    // IBrainModule registration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Register an external brain module. It will be initialised immediately and called
    /// every tick from <see cref="UpdateComponents"/>.  Modules whose
    /// <see cref="IBrainModule.ShadowedLobeToken"/> is non-null replace the named lobe.
    /// </summary>
    public void RegisterModule(IBrainModule module)
    {
        _modules.Add(module);
        module.Initialise(this);
    }

    public IReadOnlyList<BrainModuleDescriptor> GetModuleDescriptors()
    {
        var descriptors = new List<BrainModuleDescriptor>(_modules.Count);
        foreach (IBrainModule module in _modules)
            descriptors.Add(BrainModuleDescriptor.FromModule(module));
        return descriptors;
    }

    private bool IsLobeTokenShadowed(int token)
    {
        foreach (var m in _modules)
            if (m.ShadowedLobeToken == token) return true;
        return false;
    }

    // -------------------------------------------------------------------------
    // Instinct control
    // -------------------------------------------------------------------------
    public void SetWhetherToProcessInstincts(bool process)
    {
        _processingInstincts = process;
        if (_chemicals == null) return;

        if (process)
        {
            _chemicals[PreInstinctChemIndex] = 1.0f;
            _chemicals[InstinctChemIndex]    = 0.0f;
            UpdateComponents();
            _chemicals[PreInstinctChemIndex] = 0.0f;
            _chemicals[InstinctChemIndex]    = 1.0f;
        }
        else
        {
            _chemicals[PreInstinctChemIndex] = 0.0f;
            _chemicals[InstinctChemIndex]    = 0.0f;
        }
    }

    public bool IsProcessingInstincts => _processingInstincts;
    public int  InstinctsRemaining    => _instincts.Count;

    // -------------------------------------------------------------------------
    // IBrainLocusProvider — Biochemistry calls this to bind neuron state loci
    // -------------------------------------------------------------------------
    public FloatLocus GetBrainLocus(int tissueId, int locusIdx)
    {
        var key = (tissueId, locusIdx);
        if (_brainLoci.TryGetValue(key, out FloatLocus? cached))
            return cached;

        Lobe? lobe = GetLobeFromTissueId(tissueId);
        if (lobe == null)
            return FloatLocus.DefaultNeuronInput;

        int neuronIdx = locusIdx / BrainConst.NoOfVariablesAvailableAsLoci;
        int stateVar  = locusIdx % BrainConst.NoOfVariablesAvailableAsLoci;

        if (neuronIdx >= lobe.GetNoOfNeurons())
            return FloatLocus.DefaultNeuronInput;

        // Backed locus: reads/writes go directly to neuron.States[stateVar]
        var locus = new FloatLocus(lobe.GetNeuron(neuronIdx).States, stateVar);
        _brainLoci[key] = locus;
        return locus;
    }

    // -------------------------------------------------------------------------
    // Input / output interface (used by Instinct and external code)
    // -------------------------------------------------------------------------
    public void SetInput(int lobeToken, int neuron, float value)
        => GetLobeFromToken(lobeToken)?.SetNeuronInput(neuron, value);

    public void SetInputByToken(int lobeToken, int neuron, float value)
        => SetInput(lobeToken, neuron, value);

    public int GetWinningId(int lobeToken)
        => GetLobeFromToken(lobeToken)?.GetWhichNeuronWon() ?? 0;

    public int GetWinningIdByToken(int lobeToken)
        => GetWinningId(lobeToken);

    public float GetChemicalLevel(int chemicalId)
    {
        if (_chemicals == null || (uint)chemicalId >= (uint)_chemicals.Length)
            return 0.0f;
        return _chemicals[chemicalId];
    }

    public float ReadPort(BrainPort port)
    {
        if (port.Kind == BrainPortKind.Chemical)
            return port.Index.HasValue ? GetChemicalLevel(port.Index.Value) : 0.0f;

        if (!port.LobeToken.HasValue)
            return 0.0f;

        Lobe? lobe = GetLobeFromToken(port.LobeToken.Value);
        if (lobe == null)
            return 0.0f;

        if (port.Kind == BrainPortKind.Motor)
            return GetWinningId(port.LobeToken.Value);

        int index = port.Index ?? 0;
        return port.Kind switch
        {
            BrainPortKind.Drive => lobe.GetNeuronState(index, NeuronVar.State),
            BrainPortKind.LobeInput => lobe.GetNeuronState(index, NeuronVar.Input),
            BrainPortKind.LobeOutput => lobe.GetNeuronState(index, NeuronVar.Output),
            _ => 0.0f,
        };
    }

    public void ClearActivity()
    {
        foreach (Lobe lobe in _lobes)
            lobe.ClearActivity();
    }

    // -------------------------------------------------------------------------
    // Lobe lookup helpers
    // -------------------------------------------------------------------------
    private Lobe? GetLobeFromTissueId(int tissueId)
    {
        foreach (Lobe l in _lobes)
            if (l.GetTissueId() == tissueId) return l;
        return null;
    }

    private Lobe? GetLobeFromToken(int token)
    {
        foreach (Lobe l in _lobes)
            if (l.Token == token) return l;
        return null;
    }

    // -------------------------------------------------------------------------
    // Accessors used by Instinct
    // -------------------------------------------------------------------------

    /// <summary>Returns the lobe token (packed 4-char int) for a given tissue ID, or 0 if not found.</summary>
    public int GetLobeTokenFromTissueId(int tissueId)
        => GetLobeFromTissueId(tissueId)?.Token ?? 0;

    /// <summary>
    /// Maps a script offset (verb/decision byte from instinct gene) to a neuron ID.
    /// In c2e this is done via script table lookup; here we pass through unchanged
    /// as a temporary pass-through — correct mapping requires the full CAOS script table.
    /// </summary>
    public int GetNeuronIdFromScriptOffset(int scriptOffset) => scriptOffset;

    // -------------------------------------------------------------------------
    // Read-only accessors
    // -------------------------------------------------------------------------
    public int LobeCount  => _lobes.Count;
    public int TractCount => _tracts.Count;

    public Lobe? GetLobe(int index)
        => (uint)index < (uint)_lobes.Count ? _lobes[index] : null;

    public Lobe? GetLobeByToken(int token)
        => GetLobeFromToken(token);

    public Tract? GetTract(int index)
        => (uint)index < (uint)_tracts.Count ? _tracts[index] : null;

    public BrainSnapshot CreateSnapshot()
        => CreateSnapshot(new BrainSnapshotOptions());

    public BrainSnapshot CreateSnapshot(BrainSnapshotOptions options)
    {
        var lobes = new List<LobeSnapshot>(_lobes.Count);
        for (int i = 0; i < _lobes.Count; i++)
            lobes.Add(_lobes[i].CreateSnapshot(i, options.MaxNeuronsPerLobe));

        var tracts = new List<TractSnapshot>(_tracts.Count);
        for (int i = 0; i < _tracts.Count; i++)
            tracts.Add(_tracts[i].CreateSnapshot(i, options.MaxDendritesPerTract));

        return new BrainSnapshot(
            lobes,
            tracts,
            GetModuleDescriptors(),
            _instincts.Count,
            _processingInstincts);
    }

    // -------------------------------------------------------------------------
    // Utility: pack a 4-char ASCII string into an int token (matches c2e TOKEN)
    // -------------------------------------------------------------------------
    public static int TokenFromString(string s)
    {
        if (s.Length < 4) s = s.PadRight(4);
        return s[0] | (s[1] << 8) | (s[2] << 16) | (s[3] << 24);
    }

    public static string TokenToString(int token)
    {
        Span<char> chars = stackalloc char[4];
        chars[0] = (char)(token & 0xFF);
        chars[1] = (char)((token >> 8) & 0xFF);
        chars[2] = (char)((token >> 16) & 0xFF);
        chars[3] = (char)((token >> 24) & 0xFF);
        return new string(chars).TrimEnd();
    }
}
