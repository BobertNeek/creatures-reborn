using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.World;

public sealed record MetaroomRoomSegment(
    string MetaroomId,
    string PathId,
    int SegmentIndex,
    Room Room);

public sealed record DoorEndpoint(
    string MetaroomId,
    string DoorId,
    MetaroomObjectDefinition Definition,
    float WorldX,
    float WorldY,
    Room? Room);

public sealed class MetaroomBuildResult
{
    public List<MetaroomRoomSegment> PathSegments { get; } = new();
    public Dictionary<string, DoorEndpoint> Doors { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class MetaroomWorldBuilder
{
    public static MetaroomBuildResult Build(GameMap map, IEnumerable<MetaroomDefinition> definitions)
    {
        map.Reset();
        var result = new MetaroomBuildResult();
        var definitionsById = new Dictionary<string, MetaroomDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (MetaroomDefinition definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
                throw new InvalidOperationException("Metaroom definitions must have an id.");

            definitionsById[definition.Id] = definition;
            MetaRoom metaRoom = map.AddMetaRoom(
                definition.WorldX - definition.WorldWidth * 0.5f,
                definition.WorldY,
                definition.WorldWidth,
                definition.WorldHeight,
                definition.BackgroundPath);
            metaRoom.Name = string.IsNullOrWhiteSpace(definition.Name) ? definition.Id : definition.Name;

            BuildPathRooms(map, metaRoom, definition, result);
        }

        foreach (MetaroomDefinition definition in definitionsById.Values)
            RegisterDoorEndpoints(map, definition, result);

        foreach (DoorEndpoint endpoint in result.Doors.Values)
            ConnectDoorEndpoint(map, result, endpoint);

        return result;
    }

    private static void BuildPathRooms(
        GameMap map,
        MetaRoom metaRoom,
        MetaroomDefinition definition,
        MetaroomBuildResult result)
    {
        for (int pathIndex = 0; pathIndex < definition.Paths.Count; pathIndex++)
        {
            MetaroomPathDefinition path = definition.Paths[pathIndex];
            if (!path.Enabled || path.Points.Count < 2)
                continue;

            Room? previous = null;
            for (int i = 0; i < path.Points.Count - 1; i++)
            {
                MetaroomPoint a = path.Points[i];
                MetaroomPoint b = path.Points[i + 1];
                float ax = definition.WorldX + a.X;
                float ay = definition.WorldY + a.Y;
                float bx = definition.WorldX + b.X;
                float by = definition.WorldY + b.Y;
                if (MathF.Abs(bx - ax) < 0.001f)
                    continue;

                float xLeft;
                float yLeft;
                float xRight;
                float yRight;
                if (ax < bx)
                {
                    xLeft = ax;
                    yLeft = ay;
                    xRight = bx;
                    yRight = by;
                }
                else
                {
                    xLeft = bx;
                    yLeft = by;
                    xRight = ax;
                    yRight = ay;
                }

                Room room = map.AddRoom(
                    metaRoom.Id,
                    xLeft,
                    xRight,
                    yLeft + path.CeilingOffset,
                    yRight + path.CeilingOffset,
                    yLeft,
                    yRight);
                room.Type = path.RoomType;

                if (previous != null)
                {
                    float linkX = definition.WorldX + a.X;
                    float linkY = definition.WorldY + a.Y;
                    map.ConnectRooms(previous, room, RoomLinkKind.Walk, linkX, linkY, linkX, linkY);
                }

                result.PathSegments.Add(new MetaroomRoomSegment(definition.Id, path.Id, i, room));
                previous = room;
            }
        }
    }

    private static void RegisterDoorEndpoints(
        GameMap map,
        MetaroomDefinition definition,
        MetaroomBuildResult result)
    {
        for (int i = 0; i < definition.Objects.Count; i++)
        {
            MetaroomObjectDefinition obj = definition.Objects[i];
            if (!obj.Enabled || obj.Kind != MetaroomObjectKind.Door || obj.Door == null)
                continue;

            float worldX = definition.WorldX + obj.Position.X;
            float worldY = definition.WorldY + obj.Position.Y;
            Room? room = ResolveNearestRoom(map, worldX, worldY);
            result.Doors[DoorKey(definition.Id, obj.Id)] = new DoorEndpoint(
                definition.Id,
                obj.Id,
                obj,
                worldX,
                worldY,
                room);
        }
    }

    private static void ConnectDoorEndpoint(GameMap map, MetaroomBuildResult result, DoorEndpoint endpoint)
    {
        DoorDefinition? door = endpoint.Definition.Door;
        if (door == null || endpoint.Room == null)
            return;
        if (door.TransitionMode != DoorTransitionMode.Portal)
            return;
        if (string.IsNullOrWhiteSpace(door.TargetMetaroomId)
            || string.IsNullOrWhiteSpace(door.TargetDoorId))
            return;
        if (!result.Doors.TryGetValue(DoorKey(door.TargetMetaroomId, door.TargetDoorId), out DoorEndpoint? target))
            return;
        if (target.Room == null)
            return;

        map.ConnectRooms(
            endpoint.Room,
            target.Room,
            RoomLinkKind.Door,
            endpoint.WorldX,
            endpoint.WorldY,
            target.WorldX + door.ExitOffset.X,
            target.WorldY + door.ExitOffset.Y,
            Math.Clamp(door.Permeability, 0, 100),
            door.Bidirectional);
    }

    private static Room? ResolveNearestRoom(GameMap map, float x, float y)
    {
        Room? exact = map.RoomAt(x, y);
        if (exact != null)
            return exact;

        Room? best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < map.AllRooms.Count; i++)
        {
            Room room = map.AllRooms[i];
            float sx = Math.Clamp(x, room.XLeft, room.XRight);
            float sy = room.FloorYAtX(sx);
            float dx = sx - x;
            float dy = sy - y;
            float distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = room;
            }
        }

        return best;
    }

    public static string DoorKey(string metaroomId, string doorId)
        => $"{metaroomId}:{doorId}";
}
