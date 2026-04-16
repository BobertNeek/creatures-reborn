namespace CreaturesReborn.Sim.Agent;

/// <summary>
/// Well-known script event IDs matching c2e's event numbering.
/// In c2e, scripts are identified by (classifier + event number).
/// A script "scrp 2 5 1 4" means family=2 genus=5 species=1 event=4 (activate1).
/// </summary>
public static class ScriptEvent
{
    // ── Standard agent events ───────────────────────────────────────────────
    public const int Deactivate  = 0;
    public const int Activate1   = 1;
    public const int Activate2   = 2;
    public const int Hit         = 3;
    public const int Pickup      = 4;
    public const int Drop        = 5;
    public const int Eat         = 12;
    public const int HoldHandsWithCreature = 13;
    public const int Timer       = 9;
    public const int Collision   = 6;

    // ── Creature-specific events ────────────────────────────────────────────
    // These are the events fired on creatures (family 4) by their own
    // decision system. In c2e, creature scripts start at event 16+.
    public const int CreatureQuiescent  = 16;
    public const int CreatureActivate1  = 17;
    public const int CreatureActivate2  = 18;
    public const int CreatureDeactivate = 19;
    public const int CreatureApproach   = 20;
    public const int CreatureRetreat    = 21;
    public const int CreatureGet        = 22;
    public const int CreatureDrop       = 23;
    public const int CreatureEat        = 24;
    public const int CreatureHit        = 25;
    public const int CreatureTravelEast = 26;
    public const int CreatureTravelWest = 27;
    public const int CreatureSay        = 28;
    public const int CreaturePush       = 32;
    public const int CreaturePull       = 33;

    // ── Involuntary actions (64+) ───────────────────────────────────────────
    public const int InvoluntaryFlinch      = 64;
    public const int InvoluntaryLayEgg      = 65;
    public const int InvoluntarySneeze      = 66;
    public const int InvoluntaryCough       = 67;
    public const int InvoluntaryShiver      = 68;
    public const int InvoluntarySleep       = 69;
    public const int InvoluntaryDie         = 70;
    public const int InvoluntaryFaint       = 71;
    public const int InvoluntaryDream       = 72;

    // ── Done-to events (0+ on the target agent) ─────────────────────────────
    public const int DoneToPush  = 0;
    public const int DoneToPull  = 1;
    public const int DoneToStop  = 2;
    public const int DoneToHit   = 3;
    public const int DoneToEat   = 12;
}
