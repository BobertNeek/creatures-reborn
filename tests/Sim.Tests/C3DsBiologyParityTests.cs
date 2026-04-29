using System.Collections.Generic;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Lab;
using CreaturesReborn.Sim.Util;
using Xunit;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Tests;

public sealed class C3DsBiologyParityTests
{
    [Fact]
    public void C3DsImportCompatibilityBaseline_RemainsStable()
    {
        byte[] rawGenome = RawGenome(
            Lobe("driv", 4),
            Lobe("decn", 4),
            Tract("driv", "decn"),
            Gene((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_HALFLIFE, id: 10, payload: new byte[256]),
            Gene((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_NEUROEMITTER, id: 11, payload: new byte[15]),
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_STIMULUS, id: 12, payload: new byte[13]));
        byte[] fileBytes = [(byte)'d', (byte)'n', (byte)'a', (byte)'3', .. rawGenome];

        C3DsGenomeImportResult result = C3DsGenomeImporter.ImportRaw(rawGenome);
        var fileGenome = new G(new Rng(7));
        CreaturesReborn.Sim.Formats.GenomeReader.Load(fileGenome, fileBytes);

        Assert.Equal(rawGenome, result.Genome.AsSpan().ToArray());
        Assert.Equal(rawGenome, fileGenome.AsSpan().ToArray());
        Assert.Equal(BiochemistryCompatibilityMode.C3DS, result.CompatibilityProfile.BiochemistryMode);
        Assert.Contains(result.Report.SupportedGenes, gene => gene.PayloadKind == GenePayloadKind.BiochemistryHalfLife);
        Assert.Contains(result.Report.SupportedGenes, gene => gene.PayloadKind == GenePayloadKind.BiochemistryNeuroEmitter);
        Assert.DoesNotContain(result.Report.ValidationIssues, issue => issue.Severity == GeneValidationSeverity.Error);
    }

    [Fact]
    public void ExistingCreatureLabAndBrainModuleSurfaces_RemainAvailable()
    {
        Assert.Equal(256, StandardChemicalCatalog.All.Count);
        Assert.Equal(BiochemistryCompatibilityMode.C3DS, new CreatureImportOptions().BiochemistryMode);
        Assert.Equal("neutral", LabWorldPreset.Neutral.Name);
        Assert.NotNull(GeneSchemaCatalog.Get(GenePayloadKind.BrainLobe));
    }

    [Fact]
    public void AgeTransition_AppliesLateSwitchingInjectGene()
    {
        G genome = GenomeFromRaw(
            Lobe("driv", 4),
            Lobe("decn", 4),
            Tract("driv", "decn"),
            Organ(),
            GeneWithSwitch(
                (int)GeneType.BIOCHEMISTRYGENE,
                (int)BiochemSubtype.G_INJECT,
                id: 51,
                switchOnAge: 2,
                payload: [(byte)ChemID.Reward, 200]));
        var creature = Creature.Creature.CreateFromGenome(genome, new Rng(12), new CreatureImportOptions(
            Sex: GeneConstants.MALE,
            Age: 0,
            Variant: 0,
            Moniker: "late-inject",
            BiochemistryMode: BiochemistryCompatibilityMode.C3DS));
        var trace = new BiochemistryTrace();

        Assert.Equal(0.0f, creature.GetChemical(ChemID.Reward));

        AgeTransitionResult result = creature.ApplyGeneExpressionStage(2, trace);

        Assert.Equal(2, creature.Genome.Age);
        Assert.Equal(200 / 255f, creature.GetChemical(ChemID.Reward), precision: 6);
        Assert.Contains(result.Trace.ExpressedGenes, gene => gene.Id == 51);
        Assert.Contains(result.Deltas, delta =>
            delta.PayloadKind == GenePayloadKind.BiochemistryInject &&
            delta.ChemicalId == ChemID.Reward &&
            delta.Action == "set-initial-concentration");
        Assert.Contains(trace.Deltas, delta =>
            delta.ChemicalId == ChemID.Reward &&
            delta.Source == ChemicalDeltaSource.CreatureInjection);
    }

    [Fact]
    public void AdvanceAge_AppliesLateSwitchingHalfLifeGene()
    {
        byte[] halfLives = new byte[BiochemConst.NUMCHEM];
        halfLives[ChemID.Reward] = 255;
        G genome = GenomeFromRaw(
            Lobe("driv", 4),
            Lobe("decn", 4),
            Tract("driv", "decn"),
            Organ(),
            GeneWithSwitch(
                (int)GeneType.BIOCHEMISTRYGENE,
                (int)BiochemSubtype.G_HALFLIFE,
                id: 52,
                switchOnAge: 2,
                payload: halfLives));
        var creature = Creature.Creature.CreateFromGenome(genome, new Rng(13), new CreatureImportOptions(
            Sex: GeneConstants.MALE,
            Age: 0,
            Variant: 0,
            Moniker: "late-half-life",
            BiochemistryMode: BiochemistryCompatibilityMode.C3DS));

        Assert.Equal(0.0f, creature.Biochemistry.GetHalfLifeView(ChemID.Reward).DecayRate);

        creature.AdvanceAge(Creature.Creature.TicksPerAgeStep * 2);

        Assert.Equal(2, creature.Genome.Age);
        Assert.True(creature.Biochemistry.GetHalfLifeView(ChemID.Reward).DecayRate > 0.0f);
    }

    [Fact]
    public void AgeTransition_AppliesLateBiochemistryStructureGenes()
    {
        G genome = GenomeFromRaw(
            Lobe("driv", 4),
            Lobe("decn", 4),
            Tract("driv", "decn"),
            Organ(),
            Reaction(ChemID.ATP, ChemID.ADP),
            Receptor(ChemID.Injury, 3, 0),
            GeneWithSwitch(
                (int)GeneType.BIOCHEMISTRYGENE,
                (int)BiochemSubtype.G_REACTION,
                id: 61,
                switchOnAge: 2,
                payload: [1, (byte)ChemID.Glucose, 1, 0, 1, (byte)ChemID.Glycogen, 1, 0, 128]),
            GeneWithSwitch(
                (int)GeneType.BIOCHEMISTRYGENE,
                (int)BiochemSubtype.G_RECEPTOR,
                id: 62,
                switchOnAge: 2,
                payload: [1, (byte)CreatureTissue.Immune, ImmuneLocus.Die, (byte)ChemID.Pain, 1, 0, 255, 0]),
            GeneWithSwitch(
                (int)GeneType.BIOCHEMISTRYGENE,
                (int)BiochemSubtype.G_EMITTER,
                id: 63,
                switchOnAge: 2,
                payload: [1, (byte)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Const, (byte)ChemID.Reward, 0, 1, 128, 0]),
            GeneWithSwitch(
                (int)GeneType.BIOCHEMISTRYGENE,
                (int)BiochemSubtype.G_NEUROEMITTER,
                id: 64,
                switchOnAge: 2,
                payload: [0, 0, 0, 0, 0, 0, 255, (byte)ChemID.Punishment, 64, 0, 0, 0, 0, 0, 0]),
            GeneWithSwitch(
                (int)GeneType.ORGANGENE,
                (int)OrganSubtype.G_ORGAN,
                id: 65,
                switchOnAge: 2,
                payload: [128, 16, 255, 0, 16]));
        var creature = Creature.Creature.CreateFromGenome(genome, new Rng(14), new CreatureImportOptions(
            Sex: GeneConstants.MALE,
            Age: 0,
            Variant: 0,
            Moniker: "late-biochem",
            BiochemistryMode: BiochemistryCompatibilityMode.C3DS));
        int organsBefore = creature.Biochemistry.OrganCount;
        int reactionsBefore = creature.Biochemistry.GetOrgan(0).ReactionCount;
        int receptorsBefore = creature.Biochemistry.GetOrgan(0).ReceptorCount;
        int emittersBefore = creature.Biochemistry.GetOrgan(0).EmitterCount;
        int neuroEmittersBefore = creature.Biochemistry.NeuroEmitterCount;

        AgeTransitionResult result = creature.ApplyGeneExpressionStage(2);

        Assert.True(creature.Biochemistry.OrganCount > organsBefore);
        Assert.True(creature.Biochemistry.GetOrgan(0).ReactionCount > reactionsBefore);
        Assert.True(creature.Biochemistry.GetOrgan(0).ReceptorCount > receptorsBefore);
        Assert.True(creature.Biochemistry.GetOrgan(0).EmitterCount > emittersBefore);
        Assert.True(creature.Biochemistry.NeuroEmitterCount > neuroEmittersBefore);
        Assert.Contains(result.Deltas, delta => delta.PayloadKind == GenePayloadKind.BiochemistryReaction);
        Assert.Contains(result.Deltas, delta => delta.PayloadKind == GenePayloadKind.BiochemistryReceptor);
        Assert.Contains(result.Deltas, delta => delta.PayloadKind == GenePayloadKind.BiochemistryEmitter);
        Assert.Contains(result.Deltas, delta => delta.PayloadKind == GenePayloadKind.BiochemistryNeuroEmitter);
        Assert.Contains(result.Deltas, delta => delta.PayloadKind == GenePayloadKind.Organ);
    }

    [Fact]
    public void AgeTransition_AppliesLateBrainLobeAndTractGenes()
    {
        G genome = GenomeFromRaw(
            Lobe("driv", 4),
            Lobe("decn", 4),
            Tract("driv", "decn"),
            Organ(),
            Reaction(ChemID.ATP, ChemID.ADP),
            Receptor(ChemID.Injury, 3, 0),
            LateLobe("memo", 3, switchOnAge: 2),
            LateTract("driv", "memo", switchOnAge: 2));
        var creature = Creature.Creature.CreateFromGenome(genome, new Rng(15), new CreatureImportOptions(
            Sex: GeneConstants.MALE,
            Age: 0,
            Variant: 0,
            Moniker: "late-brain",
            BiochemistryMode: BiochemistryCompatibilityMode.C3DS));
        int lobesBefore = creature.Brain.LobeCount;
        int tractsBefore = creature.Brain.TractCount;

        AgeTransitionResult result = creature.ApplyGeneExpressionStage(2);

        Assert.True(creature.Brain.LobeCount > lobesBefore);
        Assert.True(creature.Brain.TractCount > tractsBefore);
        Assert.Contains(creature.Brain.CreateSnapshot().Lobes, lobe => lobe.TokenText == "memo");
        Assert.Contains(result.Deltas, delta => delta.PayloadKind == GenePayloadKind.BrainLobe);
        Assert.Contains(result.Deltas, delta => delta.PayloadKind == GenePayloadKind.BrainTract);
    }

    internal static byte[] RawGenome(params byte[][] genes)
    {
        var bytes = new List<byte>();
        foreach (byte[] gene in genes)
            bytes.AddRange(gene);
        bytes.AddRange([(byte)'g', (byte)'e', (byte)'n', (byte)'d']);
        return bytes.ToArray();
    }

    internal static G GenomeFromRaw(params byte[][] genes)
    {
        var genome = new G(new Rng(123));
        genome.AttachBytes(RawGenome(genes), GeneConstants.MALE, age: 0, variant: 0, "test");
        return genome;
    }

    internal static byte[] Gene(int type, int subtype, int id, byte flags = (byte)MutFlags.MUT, params byte[] payload)
        => GeneWithSwitch(type, subtype, id, switchOnAge: 0, flags, payload);

    internal static byte[] GeneWithSwitch(int type, int subtype, int id, byte switchOnAge, byte flags = (byte)MutFlags.MUT, params byte[] payload)
    {
        var bytes = new List<byte>
        {
            (byte)'g', (byte)'e', (byte)'n', (byte)'e',
            (byte)type,
            (byte)subtype,
            (byte)id,
            0,
            switchOnAge,
            flags,
            42,
            0
        };
        bytes.AddRange(payload);
        return bytes.ToArray();
    }

    internal static byte[] Lobe(string token, byte size)
    {
        byte[] payload = new byte[121];
        WriteToken(payload, 0, token);
        payload[10] = size;
        payload[11] = size;
        return Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, token.GetHashCode() & 0xFF, payload: payload);
    }

