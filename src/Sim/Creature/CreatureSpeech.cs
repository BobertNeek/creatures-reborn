using System;
using System.Linq;

namespace CreaturesReborn.Sim.Creature;

public enum SpeechIntentKind
{
    Unknown = 0,
    Command,
    Attention,
    What,
    Express,
    Praise,
    Punish,
}

public readonly record struct CreatureSpeechSuggestion(
    string OriginalText,
    SpeechIntentKind Intent,
    string SubjectWord,
    string VerbWord,
    int? VerbId,
    string NounWord,
    int? ObjectCategory,
    float Confidence)
{
    public bool IsRecognized => Intent != SpeechIntentKind.Unknown || VerbId.HasValue || ObjectCategory.HasValue;

    public CreatureSpeechSuggestion(
        string originalText,
        string verbWord,
        int? verbId,
        string nounWord,
        int? objectCategory,
        float confidence)
        : this(originalText, SpeechIntentKind.Command, string.Empty, verbWord, verbId, nounWord, objectCategory, confidence)
    {
    }
}

public static class CreatureSpeechParser
{
    public static CreatureSpeechSuggestion Parse(string text, CreatureVocabulary vocabulary)
    {
        string original = text ?? string.Empty;
        string[] words = original
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim().ToLowerInvariant())
            .ToArray();

        string verbWord = string.Empty;
        string nounWord = string.Empty;
        string subjectWord = string.Empty;
        int? verbId = null;
        int? objectCategory = null;
        float confidence = 0.0f;
        SpeechIntentKind intent = SpeechIntentKind.Unknown;

        if (words.Length == 0)
            return Unknown(original);

        if (words.Length > 1 && IsQueryWord(words[1], "what"))
            return IntentOnly(original, SpeechIntentKind.What, words[0], confidence: 0.9f);
        if (words.Length > 1 && IsQueryWord(words[1], "express"))
            return IntentOnly(original, SpeechIntentKind.Express, words[0], confidence: 0.9f);
        if (IsQueryWord(words[0], "what"))
            return IntentOnly(original, SpeechIntentKind.What, confidence: 0.9f);
        if (IsQueryWord(words[0], "express"))
            return IntentOnly(original, SpeechIntentKind.Express, confidence: 0.9f);
        if (IsPraiseWord(words[0]))
            intent = SpeechIntentKind.Praise;
        else if (IsPunishWord(words[0]))
            intent = SpeechIntentKind.Punish;

        int startIndex = 0;
        if (words.Length > 1 && ContainsKnownVerbAfterFirst(words, vocabulary))
        {
            CreatureVocabulary.VocabEntry? first = vocabulary.Lookup(words[0]);
            if (first == null || !first.Value.IsVerb)
            {
                subjectWord = words[0];
                startIndex = 1;
            }
        }

        for (int i = startIndex; i < words.Length; i++)
        {
            CreatureVocabulary.VocabEntry? entry = vocabulary.Lookup(words[i]);
            if (entry == null)
            {
                if (i == 0 && words.Length > 1)
                    subjectWord = words[i];
                continue;
            }

            if (entry.Value.IsVerb && verbId == null)
            {
                verbWord = words[i];
                verbId = entry.Value.Id;
                confidence = Math.Max(confidence, entry.Value.Confidence);
            }
            else if (!entry.Value.IsVerb && objectCategory == null)
            {
                nounWord = words[i];
                objectCategory = entry.Value.Id;
                confidence = Math.Max(confidence, entry.Value.Confidence);
            }
        }

        if (intent == SpeechIntentKind.Unknown)
        {
            if (verbId.HasValue)
                intent = SpeechIntentKind.Command;
            else if (objectCategory.HasValue)
                intent = SpeechIntentKind.Attention;
        }

        if (string.Equals(verbWord, "come", StringComparison.OrdinalIgnoreCase) && objectCategory == null)
        {
            nounWord = "hand";
            objectCategory = ObjectCategory.Hand;
            confidence = Math.Max(confidence, 0.45f);
        }

