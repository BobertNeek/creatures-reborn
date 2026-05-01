using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.Creature;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class AgentCatalogTests
{
    [Theory]
    [InlineData(FoodKind.Fruit, ObjectCategory.Fruit, "fruit")]
    [InlineData(FoodKind.Seed, ObjectCategory.Seed, "seed")]
    [InlineData(FoodKind.Food, ObjectCategory.Food, "food")]
    public void FoodKinds_MapToDistinctC3DsClassifiersAndNouns(
        FoodKind kind,
        int expectedCategory,
        string expectedNoun)
    {
        AgentArchetype archetype = AgentCatalog.ForFood(kind);

        Assert.Equal(expectedCategory, archetype.ObjectCategory);
        Assert.Equal(expectedNoun, archetype.Noun);
        Assert.True(archetype.IsEdible);
        Assert.Equal(AgentFamily.Simple, archetype.Classifier.Family);
    }

    [Fact]
    public void CoreAgents_HaveStableClassifierContracts()
    {
        Assert.Equal(ObjectCategory.Egg, AgentCatalog.Egg.ObjectCategory);
        Assert.Equal(ObjectCategory.Norn, AgentCatalog.Norn.ObjectCategory);
        Assert.Equal(ObjectCategory.Hand, AgentCatalog.Hand.ObjectCategory);
        Assert.Equal(ObjectCategory.Dispenser, AgentCatalog.EmpathicVendor.ObjectCategory);
        Assert.Equal(AgentFamily.Creature, AgentCatalog.Norn.Classifier.Family);
        Assert.Equal(AgentFamily.Pointer, AgentCatalog.Hand.Classifier.Family);
    }

    [Fact]
    public void C3DsInspiredAgents_HaveVocabularyNounsAndSpriteTokens()
    {
        AgentArchetype[] archetypes =
        {
            AgentCatalog.EmpathicVendor,
            AgentCatalog.LearningMachine,
            AgentCatalog.Musicola,
            AgentCatalog.TrainingDummy,
            AgentCatalog.RobotToy,
            AgentCatalog.Incubator,
        };

        foreach (AgentArchetype archetype in archetypes)
        {
            Assert.False(string.IsNullOrWhiteSpace(archetype.Noun));
            Assert.False(string.IsNullOrWhiteSpace(archetype.SpriteToken));
        }
    }
}
