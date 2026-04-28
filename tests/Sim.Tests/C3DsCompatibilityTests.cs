using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using Xunit;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Tests;

public sealed class C3DsCompatibilityTests
{
    [Fact]
    public void GeneSchemaCatalog_DefinesEveryStandardC3DsGenePayload()
    {
        AssertSchema(GenePayloadKind.BrainLobe, 121);
        AssertSchema(GenePayloadKind.BrainOrgan, 5);
        AssertSchema(GenePayloadKind.BrainTract, 128);
        AssertSchema(GenePayloadKind.BiochemistryReceptor, 8);
        AssertSchema(GenePayloadKind.BiochemistryEmitter, 8);
        AssertSchema(GenePayloadKind.BiochemistryReaction, 9);
        AssertSchema(GenePayloadKind.BiochemistryHalfLife, 256);
        AssertSchema(GenePayloadKind.BiochemistryInject, 2);
        AssertSchema(GenePayloadKind.BiochemistryNeuroEmitter, 15);
        AssertSchema(GenePayloadKind.CreatureStimulus, 13);
        AssertSchema(GenePayloadKind.CreatureGenus, 65);
        AssertSchema(GenePayloadKind.CreatureAppearance, 3);
        AssertSchema(GenePayloadKind.CreaturePose, 17);
        AssertSchema(GenePayloadKind.CreatureGait, 9);
        AssertSchema(GenePayloadKind.CreatureInstinct, 9);
        AssertSchema(GenePayloadKind.CreaturePigment, 2);
        AssertSchema(GenePayloadKind.CreaturePigmentBleed, 2);
        AssertSchema(GenePayloadKind.CreatureExpression, 11);
        AssertSchema(GenePayloadKind.Organ, 5);
    }

    [Fact]
    public void GenePayloadCodec_DecodesC3DsHalfLifeAndNeuroEmitterPayloads()
    {
        byte[] halfLives = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        EditableGenePayload halfLife = GenePayloadCodec.Decode(Record(
            (int)GeneType.BIOCHEMISTRYGENE,
            (int)BiochemSubtype.G_HALFLIFE,
            halfLives));

        Assert.False(halfLife.IsRawFallback);
        Assert.Equal(256, halfLife.Fields.Count);
        Assert.Equal(35, halfLife.GetInt("chemical_035"));
        Assert.Equal(255, halfLife.GetInt("chemical_255"));

        byte[] neuroEmitter =
        [
            1, 10,
            2, 20,
            3, 30,
            8,
            35, 64,
            36, 32,
            204, 255,
            205, 128
        ];
        EditableGenePayload decodedNeuroEmitter = GenePayloadCodec.Decode(Record(
            (int)GeneType.BIOCHEMISTRYGENE,
            (int)BiochemSubtype.G_NEUROEMITTER,
            neuroEmitter));

        Assert.False(decodedNeuroEmitter.IsRawFallback);
        Assert.Equal(1, decodedNeuroEmitter.GetInt("lobe0"));
        Assert.Equal(10, decodedNeuroEmitter.GetInt("neuron0"));
        Assert.Equal(8, decodedNeuroEmitter.GetInt("rate"));
        Assert.Equal(204, decodedNeuroEmitter.GetInt("chemical2"));
        Assert.Equal(128, decodedNeuroEmitter.GetInt("amount3"));
    }

    [Fact]
    public void GenePayloadCodec_UsesC3DsStimulusChemicalConversion()
    {
        byte[] stimulus = [4, 9, 1, 100, 3, 0, 20, 107, 30, 108, 40, 255, 50];
        EditableGenePayload decoded = GenePayloadCodec.Decode(Record(
            (int)GeneType.CREATUREGENE,
            (int)CreatureSubtype.G_STIMULUS,
            stimulus));

        Assert.False(decoded.IsRawFallback);
        Assert.Equal(4, decoded.GetInt("stimulus"));
        Assert.Equal(148, decoded.GetInt("chemical0_biochemical"));
        Assert.Equal(0, decoded.GetInt("chemical3_biochemical"));
        Assert.Equal(1, decoded.GetInt("input"));
        Assert.Equal(3, decoded.GetInt("features"));
    }

