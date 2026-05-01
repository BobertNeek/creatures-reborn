using CreaturesReborn.Sim.Creature;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class CreatureSpeechMemoryTests
{
    [Fact]
    public void SpatialMemory_ReinforcesObjectClassLocations()
    {
        var memory = new CreatureSpatialMemory();

        memory.Observe(ObjectCategory.Food, "food", 4.0f, 2.0f, roomId: 7, tick: 10);
        memory.Observe(ObjectCategory.Food, "food", 5.0f, 3.0f, roomId: 7, tick: 15);

        CreatureObjectMemory remembered = Assert.Single(memory.AllMemories);
        Assert.Equal(ObjectCategory.Food, remembered.ObjectCategory);
        Assert.Equal("food", remembered.Noun);
        Assert.Equal(4.5f, remembered.X, precision: 3);
        Assert.Equal(2.5f, remembered.Y, precision: 3);
        Assert.Equal(2, remembered.ObservationCount);
        Assert.True(remembered.Confidence > 0.5f);
    }

    [Fact]
    public void SpeechParser_ResolvesVerbNounSuggestionsThroughVocabulary()
    {
        var vocabulary = new CreatureVocabulary();
        vocabulary.SeedDefaultVocab();

        CreatureSpeechSuggestion suggestion = CreatureSpeechParser.Parse("push toy", vocabulary);

        Assert.True(suggestion.IsRecognized);
        Assert.Equal(VerbId.Activate1, suggestion.VerbId);
        Assert.Equal(ObjectCategory.Toy, suggestion.ObjectCategory);
        Assert.Equal("push", suggestion.VerbWord);
        Assert.Equal("toy", suggestion.NounWord);
    }

    [Fact]
    public void SpeechParser_TeachesUnknownNounWhenCategoryHintIsProvided()
    {
        var vocabulary = new CreatureVocabulary();
        vocabulary.SeedDefaultVocab();

        vocabulary.LearnNounAlias("bobble", ObjectCategory.Toy, reinforcement: 0.6f);
        CreatureSpeechSuggestion suggestion = CreatureSpeechParser.Parse("get bobble", vocabulary);

        Assert.True(suggestion.IsRecognized);
        Assert.Equal(VerbId.Get, suggestion.VerbId);
        Assert.Equal(ObjectCategory.Toy, suggestion.ObjectCategory);
    }

    [Fact]
    public void Vocabulary_ReinforcesExistingWordsWithoutChangingMeaning()
    {
        var vocabulary = new CreatureVocabulary();

        vocabulary.Learn("food", isVerb: false, ObjectCategory.Food, reinforcement: 0.3f);
        vocabulary.Learn("food", isVerb: false, ObjectCategory.Fruit, reinforcement: 0.4f);

        CreatureVocabulary.VocabEntry entry = vocabulary.Lookup("food")!.Value;
        Assert.False(entry.IsVerb);
        Assert.Equal(ObjectCategory.Food, entry.Id);
        Assert.Equal(0.7f, entry.Confidence, precision: 3);
    }
}
