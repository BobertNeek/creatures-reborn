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
    public void SpeechParser_ResolvesSubjectVerbNounNornish()
    {
        var vocabulary = new CreatureVocabulary();
        vocabulary.SeedDefaultVocab();

        CreatureSpeechSuggestion suggestion = CreatureSpeechParser.Parse("Alice approach food", vocabulary);

        Assert.True(suggestion.IsRecognized);
        Assert.Equal(SpeechIntentKind.Command, suggestion.Intent);
        Assert.Equal("alice", suggestion.SubjectWord);
        Assert.Equal(VerbId.Approach, suggestion.VerbId);
        Assert.Equal(ObjectCategory.Food, suggestion.ObjectCategory);
    }

    [Fact]
    public void SpeechParser_DoesNotTreatKnownCreatureSubjectAsObject()
    {
        var vocabulary = new CreatureVocabulary();
        vocabulary.SeedDefaultVocab();

        CreatureSpeechSuggestion suggestion = CreatureSpeechParser.Parse("norn eat food", vocabulary);

        Assert.Equal("norn", suggestion.SubjectWord);
        Assert.Equal(VerbId.Eat, suggestion.VerbId);
        Assert.Equal(ObjectCategory.Food, suggestion.ObjectCategory);
    }

    [Theory]
    [InlineData("what", SpeechIntentKind.What)]
    [InlineData("express", SpeechIntentKind.Express)]
    [InlineData("good norn", SpeechIntentKind.Praise)]
    [InlineData("bad norn", SpeechIntentKind.Punish)]
    public void SpeechParser_ResolvesC3DsQueryAndTeachingWords(string phrase, SpeechIntentKind expected)
    {
        var vocabulary = new CreatureVocabulary();
        vocabulary.SeedDefaultVocab();

        CreatureSpeechSuggestion suggestion = CreatureSpeechParser.Parse(phrase, vocabulary);

        Assert.True(suggestion.IsRecognized);
        Assert.Equal(expected, suggestion.Intent);
    }

    [Fact]
    public void SpeechParser_ResolvesAddressedWhatAndExpressQueries()
    {
        var vocabulary = new CreatureVocabulary();
        vocabulary.SeedDefaultVocab();

        CreatureSpeechSuggestion what = CreatureSpeechParser.Parse("Alice what", vocabulary);
        CreatureSpeechSuggestion express = CreatureSpeechParser.Parse("Alice express", vocabulary);

        Assert.Equal(SpeechIntentKind.What, what.Intent);
        Assert.Equal("alice", what.SubjectWord);
        Assert.Equal(SpeechIntentKind.Express, express.Intent);
        Assert.Equal("alice", express.SubjectWord);
    }

    [Fact]
    public void SpeechParser_NounOnlyPhraseAttractsAttentionToCategory()
    {
        var vocabulary = new CreatureVocabulary();
        vocabulary.SeedDefaultVocab();

        CreatureSpeechSuggestion suggestion = CreatureSpeechParser.Parse("food", vocabulary);

        Assert.True(suggestion.IsRecognized);
        Assert.Equal(SpeechIntentKind.Attention, suggestion.Intent);
        Assert.Null(suggestion.VerbId);
        Assert.Equal(ObjectCategory.Food, suggestion.ObjectCategory);
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

    [Fact]
    public void Vocabulary_HolisticTeachingRaisesCoreWordsToHighConfidence()
    {
        var vocabulary = new CreatureVocabulary();
        vocabulary.SeedDefaultVocab(initialConfidence: 0.05f);

        vocabulary.TeachHolisticVocabulary();

        Assert.True(vocabulary.Lookup("approach")!.Value.Confidence >= 0.95f);
        Assert.True(vocabulary.Lookup("food")!.Value.Confidence >= 0.95f);
        Assert.True(vocabulary.Lookup("very")!.Value.Confidence >= 0.95f);
    }

    [Fact]
    public void SpeechResponse_ReportsCurrentActionAndNeedInNornishShape()
    {
        var vocabulary = new CreatureVocabulary();
        vocabulary.SeedDefaultVocab();

        string action = CreatureSpeechResponse.FormatAction("Alice", VerbId.Eat, ObjectCategory.Food, vocabulary);
        string need = CreatureSpeechResponse.FormatNeed("Alice", DriveId.Tiredness);

        Assert.Equal("Alice eat food", action);
        Assert.Equal("Alice very tired", need);
    }
}