        return new CreatureSpeechSuggestion(
            original,
            intent,
            subjectWord,
            verbWord,
            verbId,
            nounWord,
            objectCategory,
            Math.Clamp(confidence, 0.0f, 1.0f));
    }

    private static CreatureSpeechSuggestion Unknown(string original)
        => new(original, SpeechIntentKind.Unknown, string.Empty, string.Empty, null, string.Empty, null, 0);

    private static CreatureSpeechSuggestion IntentOnly(
        string original,
        SpeechIntentKind intent,
        string subjectWord = "",
        float confidence = 0.9f)
        => new(original, intent, subjectWord, string.Empty, null, string.Empty, null, confidence);

    private static bool IsQueryWord(string word, string query)
        => string.Equals(word, query, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsKnownVerbAfterFirst(string[] words, CreatureVocabulary vocabulary)
    {
        for (int i = 1; i < words.Length; i++)
        {
            CreatureVocabulary.VocabEntry? entry = vocabulary.Lookup(words[i]);
            if (entry is { IsVerb: true })
                return true;
        }

        return false;
    }

    private static bool IsPraiseWord(string word)
        => word is "good" or "yes" or "nice" or "reward";

    private static bool IsPunishWord(string word)
        => word is "bad" or "no" or "punish";
}

public static class CreatureSpeechResponse
{
    public static string FormatAction(string creatureName, int verbId, int objectCategory, CreatureVocabulary vocabulary)
    {
        string verb = vocabulary.FindKnownWord(isVerb: true, verbId) ?? VerbName(verbId);
        string noun = vocabulary.FindKnownWord(isVerb: false, objectCategory) ?? CategoryName(objectCategory);
        return $"{CleanName(creatureName)} {verb} {noun}".Trim();
    }

    public static string FormatNeed(string creatureName, int driveId)
        => $"{CleanName(creatureName)} very {DriveName(driveId)}";

    private static string CleanName(string creatureName)
        => string.IsNullOrWhiteSpace(creatureName) ? "norn" : creatureName.Trim();

    private static string VerbName(int verbId)
        => verbId switch
        {
            VerbId.Activate1 => "push",
            VerbId.Activate2 => "pull",
            VerbId.Deactivate => "stop",
            VerbId.Approach => "approach",
            VerbId.Retreat => "retreat",
            VerbId.Get => "get",
            VerbId.Drop => "drop",
            VerbId.ExpressNeed => "express",
            VerbId.Rest => "rest",
            VerbId.TravelWest => "left",
            VerbId.TravelEast => "right",
            VerbId.Eat => "eat",
            VerbId.Hit => "hit",
            _ => "idle",
        };

    private static string CategoryName(int objectCategory)
        => objectCategory switch
        {
            ObjectCategory.Hand => "hand",
            ObjectCategory.Door => "door",
            ObjectCategory.Seed => "seed",
            ObjectCategory.Plant => "plant",
            ObjectCategory.Fruit => "fruit",
            ObjectCategory.Food => "food",
            ObjectCategory.Button => "button",
            ObjectCategory.Bug => "bug",
            ObjectCategory.Critter => "critter",
            ObjectCategory.Norn => "norn",
            ObjectCategory.Grendel => "grendel",
            ObjectCategory.Ettin => "ettin",
            ObjectCategory.Toy => "toy",
            ObjectCategory.Instrument => "instrument",
            ObjectCategory.Dispenser => "dispenser",
            ObjectCategory.Tool => "tool",
            ObjectCategory.Elevator => "elevator",
            ObjectCategory.Machine => "machine",
            ObjectCategory.Egg => "egg",
            _ => "something",
        };

    private static string DriveName(int driveId)
        => driveId switch
        {
            DriveId.Pain => "hurt",
            DriveId.HungerForProtein or DriveId.HungerForCarb or DriveId.HungerForFat => "hungry",
            DriveId.Coldness => "cold",
            DriveId.Hotness => "hot",
            DriveId.Tiredness or DriveId.Sleepiness => "tired",
            DriveId.Loneliness => "lonely",
            DriveId.Crowdedness => "crowded",
            DriveId.Fear => "scared",
            DriveId.Boredom => "bored",
            DriveId.Anger => "angry",
            DriveId.SexDrive => "friendly",
            DriveId.Comfort => "comfortable",
            _ => "confused",
        };
}
