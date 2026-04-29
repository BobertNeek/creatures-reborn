using System;
using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Brain;

public enum NeuralArchitectureProfile
{
    ClassicC3Ds = 0,
    RebornExpanded = 1
}

public sealed record BrainLobeBlueprint(
    string Token,
    byte Width,
    byte Height,
    byte TissueId,
    string Description);

public sealed record BrainTractBlueprint(
    string SourceLobe,
    string DestinationLobe,
    byte DendritesPerNeuron,
    string Description);

public sealed record BrainBlueprint(
    string Name,
    NeuralArchitectureProfile Profile,
    IReadOnlyList<BrainLobeBlueprint> Lobes,
    IReadOnlyList<BrainTractBlueprint> Tracts);

public sealed record CreatureRebornBrainProfile(
    NeuralArchitectureProfile Profile,
    BrainBlueprint Blueprint,
    string CompatibilityNote);

public static class BrainBlueprintBuilder
{
    public static CreatureRebornBrainProfile ClassicC3DsBrainProfile()
    {
        BrainBlueprint blueprint = new(
            "Classic C3/DS minimum",
            NeuralArchitectureProfile.ClassicC3Ds,
            [
                new("driv", 8, 4, 0, "Drive inputs from biochemistry."),
                new("decn", 8, 4, 1, "Decision/action output."),
                new("verb", 8, 4, 2, "Verb/action concept lobe."),
                new("noun", 8, 4, 3, "Noun/object concept lobe."),
                new("attn", 8, 4, 4, "Attention target lobe.")
            ],
            [
                new("driv", "decn", 2, "Drive pressure to decision output."),
                new("verb", "decn", 1, "Verb context to decision output."),
                new("noun", "decn", 1, "Object context to decision output."),
                new("attn", "decn", 1, "Attention context to decision output.")
            ]);

        return new CreatureRebornBrainProfile(
            NeuralArchitectureProfile.ClassicC3Ds,
            blueprint,
            "Matches the classic lobe/tract substrate and does not replace imported C3/DS genomes.");
    }

    public static CreatureRebornBrainProfile RebornExpandedBrainProfile()
    {
        BrainBlueprint blueprint = new(
            "Creatures Reborn expanded",
            NeuralArchitectureProfile.RebornExpanded,
            [
                new("driv", 16, 8, 0, "Higher-resolution drive inputs from biochemistry."),
                new("decn", 16, 8, 1, "Decision/action output with more arbitration capacity."),
                new("verb", 12, 8, 2, "Richer action concept lobe."),
                new("noun", 12, 8, 3, "Richer object concept lobe."),
                new("attn", 12, 8, 4, "Attention target and salience lobe."),
                new("smel", 16, 8, 5, "Smell/object perception."),
                new("plac", 16, 8, 6, "Place and route context memory."),
                new("socl", 16, 8, 7, "Social identity memory."),
                new("dang", 12, 8, 8, "Danger and pain context memory."),
                new("curi", 12, 8, 9, "Curiosity/play arbitration."),
                new("goal", 12, 8, 10, "Goal persistence."),
                new("inhi", 8, 8, 11, "Action inhibition."),
                new("rest", 8, 8, 12, "Sleep and rest regulation.")
            ],
            [
                new("driv", "decn", 3, "Drive pressure to decision output."),
                new("smel", "attn", 2, "Perception drives attention."),
                new("noun", "attn", 2, "Object concepts inform attention."),
                new("attn", "decn", 2, "Attention context to decision output."),
                new("plac", "decn", 2, "Place context supports decisions."),
                new("socl", "decn", 2, "Social memory supports decisions."),
                new("dang", "inhi", 2, "Danger context can inhibit action."),
                new("inhi", "decn", 1, "Inhibition gates decision output."),
                new("curi", "goal", 2, "Curiosity feeds goal persistence."),
                new("goal", "decn", 2, "Goals bias decision output."),
                new("rest", "decn", 1, "Rest state biases action arbitration.")
            ]);

        return new CreatureRebornBrainProfile(
            NeuralArchitectureProfile.RebornExpanded,
            blueprint,
            "Opt-in expanded C3/DS-style brain; all lobes and tracts remain inspectable.");
    }

