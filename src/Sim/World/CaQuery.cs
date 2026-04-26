using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.World;

public sealed record RoomCaReading(Room Room, CaChannelDefinition Channel, float Value);

public sealed record CaConsumer(string Name, int ChannelIndex)
{
    public CaChannelDefinition Channel => CaChannelCatalog.Get(ChannelIndex);

    public float Read(Room room) => CaQuery.Read(room, ChannelIndex);

    public RoomCaReading HighestRoom(IEnumerable<Room> rooms) => CaQuery.HighestRoom(rooms, ChannelIndex);

    public RoomCaReading LowestRoom(IEnumerable<Room> rooms) => CaQuery.LowestRoom(rooms, ChannelIndex);
}

public static class CaQuery
{
    public static float Read(Room room, int channelIndex) => room.CA[CaChannelCatalog.Get(channelIndex).Index];

    public static RoomCaReading HighestRoom(IEnumerable<Room> rooms, int channelIndex)
        => ExtremeRoom(rooms, channelIndex, findHighest: true);

    public static RoomCaReading LowestRoom(IEnumerable<Room> rooms, int channelIndex)
        => ExtremeRoom(rooms, channelIndex, findHighest: false);

    private static RoomCaReading ExtremeRoom(IEnumerable<Room> rooms, int channelIndex, bool findHighest)
    {
        CaChannelDefinition channel = CaChannelCatalog.Get(channelIndex);
        Room? bestRoom = null;
        float bestValue = findHighest ? float.NegativeInfinity : float.PositiveInfinity;

        foreach (Room room in rooms)
        {
            float value = room.CA[channel.Index];
            bool isBetter = findHighest ? value > bestValue : value < bestValue;
            if (!isBetter)
                continue;

            bestRoom = room;
            bestValue = value;
        }

        if (bestRoom == null)
            throw new InvalidOperationException("Cannot query CA channels from an empty room set.");

        return new RoomCaReading(bestRoom, channel, bestValue);
    }
}
