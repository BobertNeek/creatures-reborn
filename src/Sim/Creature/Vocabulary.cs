using System.Collections.Generic;

namespace CreaturesReborn.Sim.Creature;

/// <summary>
/// Object categories matching c2e's creature vocabulary system.
/// Each agent has a category (classifier → category mapping) that the
/// creature brain uses to recognise objects. The creature brain has a
/// "noun" lobe with one neuron per category.
///
/// When a creature "sees" an agent, the agent's category activates the
/// corresponding noun neuron, which feeds into the decision lobe.
/// </summary>
public static class ObjectCategory
{
    public const int Self       = 0;
    public const int Hand       = 1;   // the user's pointer
    public const int Door       = 2;
    public const int Seed       = 3;
    public const int Plant      = 4;
    public const int Weed       = 5;
    public const int Leaf       = 6;
    public const int Flower     = 7;
    public const int Fruit      = 8;
    public const int Manky      = 9;   // spoiled/rotting food
    public const int Detritus   = 10;
    public const int Food       = 11;
    public const int Button     = 12;
    public const int Bug        = 13;
    public const int Pest       = 14;
    public const int Critter    = 15;
    public const int Beast      = 16;
    public const int Nest       = 17;
    public const int Animal     = 18;
    public const int Norn       = 19;
    public const int Grendel    = 20;
    public const int Ettin      = 21;
    public const int Geat       = 22;
    public const int Toy        = 23;
    public const int Instrument = 24;
    public const int Dispenser  = 25;  // empathic vendor, etc.
    public const int Tool       = 26;
    public const int Potion     = 27;
    public const int Elevator   = 28;
    public const int Teleporter = 29;
    public const int Machine    = 30;
    public const int Egg        = 31;
    public const int Weather    = 32;
    public const int Bad        = 33;  // hazards, detritus
    public const int Count      = 40;
}

/// <summary>
/// Creature vocabulary — maps words the creature can learn to
/// object categories and actions. In c2e, creatures learn words
/// via the "Holistic Learning Machine" and through interaction.
///
/// Each word associates with either an object category ("food",
/// "norn", "toy") or an action/verb ("push", "eat", "come").
/// </summary>
public sealed class CreatureVocabulary
{
    /// <summary>Known words with confidence level (0-1).</summary>
    private readonly Dictionary<string, VocabEntry> _words = new();

    public readonly struct VocabEntry
    {
        /// <summary>Is this word an action/verb (true) or a noun/object (false)?</summary>
        public readonly bool IsVerb;
        /// <summary>Object category (if noun) or verb ID (if verb).</summary>
        public readonly int  Id;
        /// <summary>How well the creature knows this word (0-1).</summary>
        public readonly float Confidence;

        public VocabEntry(bool isVerb, int id, float confidence)
        {
            IsVerb     = isVerb;
            Id         = id;
            Confidence = confidence;
        }
    }

    /// <summary>Teach the creature a word (or reinforce existing knowledge).</summary>
    public void Learn(string word, bool isVerb, int id, float reinforcement = 0.2f)
    {
        word = Normalize(word);
        if (_words.TryGetValue(word, out var existing))
        {
            // Reinforce existing knowledge
            float newConf = System.Math.Min(existing.Confidence + reinforcement, 1.0f);
            _words[word] = new VocabEntry(existing.IsVerb, existing.Id, newConf);
        }
        else
        {
            _words[word] = new VocabEntry(isVerb, id, reinforcement);
        }
    }

    public void LearnNounAlias(string word, int objectCategory, float reinforcement = 0.2f)
        => Learn(word, false, objectCategory, reinforcement);

    public void LearnVerbAlias(string word, int verbId, float reinforcement = 0.2f)
        => Learn(word, true, verbId, reinforcement);

    /// <summary>Look up a word. Returns null if unknown.</summary>
    public VocabEntry? Lookup(string word)
    {
        word = Normalize(word);
        return _words.TryGetValue(word, out var entry) ? entry : null;
    }

    /// <summary>Get all known words.</summary>
    public IReadOnlyDictionary<string, VocabEntry> AllWords => _words;

    /// <summary>Seed with the DS default vocabulary.</summary>
    public void SeedDefaultVocab()
    {
        // Nouns
        Learn("food",      false, ObjectCategory.Food,       0.4f);
        Learn("fruit",     false, ObjectCategory.Fruit,      0.3f);
        Learn("seed",      false, ObjectCategory.Seed,       0.3f);
        Learn("norn",      false, ObjectCategory.Norn,       0.5f);
        Learn("grendel",   false, ObjectCategory.Grendel,    0.3f);
        Learn("ettin",     false, ObjectCategory.Ettin,      0.3f);
        Learn("hand",      false, ObjectCategory.Hand,       0.5f);
        Learn("toy",       false, ObjectCategory.Toy,        0.3f);
        Learn("door",      false, ObjectCategory.Door,       0.3f);
        Learn("egg",       false, ObjectCategory.Egg,        0.3f);
        Learn("plant",     false, ObjectCategory.Plant,      0.3f);
        Learn("machine",   false, ObjectCategory.Machine,    0.2f);
        Learn("elevator",  false, ObjectCategory.Elevator,   0.2f);
        Learn("bug",       false, ObjectCategory.Bug,        0.2f);
        Learn("critter",   false, ObjectCategory.Critter,    0.2f);
        Learn("dispenser", false, ObjectCategory.Dispenser,  0.2f);

        // Verbs
        Learn("push",      true, VerbId.Activate1,   0.4f);
        Learn("pull",      true, VerbId.Activate2,   0.3f);
        Learn("eat",       true, VerbId.Eat,         0.5f);
        Learn("get",       true, VerbId.Get,         0.4f);
        Learn("drop",      true, VerbId.Drop,        0.3f);
        Learn("come",      true, VerbId.Approach,    0.4f);
        Learn("run",       true, VerbId.Retreat,     0.3f);
        Learn("hit",       true, VerbId.Hit,         0.2f);
        Learn("rest",      true, VerbId.Rest,        0.3f);
        Learn("left",      true, VerbId.TravelWest,  0.3f);
        Learn("right",     true, VerbId.TravelEast,  0.3f);
    }

    private static string Normalize(string word)
        => word.Trim().ToLowerInvariant();
}
