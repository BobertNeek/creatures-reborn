using System;
using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;

namespace CreaturesReborn.Sim.Creature;

public sealed record StimulusChemicalDelta(int ChemicalId, float Amount);

public sealed record StimulusGeneDefinition(
    GeneIdentity Gene,
    int StimulusId,
    float Significance,
    int Input,
    float Intensity,
    int Features,
    IReadOnlyList<StimulusChemicalDelta> ChemicalDeltas);

public sealed record StimulusApplicationTrace(
    int StimulusId,
    bool UsedGenomeAuthoredDefinition,
    GeneIdentity? SourceGene,
    float Significance,
    IReadOnlyList<StimulusChemicalDelta> ChemicalDeltas);

public sealed class GenomeStimulusTable
{
    private readonly Dictionary<int, StimulusGeneDefinition> _definitionsByStimulus;

    public GenomeStimulusTable(IReadOnlyList<StimulusGeneDefinition> definitions)
    {
        Definitions = definitions;
        _definitionsByStimulus = definitions
            .GroupBy(definition => definition.StimulusId)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    public static GenomeStimulusTable Empty { get; } = new([]);

    public IReadOnlyList<StimulusGeneDefinition> Definitions { get; }

    public bool HasGenomeAuthoredDefinitions => Definitions.Count > 0;

    public bool TryGet(int stimulusId, out StimulusGeneDefinition definition)
        => _definitionsByStimulus.TryGetValue(stimulusId, out definition!);

    public static GenomeStimulusTable FromGenome(Genome.Genome genome)
    {
        var definitions = new List<StimulusGeneDefinition>();
        foreach (GeneRecord record in GeneDecoder.Decode(genome))
        {
            if (record.Payload.Kind != GenePayloadKind.CreatureStimulus || record.Payload.Bytes.Length < 13)
                continue;
            if (record.Header.IsSilent)
                continue;
            if (record.Header.MaleLinked && genome.Sex != GeneConstants.MALE)
                continue;
            if (record.Header.FemaleLinked && genome.Sex != GeneConstants.FEMALE)
                continue;
            if (record.Header.Variant != 0 && record.Header.Variant != genome.Variant)
                continue;
            if (record.Header.SwitchOnAge > genome.Age)
                continue;

            ReadOnlySpan<byte> payload = record.Payload.Bytes;
            var deltas = new List<StimulusChemicalDelta>(4);
            for (int i = 0; i < 4; i++)
            {
                int offset = 5 + i * 2;
                int chemical = GenePayloadCodec.StimulusChemicalToBiochemical(payload[offset]);
                float amount = payload[offset + 1] / 255f;
                deltas.Add(new StimulusChemicalDelta(chemical, amount));
            }

            definitions.Add(new StimulusGeneDefinition(
                record.Identity,
                payload[0],
                payload[1] / 255f,
                payload[2],
                payload[3] / 255f,
                payload[4],
                deltas));
        }

        return new GenomeStimulusTable(definitions);
    }
}