    [Fact]
    public void C3DsGenomeImporter_PreservesRawGenesAndReportsCompatibility()
    {
        byte[] rawGenome = RawGenome(
            Gene((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_HALFLIFE, id: 1, payload: new byte[256]),
            Gene((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_NEUROEMITTER, id: 2, payload: new byte[15]),
            Gene((int)GeneType.CREATUREGENE, (int)CreatureSubtype.G_STIMULUS, id: 3, payload: new byte[13]));

        C3DsGenomeImportResult result = C3DsGenomeImporter.ImportRaw(rawGenome, new C3DsGenomeImportOptions(
            Sex: GeneConstants.MALE,
            Age: 0,
            Variant: 0,
            Moniker: "compat"));

        Assert.Equal(BiochemistryCompatibilityMode.C3DS, result.CompatibilityProfile.BiochemistryMode);
        Assert.Equal(3, result.Records.Count);
        Assert.Equal(rawGenome, result.Genome.AsSpan().ToArray());
        Assert.False(result.Report.HasErrors);
        Assert.Contains(result.Report.SupportedGenes, gene => gene.PayloadKind == GenePayloadKind.BiochemistryHalfLife);
    }

    [Fact]
    public void C3DsGenomeImporter_LoadsDna3GenFileAndStripsFileHeader()
    {
        byte[] rawGenome = RawGenome(Gene((int)GeneType.BIOCHEMISTRYGENE, (int)BiochemSubtype.G_INJECT, id: 1, payload: [35, 128]));
        byte[] fileBytes = [(byte)'d', (byte)'n', (byte)'a', (byte)'3', .. rawGenome];
        string path = Path.Combine(Path.GetTempPath(), $"creatures-reborn-{Path.GetRandomFileName()}.gen");
        File.WriteAllBytes(path, fileBytes);
        try
        {
            C3DsGenomeImportResult result = C3DsGenomeImporter.ImportFile(path);

            Assert.Equal(rawGenome, result.Genome.AsSpan().ToArray());
            Assert.Single(result.Records);
            Assert.Equal(BiochemistryCompatibilityMode.C3DS, result.CompatibilityProfile.BiochemistryMode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BiochemistryCompatibilityMode_DisablesModernHooksInC3DsMode()
    {
        var modern = new Biochemistry.Biochemistry(BiochemistryCompatibilityMode.ModernExtensions);
        modern.SetChemical(ChemID.Glycogen, 0.0f);
        modern.SetChemical(ChemID.HungerForCarb, 0.0f);
        modern.Update();

        var c3ds = new Biochemistry.Biochemistry(BiochemistryCompatibilityMode.C3DS);
        c3ds.SetChemical(ChemID.Glycogen, 0.0f);
        c3ds.SetChemical(ChemID.HungerForCarb, 0.0f);
        c3ds.Update();

        Assert.True(modern.GetChemical(ChemID.HungerForCarb) > 0.0f);
        Assert.Equal(0.0f, c3ds.GetChemical(ChemID.HungerForCarb));
    }

    [Fact]
    public void StandardChemicalCatalog_DocumentsAllC3DsChemicalSlots()
    {
        Assert.Equal(BiochemConst.NUMCHEM, StandardChemicalCatalog.All.Count);
        Assert.All(Enumerable.Range(0, BiochemConst.NUMCHEM), id =>
        {
            StandardChemicalDefinition definition = StandardChemicalCatalog.Get(id);
            Assert.Equal(id, definition.Id);
            Assert.False(string.IsNullOrWhiteSpace(definition.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(definition.Source));
        });

        Assert.Equal("ATP", StandardChemicalCatalog.Get(35).DisplayName);
        Assert.Equal("Pain", StandardChemicalCatalog.Get(148).DisplayName);
        Assert.Equal("Reward", StandardChemicalCatalog.Get(204).DisplayName);
        Assert.Equal("REM", StandardChemicalCatalog.Get(213).DisplayName);
    }

    private static void AssertSchema(GenePayloadKind kind, int exactLength)
    {
        GenePayloadSchema schema = GeneSchemaCatalog.Get(kind);
        Assert.Equal(exactLength, schema.ExactLength);
        Assert.NotEmpty(schema.Fields);
    }

    private static GeneRecord Record(int type, int subtype, byte[] payload)
    {
        G genome = GenomeFromRaw(Gene(type, subtype, id: 1, payload: payload));
        return GeneDecoder.Decode(genome)[0];
    }

    private static G GenomeFromRaw(params byte[][] genes)
    {
        var genome = new G(new Rng(123));
        genome.AttachBytes(RawGenome(genes), GeneConstants.MALE, age: 0, variant: 0, "test");
        return genome;
    }

    private static byte[] RawGenome(params byte[][] genes)
    {
        var bytes = new List<byte>();
        foreach (byte[] gene in genes)
            bytes.AddRange(gene);
        bytes.AddRange([(byte)'g', (byte)'e', (byte)'n', (byte)'d']);
        return bytes.ToArray();
    }

    private static byte[] Gene(int type, int subtype, int id, params byte[] payload)
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
}