    public static G CreateGenome(
        BrainBlueprint blueprint,
        IRng rng,
        string moniker = "blueprint")
    {
        var bytes = new List<byte>();
        int id = 1;
        foreach (BrainLobeBlueprint lobe in blueprint.Lobes)
            bytes.AddRange(LobeGene(lobe, id++));
        foreach (BrainTractBlueprint tract in blueprint.Tracts)
            bytes.AddRange(TractGene(tract, id++));

        bytes.AddRange(OrganGene(id++));
        bytes.AddRange(ReactionGene(id++, ChemID.ATP, ChemID.ADP));
        bytes.AddRange(ReceptorGene(id++, ChemID.Injury, tissue: 3, locus: 0));
        bytes.AddRange([(byte)'g', (byte)'e', (byte)'n', (byte)'d']);

        var genome = new G(rng);
        genome.AttachBytes(bytes.ToArray(), GeneConstants.MALE, age: 0, variant: 0, moniker);
        return genome;
    }

    private static byte[] LobeGene(BrainLobeBlueprint lobe, int id)
    {
        byte[] payload = new byte[121];
        WriteToken(payload, 0, lobe.Token);
        payload[10] = lobe.Width;
        payload[11] = lobe.Height;
        payload[16] = lobe.TissueId;
        return Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, id, payload);
    }

    private static byte[] TractGene(BrainTractBlueprint tract, int id)
    {
        byte[] payload = new byte[128];
        WriteToken(payload, 2, tract.SourceLobe);
        payload[8] = 0;
        payload[9] = byte.MaxValue;
        payload[11] = tract.DendritesPerNeuron;
        WriteToken(payload, 12, tract.DestinationLobe);
        payload[18] = 0;
        payload[19] = byte.MaxValue;
        payload[21] = tract.DendritesPerNeuron;
        return Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_TRACT, id, payload);
    }

    private static byte[] OrganGene(int id)
        => Gene((int)GeneType.ORGANGENE, (int)OrganSubtype.G_ORGAN, id, [128, 16, 255, 0, 16]);

    private static byte[] ReactionGene(int id, int fromChemical, int toChemical)
        => Gene(
            (int)GeneType.BIOCHEMISTRYGENE,
            (int)BiochemSubtype.G_REACTION,
            id,
            [1, (byte)fromChemical, 1, 0, 1, (byte)toChemical, 1, 0, 128]);

    private static byte[] ReceptorGene(int id, int chemical, int tissue, int locus)
        => Gene(
            (int)GeneType.BIOCHEMISTRYGENE,
            (int)BiochemSubtype.G_RECEPTOR,
            id,
            [1, (byte)tissue, (byte)locus, (byte)chemical, 1, 0, 255, 0]);

    private static byte[] Gene(int type, int subtype, int id, byte[] payload)
    {
        var bytes = new List<byte>
        {
            (byte)'g', (byte)'e', (byte)'n', (byte)'e',
            (byte)type,
            (byte)subtype,
            (byte)id,
            0,
            0,
            (byte)MutFlags.MUT,
            42,
            0
        };
        bytes.AddRange(payload);
        return bytes.ToArray();
    }

    private static void WriteToken(byte[] payload, int offset, string token)
    {
        string padded = token.PadRight(4).Substring(0, 4);
        for (int i = 0; i < 4; i++)
            payload[offset + i] = (byte)padded[i];
    }
}

public static class BrainBlueprintValidator
{
    public static BrainInterfaceReport Validate(BrainBlueprint blueprint)
    {
        G genome = BrainBlueprintBuilder.CreateGenome(blueprint, new Rng(0), "blueprint-validation");
        var requiredRoutes = blueprint.Tracts
            .Select(tract => (tract.SourceLobe, tract.DestinationLobe))
            .ToArray();
        var spec = new MinimumBrainInterfaceSpec(
            RequiredLobes: blueprint.Lobes.Select(lobe => lobe.Token).ToArray(),
            RequiredRoutes: requiredRoutes,
            MinimumHealthyLobeNeurons: 4,
            MaximumLobeNeurons: 4096);
        return BrainInterfaceValidator.Validate(GeneDecoder.Decode(genome), spec);
    }
}
