using System;
using CreaturesReborn.Sim.Biochemistry;

namespace CreaturesReborn.Sim.Creature;

/// <summary>
/// The Stimulus system — the bridge between the world (agents, events) and
/// creature biochemistry. When a creature interacts with an object, a
/// stimulus is fired which injects chemicals into the creature.
///
/// This mirrors c2e's <c>stim writ</c> / <c>stim shou</c> / <c>stim sign</c>
/// CAOS commands. Each stimulus ID maps to a set of chemical injections.
///
/// Stimuli are how creatures learn: the reward/punishment chemicals
/// (Reward, Punishment, ReinforcementBase, etc.) train the brain's
/// decision→drive tract weights so the creature associates actions with
/// good or bad outcomes.
/// </summary>
public static class StimulusId
{
    // ── Interaction outcomes ────────────────────────────────────────────────
    public const int WallBump           = 0;
    public const int PatOnBack          = 1;   // tickle / positive hand interaction
    public const int Slap               = 2;   // slap / punishment
    public const int AteFruit           = 3;
    public const int AteProtein         = 4;
    public const int AteFat             = 5;
    public const int AteAlcohol         = 6;
    public const int BumpedIntoAgent    = 7;
    public const int GotHot             = 8;
    public const int GotCold            = 9;
    public const int Drowning           = 10;
    public const int CollidedWithEdge   = 11;
    public const int ApproachSuccess    = 12;
    public const int RetreatSuccess     = 13;
    public const int GotFood            = 14;
    public const int AteFoodSuccess     = 15;

    // ── Social ──────────────────────────────────────────────────────────────
    public const int ItCrowded          = 16;
    public const int ItApproached       = 17;
    public const int ItBumped           = 18;
    public const int ItMated            = 19;
    public const int FriendNearby       = 20;

    // ── Object interaction results ──────────────────────────────────────────
    public const int Activate1Good      = 21;
    public const int Activate1Bad       = 22;
    public const int PushedAgent        = 23;
    public const int PulledAgent        = 24;
    public const int GotToy             = 25;
    public const int PlayedWithToy      = 26;

    // ── Involuntary ─────────────────────────────────────────────────────────
    public const int Flinch             = 30;
    public const int LaidEgg            = 31;
    public const int Sneezed            = 32;
    public const int Coughed            = 33;
    public const int Shivered           = 34;
    public const int Fell               = 35;
    public const int Died               = 36;

    // ── Hand/pointer interactions (DS-specific) ─────────────────────────────
    public const int HandHeldCreature   = 40;
    public const int HandDroppedCreature= 41;

    // ── Vendor/gadget interactions ──────────────────────────────────────────
    public const int VendorGaveFood     = 90;

    public const int Count = 100;
}

/// <summary>
/// Defines what chemicals a single stimulus injects.
/// Up to 4 chemical adjustments per stimulus (matching c2e's 4-slot stim genes).
/// </summary>
public readonly struct StimulusDef
{
    public readonly int   Chem1, Chem2, Chem3, Chem4;
    public readonly float Amt1,  Amt2,  Amt3,  Amt4;
    /// <summary>Significance: controls how strongly the creature learns from this stim.
    /// 0 = don't learn, 1 = full reinforcement.</summary>
    public readonly float Significance;

    public StimulusDef(int c1, float a1, int c2 = 0, float a2 = 0,
                       int c3 = 0, float a3 = 0, int c4 = 0, float a4 = 0,
                       float significance = 1.0f)
    {
        Chem1 = c1; Amt1 = a1; Chem2 = c2; Amt2 = a2;
        Chem3 = c3; Amt3 = a3; Chem4 = c4; Amt4 = a4;
        Significance = significance;
    }
}

/// <summary>
/// The stimulus table — maps stimulus IDs to chemical injection definitions.
/// Populated with DS-accurate default values.
/// </summary>
public static class StimulusTable
{
    private static readonly StimulusDef[] _table = new StimulusDef[StimulusId.Count];

