namespace CreaturesReborn.Sim.Brain;

using G = CreaturesReborn.Sim.Genome.Genome;

/// <summary>
/// One hardwired brain reflex, loaded from a CREATUREGENE/G_INSTINCT gene.
/// Processed during REM sleep to burn in long-term weights.
/// Direct port of c2e's <c>Instinct</c> class (Instinct.h / Instinct.cpp).
/// </summary>
public sealed class Instinct
{
    private const int MaxInputs              = 3;
    private const float ReinforcementMod    = 0.5f;

    private readonly Brain _brain;

    private struct Input
    {
        public int    LobeToken;   // packed 4-char token
        public int    NeuronId;
    }

    private readonly Input[] _inputs = new Input[MaxInputs];
    private int   _decisionId;
    private int   _driveId;
    private float _reinforcementAmount;

    // -------------------------------------------------------------------------
    // Constructor — reads one G_INSTINCT gene
    // -------------------------------------------------------------------------
    public Instinct(G genome, Brain brain)
    {
        _brain = brain;

        for (int i = 0; i < MaxInputs; i++)
        {
            int tissueId = genome.GetByte() - 1; // 255-1 = invalid
            _inputs[i].NeuronId  = genome.GetByte();
            _inputs[i].LobeToken = brain.GetLobeTokenFromTissueId(tissueId);
        }

        int decisionScriptId = genome.GetByte();
        _decisionId          = brain.GetNeuronIdFromScriptOffset(decisionScriptId);

        _driveId             = genome.GetCodon(0, 255);
        _reinforcementAmount = genome.GetSignedFloat();
    }

    // -------------------------------------------------------------------------
    // Process — one tick of instinct application; returns true when complete
    // -------------------------------------------------------------------------
    public bool Process()
    {
        _brain.ClearActivity();

        // Fire each input neuron
        for (int i = 0; i < MaxInputs; i++)
        {
            int lobeToken = _inputs[i].LobeToken;
            int neuronId  = _inputs[i].NeuronId;
            if (lobeToken == 0) continue; // invalid

            // verb lobe remap
            int verbToken = Brain.TokenFromString("verb");
            if (lobeToken == verbToken)
                neuronId = _brain.GetNeuronIdFromScriptOffset(neuronId);

            int nounToken = Brain.TokenFromString("noun");
            if (lobeToken == nounToken)
            {
                _brain.SetInputByToken(Brain.TokenFromString("visn"), neuronId, 0.1f);
                _brain.SetInputByToken(Brain.TokenFromString("smel"), neuronId, 1.0f);
            }

            _brain.SetInputByToken(lobeToken, neuronId, 1.0f);
        }

        // Force decision via verb lobe, then run the component stack.
        // The stack is ordered (verb t=18 → comb t=25 → decn t=28) so one pass
        // propagates the signal; run twice for good measure.
        _brain.SetInputByToken(Brain.TokenFromString("verb"), _decisionId, 1.0f);
        _brain.UpdateComponents();
        _brain.UpdateComponents();

        // If the comb→decn tract weights are still near-zero (early life) the decn
        // WTA won't reflect the verb input.  In that case force the decn neuron
        // directly so reinforcement can still burn in the correct LT weight.
        if (_brain.GetWinningIdByToken(Brain.TokenFromString("decn")) != _decisionId)
        {
            _brain.SetInputByToken(Brain.TokenFromString("decn"), _decisionId, 1.0f);
            _brain.UpdateComponents();
            if (_brain.GetWinningIdByToken(Brain.TokenFromString("decn")) != _decisionId)
                return true;   // decn still won't cooperate — skip this instinct
        }

        // Send reinforcement to the resp lobe so active tract dendrites
        // update their long-term weights toward the correct decision.
        _brain.SetInputByToken(Brain.TokenFromString("resp"),
            _driveId, ReinforcementMod * _reinforcementAmount);
        _brain.UpdateComponents();

        return true;
    }
}
