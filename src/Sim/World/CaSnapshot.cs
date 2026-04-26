using System.Collections.Generic;

namespace CreaturesReborn.Sim.World;

public sealed record CaChannelValue(CaChannelDefinition Channel, float Value)
{
    public int ChannelIndex => Channel.Index;
}

public sealed record RoomCaSnapshot(
    int RoomId,
    int MetaRoomId,
    IReadOnlyList<CaChannelValue> Channels)
{
    public float GetValue(int channelIndex) => Channels[CaChannelCatalog.Get(channelIndex).Index].Value;
}

public sealed record MetaRoomCaSnapshot(
    int MetaRoomId,
    string Name,
    IReadOnlyList<RoomCaSnapshot> Rooms);

public sealed record CaSnapshot(
    IReadOnlyList<MetaRoomCaSnapshot> MetaRooms,
    IReadOnlyList<RoomCaSnapshot> Rooms);

public static class CaSnapshotExtensions
{
    public static RoomCaSnapshot CreateCaSnapshot(this Room room)
    {
        var channels = new CaChannelValue[CaIndex.Count];
        for (int i = 0; i < CaIndex.Count; i++)
            channels[i] = new CaChannelValue(CaChannelCatalog.Get(i), room.CA[i]);

        return new RoomCaSnapshot(room.Id, room.MetaRoomId, channels);
    }

    public static MetaRoomCaSnapshot CreateCaSnapshot(this MetaRoom metaRoom)
    {
        var rooms = new RoomCaSnapshot[metaRoom.Rooms.Count];
        for (int i = 0; i < metaRoom.Rooms.Count; i++)
            rooms[i] = metaRoom.Rooms[i].CreateCaSnapshot();

        return new MetaRoomCaSnapshot(metaRoom.Id, metaRoom.Name, rooms);
    }

    public static CaSnapshot CreateCaSnapshot(this GameMap map)
    {
        var metaRooms = new MetaRoomCaSnapshot[map.MetaRooms.Count];
        for (int i = 0; i < map.MetaRooms.Count; i++)
            metaRooms[i] = map.MetaRooms[i].CreateCaSnapshot();

        var rooms = new RoomCaSnapshot[map.AllRooms.Count];
        for (int i = 0; i < map.AllRooms.Count; i++)
            rooms[i] = map.AllRooms[i].CreateCaSnapshot();

        return new CaSnapshot(metaRooms, rooms);
    }
}