    static StimulusTable()
    {
        // Eating fruit (carbs) → reduce hunger for carb, inject glycogen, reward
        _table[StimulusId.AteFruit] = new(
            ChemID.HungerForCarb, -0.5f,
            ChemID.Glycogen, 0.4f,
            ChemID.Reward, 0.3f,
            significance: 1.0f);

        // Eating protein
        _table[StimulusId.AteProtein] = new(
            ChemID.HungerForProtein, -0.5f,
            ChemID.Muscle, 0.3f,
            ChemID.Reward, 0.2f,
            significance: 1.0f);

        // Eating fat
        _table[StimulusId.AteFat] = new(
            ChemID.HungerForFat, -0.5f,
            ChemID.Adipose, 0.3f,
            ChemID.Reward, 0.2f,
            significance: 1.0f);

        // Pat on back (tickle) — positive reinforcement
        _table[StimulusId.PatOnBack] = new(
            ChemID.Reward, 0.4f,
            ChemID.Endorphin, 0.2f,
            ChemID.Loneliness, -0.3f,
            significance: 1.0f);

        // Slap — negative reinforcement
        _table[StimulusId.Slap] = new(
            ChemID.Punishment, 0.4f,
            ChemID.Pain, 0.3f,
            ChemID.Fear, 0.2f,
            significance: 1.0f);

        // Wall bump — mild pain/fear
        _table[StimulusId.WallBump] = new(
            ChemID.Pain, 0.15f,
            ChemID.Fear, 0.1f,
            significance: 0.5f);

        // Got food (picked up)
        _table[StimulusId.GotFood] = new(
            ChemID.Reward, 0.1f,
            significance: 0.5f);

        // Ate food successfully
        _table[StimulusId.AteFoodSuccess] = new(
            ChemID.Glycogen, 0.3f,
            ChemID.Reward, 0.3f,
            ChemID.HungerForCarb, -0.4f,
            significance: 1.0f);

        // Approach success
        _table[StimulusId.ApproachSuccess] = new(
            ChemID.Reward, 0.05f,
            significance: 0.3f);

        // Retreat success
        _table[StimulusId.RetreatSuccess] = new(
            ChemID.Fear, -0.1f,
            ChemID.Reward, 0.05f,
            significance: 0.3f);

        // Social — friend nearby
        _table[StimulusId.FriendNearby] = new(
            ChemID.Loneliness, -0.2f,
            ChemID.Endorphin, 0.05f,
            significance: 0.3f);

        // Mated
        _table[StimulusId.ItMated] = new(
            ChemID.Progesterone, 0.8f,
            ChemID.Reward, 0.5f,
            ChemID.SexDrive, -0.8f,
            significance: 1.0f);

        // Crowded
        _table[StimulusId.ItCrowded] = new(
            ChemID.Crowdedness, 0.3f,
            ChemID.Punishment, 0.1f,
            significance: 0.4f);

        // Vendor gave food — mild positive
        _table[StimulusId.VendorGaveFood] = new(
            ChemID.Reward, 0.15f,
            significance: 0.5f);

        // Got hot
        _table[StimulusId.GotHot] = new(
            ChemID.Hotness, 0.3f,
            ChemID.Punishment, 0.1f,
            significance: 0.5f);

        // Got cold
        _table[StimulusId.GotCold] = new(
            ChemID.Coldness, 0.3f,
            ChemID.Punishment, 0.1f,
            significance: 0.5f);

        // Fell
        _table[StimulusId.Fell] = new(
            ChemID.Pain, 0.2f,
            ChemID.Fear, 0.15f,
            significance: 0.6f);

        // Hand held creature
        _table[StimulusId.HandHeldCreature] = new(
            ChemID.Fear, 0.1f,
            significance: 0.3f);

        // Hand dropped creature
        _table[StimulusId.HandDroppedCreature] = new(
            ChemID.Fear, -0.05f,
            significance: 0.2f);

        // Pushed agent — mild reward
        _table[StimulusId.PushedAgent] = new(
            ChemID.Boredom, -0.1f,
            ChemID.Reward, 0.05f,
            significance: 0.4f);

        // Played with toy
        _table[StimulusId.PlayedWithToy] = new(
            ChemID.Boredom, -0.3f,
            ChemID.Reward, 0.15f,
            significance: 0.6f);

        // Laid egg
        _table[StimulusId.LaidEgg] = new(
            ChemID.Progesterone, -0.5f,
            ChemID.Reward, 0.3f,
            significance: 0.8f);
    }

    /// <summary>Look up the chemical injection definition for a stimulus.</summary>
    public static StimulusDef Get(int stimId)
        => (uint)stimId < StimulusId.Count ? _table[stimId] : default;

    /// <summary>
    /// Apply a stimulus to a creature: inject the defined chemicals.
    /// </summary>
    public static void Apply(Creature creature, int stimId)
        => Apply(creature, stimId, trace: null);

    public static void Apply(Creature creature, int stimId, BiochemistryTrace? trace)
    {
        var def = Get(stimId);
        ApplyChemical(creature, stimId, def.Chem1, def.Amt1, trace);
        ApplyChemical(creature, stimId, def.Chem2, def.Amt2, trace);
        ApplyChemical(creature, stimId, def.Chem3, def.Amt3, trace);
        ApplyChemical(creature, stimId, def.Chem4, def.Amt4, trace);
    }

    private static void ApplyChemical(Creature creature, int stimId, int chem, float amount, BiochemistryTrace? trace)
    {
        if (chem == 0) return;
        creature.Biochemistry.AddChemical(chem, amount, ChemicalDeltaSource.Stimulus, $"stimulus:{stimId}", trace);
    }
}
