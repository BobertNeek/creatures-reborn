using System;
using System.IO;
using CreaturesReborn.Sim.Biochemistry;
using CreaturesReborn.Sim.Creature;
using CreaturesReborn.Sim.Util;
using CreaturesReborn.Sim.World;
using C = CreaturesReborn.Sim.Creature.Creature;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class CreatureEnvironmentTests
{
    private static readonly string StarterGenomePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "genomes", "starter.gen");

    private static C LoadStarter(int seed = 80)
        => C.LoadFromFile(Path.GetFullPath(StarterGenomePath), new Rng(seed));

    [Fact]
    public void HotRoomEnvironment_RaisesHotnessPunishmentAndComfortNeed()
    {
        var creature = LoadStarter();
        creature.SetChemical(ChemID.Punishment, 0.0f);
        var room = MakeRoom(temperature: 0.95f, light: 0.7f, radiation: 0.0f);
        var trace = new BiochemistryTrace();

        CreatureEnvironmentResponse response = creature.ApplyEnvironment(room, trace);

        Assert.True(response.Hotness > 0.0f);
        Assert.Equal(0.0f, response.Coldness);
        Assert.True(response.ComfortNeed > 0.0f);
        Assert.True(creature.GetChemical(ChemID.Hotness) > 0.0f);
        Assert.True(creature.GetChemical(ChemID.Punishment) > 0.0f);
        Assert.Equal(
            response.Hotness,
            creature.Biochemistry.GetCreatureLocus((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Hotness).Value,
            precision: 6);
        Assert.Contains(trace.Deltas, d =>
            d.ChemicalId == ChemID.Hotness &&
            d.Source == ChemicalDeltaSource.Environment &&
            d.Amount > 0.0f);
    }

    [Fact]
    public void ColdRoomEnvironment_RaisesColdnessAndComfortNeed()
    {
        var creature = LoadStarter(seed: 81);
        var room = MakeRoom(temperature: 0.05f, light: 0.5f, radiation: 0.0f);
        var trace = new BiochemistryTrace();

        CreatureEnvironmentResponse response = creature.ApplyEnvironment(room, trace);

        Assert.True(response.Coldness > 0.0f);
        Assert.Equal(0.0f, response.Hotness);
        Assert.True(response.ComfortNeed > 0.0f);
        Assert.True(creature.GetChemical(ChemID.Coldness) > 0.0f);
        Assert.Equal(
            response.Coldness,
            creature.Biochemistry.GetCreatureLocus((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Coldness).Value,
            precision: 6);
        Assert.Contains(trace.Deltas, d =>
            d.ChemicalId == ChemID.Coldness &&
            d.Source == ChemicalDeltaSource.Environment &&
            d.Amount > 0.0f);
    }

    [Fact]
    public void NeutralRoomEnvironment_ReducesTemperatureDiscomfortWithoutPunishment()
    {
        var creature = LoadStarter(seed: 82);
        creature.SetChemical(ChemID.Hotness, 0.6f);
        creature.SetChemical(ChemID.Coldness, 0.4f);
        creature.SetChemical(ChemID.Punishment, 0.0f);
        var room = MakeRoom(temperature: 0.5f, light: 0.42f, radiation: 0.0f);

        CreatureEnvironmentResponse response = creature.ApplyEnvironment(room);

        Assert.Equal(0.0f, response.Hotness);
        Assert.Equal(0.0f, response.Coldness);
        Assert.Equal(0.0f, response.ComfortNeed);
        Assert.True(creature.GetChemical(ChemID.Hotness) < 0.6f);
        Assert.True(creature.GetChemical(ChemID.Coldness) < 0.4f);
        Assert.Equal(0.0f, creature.GetChemical(ChemID.Punishment));
        Assert.Equal(
            0.42f,
            creature.Biochemistry.GetCreatureLocus((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.LightLevel).Value,
            precision: 6);
    }

    [Fact]
    public void RadiationEnvironment_MapsRoomFieldToStressAndLocus()
    {
        var creature = LoadStarter(seed: 83);
        creature.SetChemical(ChemID.Punishment, 0.0f);
        creature.SetChemical(ChemID.Fear, 0.0f);
        var room = MakeRoom(temperature: 0.5f, light: 0.35f, radiation: 0.8f);
        var trace = new BiochemistryTrace();

        CreatureEnvironmentResponse response = creature.ApplyEnvironment(room, trace);

        Assert.True(response.Stress > 0.0f);
        Assert.True(creature.GetChemical(ChemID.Punishment) > 0.0f);
        Assert.True(creature.GetChemical(ChemID.Fear) > 0.0f);
        Assert.Equal(
            0.8f,
            creature.Biochemistry.GetCreatureLocus((int)CreatureTissue.Sensorimotor, SensorimotorEmitterLocus.Radiation).Value,
            precision: 6);
        Assert.Contains(trace.Deltas, d =>
            d.ChemicalId == ChemID.Punishment &&
            d.Source == ChemicalDeltaSource.Environment &&
            d.Amount > 0.0f);
    }

    private static Room MakeRoom(float temperature, float light, float radiation)
    {
        var room = new Room { Id = 1, MetaRoomId = 1 };
        room.CA[CaIndex.Temperature] = temperature;
        room.CA[CaIndex.Light] = light;
        room.CA[CaIndex.Radiation] = radiation;
        return room;
    }
}
