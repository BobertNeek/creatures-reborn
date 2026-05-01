using System;
using System.Linq;

namespace CreaturesReborn.Sim.Creature;

public readonly record struct CreatureSpeechSuggestion(
    string OriginalText,
    string VerbWord,
    int? VerbId,
    string NounWord,
    int? ObjectCategory,
    float Confidence)
{
    public bool IsRecognized => VerbId.HasValue || ObjectCategory.HasValue;
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
        int? verbId = null;
        int? objectCategory = null;
        float confidence = 0.0f;

        for (int i = 0; i < words.Length; i++)
        {
            CreatureVocabulary.VocabEntry? entry = vocabulary.Lookup(words[i]);
            if (entry == null)
                continue;

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

        if (string.Equals(verbWord, "come", StringComparison.OrdinalIgnoreCase) && objectCategory == null)
        {
            nounWord = "hand";
            objectCategory = ObjectCategory.Hand;
            confidence = Math.Max(confidence, 0.45f);
        }

        return new CreatureSpeechSuggestion(
            original,
            verbWord,
            verbId,
            nounWord,
            objectCategory,
            Math.Clamp(confidence, 0.0f, 1.0f));
    }
}
