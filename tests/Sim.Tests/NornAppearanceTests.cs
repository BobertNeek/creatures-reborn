using System;
using System.IO;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using G = CreaturesReborn.Sim.Genome.Genome;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class NornAppearanceTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    private static G LoadStarter(int sex = GeneConstants.MALE, int seed = 42)
        => CreaturesReborn.Sim.Formats.GenomeReader.LoadNew(
            new Rng(seed),
            Path.GetFullPath(StarterGenomePath),
            sex: sex);

    [Fact]
    public void AppearanceFromGenome_IsDeterministicForSameGenomeAndSex()
    {
        CreatureAppearance first = CreatureAppearance.FromGenome(LoadStarter(seed: 1));
        CreatureAppearance second = CreatureAppearance.FromGenome(LoadStarter(seed: 1));

        Assert.Equal(first, second);
    }

    [Fact]
    public void AppearanceFromGenome_ExpressesSexualDimorphism()
    {
        CreatureAppearance male = CreatureAppearance.FromGenome(LoadStarter(GeneConstants.MALE));
        CreatureAppearance female = CreatureAppearance.FromGenome(LoadStarter(GeneConstants.FEMALE));

        Assert.Equal(CreatureSex.Male, male.Sex);
        Assert.Equal(CreatureSex.Female, female.Sex);
        Assert.True(male.BodyWidthScale > female.BodyWidthScale);
        Assert.True(female.HeadScale > male.HeadScale);
        Assert.NotEqual(male.Signature, female.Signature);
    }

    [Fact]
    public void AppearanceFromGenome_ChangesWhenGenomeBytesMutate()
    {
        G starter = LoadStarter();
        byte[] mutatedBytes = starter.AsSpan().ToArray();
        int mutationIndex = mutatedBytes.Length / 2;
        mutatedBytes[mutationIndex] ^= 0x5A;

        var mutated = new G(new Rng(99));
        mutated.AttachBytes(mutatedBytes, GeneConstants.MALE, age: 0, variant: 0, moniker: "mutant");

        CreatureAppearance normal = CreatureAppearance.FromGenome(starter);
        CreatureAppearance mutant = CreatureAppearance.FromGenome(mutated);

        Assert.NotEqual(normal.Signature, mutant.Signature);
        Assert.True(
            normal.FurColor != mutant.FurColor ||
            normal.MarkingStrength != mutant.MarkingStrength ||
            normal.TailScale != mutant.TailScale);
    }

    [Fact]
    public void CreatureLoadFromFile_UsesRequestedSexForGenomeExpression()
    {
        Creature.Creature male = Creature.Creature.LoadFromFile(
            Path.GetFullPath(StarterGenomePath),
            new Rng(3),
            sex: GeneConstants.MALE);

        Creature.Creature female = Creature.Creature.LoadFromFile(
            Path.GetFullPath(StarterGenomePath),
            new Rng(3),
            sex: GeneConstants.FEMALE);

        Assert.Equal(GeneConstants.MALE, male.Genome.Sex);
        Assert.Equal(GeneConstants.FEMALE, female.Genome.Sex);
    }
}
