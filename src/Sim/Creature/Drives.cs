namespace CreaturesReborn.Sim.Creature;

/// <summary>
/// Drive neuron indices into the "driv" brain lobe.
/// Mirrors c2e's <c>driveoffsets</c> enum (CreatureConstants.h).
/// </summary>
public static class DriveId
{
    public const int Pain              = 0;
    public const int HungerForProtein  = 1;
    public const int HungerForCarb     = 2;
    public const int HungerForFat      = 3;
    public const int Coldness          = 4;
    public const int Hotness           = 5;
    public const int Tiredness         = 6;
    public const int Sleepiness        = 7;
    public const int Loneliness        = 8;
    public const int Crowdedness       = 9;
    public const int Fear              = 10;
    public const int Boredom           = 11;
    public const int Anger             = 12;
    public const int SexDrive          = 13;
    public const int Comfort           = 14;
    public const int Up                = 15;
    public const int Down              = 16;
    public const int Exit              = 17;
    public const int Enter             = 18;
    public const int Wait              = 19;
    public const int NumDrives         = 20;
}

/// <summary>
/// Verb / decision neuron indices into the "decn" and "verb" lobes.
/// Mirrors c2e's <c>decisionoffsets</c> enum (CreatureConstants.h).
/// </summary>
public static class VerbId
{
    public const int Default      = 0;
    public const int Activate1    = 1;
    public const int Activate2    = 2;
    public const int Deactivate   = 3;
    public const int Approach     = 4;
    public const int Retreat      = 5;
    public const int Get          = 6;
    public const int Drop         = 7;
    public const int ExpressNeed  = 8;
    public const int Rest         = 9;
    public const int TravelWest   = 10;
    public const int TravelEast   = 11;
    public const int Eat          = 12;
    public const int Hit          = 13;
    public const int NumVerbs     = 14;
}
