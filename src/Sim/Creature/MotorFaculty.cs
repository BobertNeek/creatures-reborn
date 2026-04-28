namespace CreaturesReborn.Sim.Creature;

using CreaturesReborn.Sim.Save;
using BrainCls = CreaturesReborn.Sim.Brain.Brain;

/// <summary>
/// Reads the "decn" (decision) and "attn" (attention) WTA winners from the brain
/// and translates them into a verb+noun action request.
/// </summary>
public sealed class MotorFaculty
{
    private readonly BrainCls _brain;

    private readonly int _decnToken = BrainCls.TokenFromString("decn");
    private readonly int _attnToken = BrainCls.TokenFromString("attn");
    private readonly int _verbToken = BrainCls.TokenFromString("verb");
    private readonly int _nounToken = BrainCls.TokenFromString("noun");
    private readonly int _drivToken = BrainCls.TokenFromString("driv");

    // Current resolved decision
    public int CurrentVerb { get; private set; } = VerbId.Default;
    public int CurrentNoun { get; private set; } = 0;
    public int CurrentGait { get; private set; } = 0;
    public int CurrentPose { get; private set; } = 0;

    public MotorFaculty(BrainCls brain)
    {
        _brain = brain;
    }

    /// <summary>Read WTA winners and update CurrentVerb/CurrentNoun/CurrentGait.</summary>
    public void Resolve()
    {
        CurrentVerb = _brain.GetWinningId(_decnToken);
        CurrentNoun = _brain.GetWinningId(_attnToken);
        CurrentGait = VerbToGait(CurrentVerb);
        CurrentPose = 0;
    }

    public void SetDriveInput(int driveId, float level)
        => _brain.SetInput(_drivToken, driveId, level);

    public void SuggestVerb(int verbId, float strength = 1.0f)
        => _brain.SetInput(_verbToken, verbId, strength);

    public void SuggestNoun(int nounId, float strength = 1.0f)
        => _brain.SetInput(_nounToken, nounId, strength);

    public SavedMotorState CreateSaveState()
        => new()
        {
            CurrentVerb = CurrentVerb,
            CurrentNoun = CurrentNoun,
            CurrentGait = CurrentGait,
            CurrentPose = CurrentPose,
        };

    public void RestoreSaveState(SavedMotorState state)
    {
        CurrentVerb = state.CurrentVerb;
        CurrentNoun = state.CurrentNoun;
        CurrentGait = state.CurrentGait;
        CurrentPose = state.CurrentPose;
    }

    private static int VerbToGait(int verb) => verb switch
    {
        VerbId.TravelWest => 1,
        VerbId.TravelEast => 2,
        VerbId.Rest       => 3,
        _                 => 0,
    };
}
