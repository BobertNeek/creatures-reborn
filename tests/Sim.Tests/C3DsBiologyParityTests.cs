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
    {
        var bytes = new List<byte>
        {
            (byte)'g', (byte)'e', (byte)'n', (byte)'e',
            (byte)type,
            (byte)subtype,
            (byte)id,
            0,
            0,
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

