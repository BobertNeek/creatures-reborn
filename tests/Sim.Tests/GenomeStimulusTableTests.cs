using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class GenomeStimulusTableTests
{
    [Fact]
    public void FromGenome_DecodesStimulusGenesWithFourMappedChemicals()
    {
        byte[] stimulusPayload =
        [
            44,
            128,
            3,
            255,
            7,
            255, 0,
            0, 64,
            1, 128,
            2, 255
        ];
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Gene(
                (int)GeneType.CREATUREGENE,
                (int)CreatureSubtype.G_STIMULUS,
                id: 80,
                payload: stimulusPayload));

        GenomeStimulusTable table = GenomeStimulusTable.FromGenome(genome);

        Assert.True(table.HasGenomeAuthoredDefinitions);
        StimulusGeneDefinition definition = Assert.Single(table.Definitions);
        Assert.Equal(44, definition.StimulusId);
        Assert.Equal(128 / 255f, definition.Significance, precision: 6);
        Assert.Equal(3, definition.Input);
        Assert.Equal(255 / 255f, definition.Intensity, precision: 6);
        Assert.Equal(7, definition.Features);
        Assert.Equal(0, definition.ChemicalDeltas[0].ChemicalId);
        Assert.Equal(GenePayloadCodec.StimulusChemicalToBiochemical(0), definition.ChemicalDeltas[1].ChemicalId);
        Assert.Equal(64 / 255f, definition.ChemicalDeltas[1].Amount, precision: 6);
        Assert.Equal(GenePayloadCodec.StimulusChemicalToBiochemical(2), definition.ChemicalDeltas[3].ChemicalId);
        Assert.Equal(255 / 255f, definition.ChemicalDeltas[3].Amount, precision: 6);
    }

    [Fact]
    public void StimulusTable_AppliesGenomeAuthoredStimulusBeforeFallback()
    {
        int mappedChemical = GenePayloadCodec.StimulusChemicalToBiochemical(0);
        byte[] stimulusPayload =
        [
            StimulusId.AteFruit,
            255,
            0,
            0,
            0,
            0, 200,
            255, 0,
            255, 0,
            255, 0
        ];
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.Reaction(ChemID.ATP, ChemID.ADP),
            C3DsBiologyParityTests.Gene(
                (int)GeneType.CREATUREGENE,
                (int)CreatureSubtype.G_STIMULUS,
                id: 81,
                payload: stimulusPayload));
        var creature = Creature.Creature.CreateFromGenome(
            genome,
            new Rng(31),
            new CreatureImportOptions(BiochemistryMode: BiochemistryCompatibilityMode.C3DS));
        var trace = new BiochemistryTrace();

        StimulusApplicationTrace stimulusTrace = StimulusTable.Apply(creature, StimulusId.AteFruit, trace);

        Assert.True(creature.Stimuli.HasGenomeAuthoredDefinitions);
        Assert.Equal(200 / 255f, creature.GetChemical(mappedChemical), precision: 6);
        Assert.Equal(0.0f, creature.GetChemical(ChemID.Glycogen));
        Assert.True(stimulusTrace.UsedGenomeAuthoredDefinition);
        Assert.Contains(stimulusTrace.ChemicalDeltas, delta => delta.ChemicalId == mappedChemical);
        Assert.Contains(trace.Deltas, delta => delta.Detail?.Contains("stimulus-gene:81") == true);
    }

    [Fact]
    public void StimulusTable_UsesFallbackWhenGenomeHasNoStimulusGenes()
    {
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ());
        var creature = Creature.Creature.CreateFromGenome(genome, new Rng(32));

        StimulusApplicationTrace stimulusTrace = StimulusTable.Apply(creature, StimulusId.PatOnBack);

        Assert.False(creature.Stimuli.HasGenomeAuthoredDefinitions);
        Assert.False(stimulusTrace.UsedGenomeAuthoredDefinition);
        Assert.True(creature.GetChemical(ChemID.Reward) > 0.0f);
    }

    [Fact]
    public void LateSwitchingStimulusGene_BecomesAvailableAtAgeTransition()
    {
        int mappedChemical = GenePayloadCodec.StimulusChemicalToBiochemical(0);
        var genome = C3DsBiologyParityTests.GenomeFromRaw(
            C3DsBiologyParityTests.Lobe("driv", 4),
            C3DsBiologyParityTests.Lobe("decn", 4),
            C3DsBiologyParityTests.Tract("driv", "decn"),
            C3DsBiologyParityTests.Organ(),
            C3DsBiologyParityTests.GeneWithSwitch(
                (int)GeneType.CREATUREGENE,
                (int)CreatureSubtype.G_STIMULUS,
                id: 82,
                switchOnAge: 2,
                payload: [88, 255, 0, 0, 0, 0, 128, 255, 0, 255, 0, 255, 0]));
        var creature = Creature.Creature.CreateFromGenome(
            genome,
            new Rng(33),
            new CreatureImportOptions(BiochemistryMode: BiochemistryCompatibilityMode.C3DS));

        Assert.False(creature.Stimuli.HasGenomeAuthoredDefinitions);

        creature.ApplyGeneExpressionStage(2);
        StimulusApplicationTrace trace = StimulusTable.Apply(creature, 88);

        Assert.True(creature.Stimuli.HasGenomeAuthoredDefinitions);
        Assert.True(trace.UsedGenomeAuthoredDefinition);
        Assert.Equal(128 / 255f, creature.GetChemical(mappedChemical), precision: 6);
    }
}
