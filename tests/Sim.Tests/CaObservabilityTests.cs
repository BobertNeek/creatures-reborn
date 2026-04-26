using CreaturesReborn.Sim.Agent;
using CreaturesReborn.Sim.World;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public sealed class CaObservabilityTests
{
    [Fact]
    public void ChannelCatalog_MapsExactlyTheCurrentTwentyChannels()
    {
        Assert.Equal(CaIndex.Count, CaChannelCatalog.All.Count);

        CaChannelDefinition temperature = CaChannelCatalog.Get(CaIndex.Temperature);
        Assert.Equal("temperature", temperature.Token);
        Assert.Equal(CaChannelCategory.Physical, temperature.Category);
        Assert.Equal(CaIndex.HeatSource, temperature.SourceChannelIndex);

        CaChannelDefinition heatSource = CaChannelCatalog.Get(CaIndex.HeatSource);
        Assert.True(heatSource.IsSource);
        Assert.Equal(CaIndex.Temperature, heatSource.OutputChannelIndex);

        CaChannelDefinition scent = CaChannelCatalog.Get(CaIndex.Scent7);
        Assert.Equal("scent_7", scent.Token);
        Assert.Equal(CaChannelCategory.Scent, scent.Category);
    }

    [Fact]
    public void RoomSnapshot_CopiesAllChannelValues()
    {
        var room = new Room { Id = 7, MetaRoomId = 3 };
        room.CA[CaIndex.Light] = 0.75f;
        room.CA[CaIndex.NutrientWater] = 0.25f;

        RoomCaSnapshot snapshot = room.CreateCaSnapshot();

        Assert.Equal(7, snapshot.RoomId);
        Assert.Equal(3, snapshot.MetaRoomId);
        Assert.Equal(CaIndex.Count, snapshot.Channels.Count);
        Assert.Equal(0.75f, snapshot.GetValue(CaIndex.Light));

        room.CA[CaIndex.Light] = 0.1f;
        Assert.Equal(0.75f, snapshot.GetValue(CaIndex.Light));
    }

    [Fact]
    public void MetaRoomAndMapSnapshots_CollectRoomCaValues()
    {
        var map = new GameMap();
        MetaRoom metaRoom = map.AddMetaRoom(0, 0, 30, 20, "test");
        Room left = map.AddRoom(metaRoom.Id, 0, 10, 10, 10, 0, 0);
        Room right = map.AddRoom(metaRoom.Id, 12, 22, 10, 10, 0, 0);
        left.CA[CaIndex.Temperature] = 0.2f;
        right.CA[CaIndex.Temperature] = 0.8f;

        MetaRoomCaSnapshot metaSnapshot = metaRoom.CreateCaSnapshot();
        CaSnapshot mapSnapshot = map.CreateCaSnapshot();

        Assert.Equal(metaRoom.Id, metaSnapshot.MetaRoomId);
        Assert.Equal(2, metaSnapshot.Rooms.Count);
        Assert.Equal(0.8f, metaSnapshot.Rooms[1].GetValue(CaIndex.Temperature));
        Assert.Single(mapSnapshot.MetaRooms);
        Assert.Equal(2, mapSnapshot.Rooms.Count);
    }

    [Fact]
    public void AgentCaProducer_MapsExistingEmitFieldsWithoutChangingEmissionBehavior()
    {
        var manager = new AgentManager();
        var room = new Room { Id = 5, MetaRoomId = 2 };
        var agent = new Agent.Agent(2, 6, 9)
        {
            CurrentRoom = room,
            EmitCaIndex = CaIndex.Scent2,
            EmitCaAmount = 0.35f,
        };
        manager.Add(agent);

        CaProducer? producer = agent.CreateCaProducer();

        Assert.NotNull(producer);
        Assert.Equal(agent.UniqueId, producer.AgentId);
        Assert.Equal(agent.Classifier, producer.Classifier);
        Assert.Equal(room.Id, producer.RoomId);
        CaEmission emission = Assert.Single(producer.Emissions);
        Assert.Equal(CaIndex.Scent2, emission.Channel.Index);
        Assert.Equal(0.35f, emission.Amount);
        Assert.Equal(CaEmissionKind.AgentEmit, emission.Kind);
    }

    [Fact]
    public void CaQuery_FindsHighestAndLowestRoomsForAChannel()
    {
        var map = new GameMap();
        MetaRoom metaRoom = map.AddMetaRoom(0, 0, 40, 20, "test");
        Room low = map.AddRoom(metaRoom.Id, 0, 10, 10, 10, 0, 0);
        Room middle = map.AddRoom(metaRoom.Id, 12, 22, 10, 10, 0, 0);
        Room high = map.AddRoom(metaRoom.Id, 24, 34, 10, 10, 0, 0);
        low.CA[CaIndex.Radiation] = 0.1f;
        middle.CA[CaIndex.Radiation] = 0.45f;
        high.CA[CaIndex.Radiation] = 0.9f;

        RoomCaReading highest = CaQuery.HighestRoom(map.AllRooms, CaIndex.Radiation);
        RoomCaReading lowest = CaQuery.LowestRoom(map.AllRooms, CaIndex.Radiation);
        var consumer = new CaConsumer("danger-sense", CaIndex.Radiation);

        Assert.Same(high, highest.Room);
        Assert.Equal(0.9f, highest.Value);
        Assert.Same(low, lowest.Room);
        Assert.Equal(0.1f, lowest.Value);
        Assert.Equal(0.45f, consumer.Read(middle));
    }
}