    internal static byte[] LateLobe(string token, byte size, byte switchOnAge)
    {
        byte[] payload = new byte[121];
        WriteToken(payload, 0, token);
        payload[10] = size;
        payload[11] = size;
        return GeneWithSwitch((int)GeneType.BRAINGENE, (int)BrainSubtype.G_LOBE, token.GetHashCode() & 0xFF, switchOnAge, payload: payload);
    }

    internal static byte[] Tract(string source, string destination)
    {
        byte[] payload = new byte[128];
        WriteToken(payload, 2, source);
        payload[7] = 0;
        payload[9] = 3;
        payload[11] = 1;
        WriteToken(payload, 12, destination);
        payload[17] = 0;
        payload[19] = 3;
        payload[21] = 1;
        return Gene((int)GeneType.BRAINGENE, (int)BrainSubtype.G_TRACT, source[0] ^ destination[0], payload: payload);
    }

    internal static byte[] LateTract(string source, string destination, byte switchOnAge)
    {
        byte[] payload = new byte[128];
        WriteToken(payload, 2, source);
        payload[7] = 0;
        payload[9] = 3;
        payload[11] = 1;
        WriteToken(payload, 12, destination);
        payload[17] = 0;
        payload[19] = 3;
        payload[21] = 1;
        return GeneWithSwitch((int)GeneType.BRAINGENE, (int)BrainSubtype.G_TRACT, source[0] ^ destination[0], switchOnAge, payload: payload);
    }

    internal static byte[] Organ()
        => Gene((int)GeneType.ORGANGENE, (int)OrganSubtype.G_ORGAN, id: 40, payload: [128, 16, 255, 0, 16]);

    internal static byte[] Reaction(int fromChemical, int toChemical)
        => Gene(
            (int)GeneType.BIOCHEMISTRYGENE,
            (int)BiochemSubtype.G_REACTION,
            id: 41,
            payload: [1, (byte)fromChemical, 1, 0, 1, (byte)toChemical, 1, 0, 128]);

    internal static byte[] Receptor(int chemical, int tissue, int locus)
        => Gene(
            (int)GeneType.BIOCHEMISTRYGENE,
            (int)BiochemSubtype.G_RECEPTOR,
            id: 42,
            payload: [1, (byte)tissue, (byte)locus, (byte)chemical, 1, 0, 255, 0]);

    private static void WriteToken(byte[] payload, int offset, string token)
    {
        for (int i = 0; i < 4; i++)
            payload[offset + i] = i < token.Length ? (byte)token[i] : (byte)' ';
    }
}
