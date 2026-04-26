using System;
using System.Collections.Generic;

namespace CreaturesReborn.Sim.World;

public enum RoomLinkKind
{
    Walk,
    Stair,
    Elevator,
    Door,
}

public sealed record RoomNavigationLink(
    Room From,
    Room To,
    RoomLinkKind Kind,
    float FromX,
    float FromY,
    float ToX,
    float ToY,
    int Permeability);

public readonly record struct WalkableSurface(Room Room, float X, float Y, float DistanceSquared);

public readonly record struct NavigationTarget(float X, float Y, string Id);

public readonly record struct ResolvedNavigationTarget(
    string Id,
    float X,
    float Y,
    Room Room,
    RoomRoute Route,
    float RouteCost);

public sealed class RoomRoute
{
    public RoomRoute(Room start, Room end, IReadOnlyList<RoomNavigationLink> links)
    {
        Start = start;
        End = end;
        Links = links;
    }

    public Room Start { get; }
    public Room End { get; }
    public IReadOnlyList<RoomNavigationLink> Links { get; }
}

public sealed class RoomNavigation
{
    private readonly GameMap _map;
    private const float SurfaceTouchTolerance = 0.65f;
    private const float SurfaceGapTolerance = 0.08f;

    public RoomNavigation(GameMap map)
    {
        _map = map;
    }

    public WalkableSurface? SnapToNearestSurface(float x, float y)
    {
        WalkableSurface? best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < _map.AllRooms.Count; i++)
        {
            Room room = _map.AllRooms[i];
            float sx = Math.Clamp(x, room.XLeft, room.XRight);
            float sy = room.FloorYAtX(sx);
            float dx = sx - x;
            float dy = sy - y;
            float distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = new WalkableSurface(room, sx, sy, distance);
            }
        }

        return best;
    }

    public WalkableSurface? ProjectHorizontalStep(float fromX, float fromY, float requestedX)
    {
        WalkableSurface? current = SnapToNearestSurface(fromX, fromY);
        if (current == null) return null;

        float direction = MathF.Sign(requestedX - fromX);
        if (direction == 0)
            return current.Value;

        WalkableSurface? best = null;
        float bestScore = float.MaxValue;
        Room currentRoom = current.Value.Room;

        for (int i = 0; i < _map.AllRooms.Count; i++)
        {
            Room room = _map.AllRooms[i];
            if (!CanStepBetween(currentRoom, room))
                continue;

            float stepX = Math.Clamp(requestedX, room.XLeft, room.XRight);
            if (direction > 0 && stepX < fromX - SurfaceGapTolerance)
                continue;
            if (direction < 0 && stepX > fromX + SurfaceGapTolerance)
                continue;

            float stepY = room.FloorYAtX(stepX);
            float verticalDelta = MathF.Abs(stepY - current.Value.Y);
            if (!ReferenceEquals(room, currentRoom) && verticalDelta > SurfaceTouchTolerance)
                continue;

            float dx = requestedX - stepX;
            float distance = dx * dx + verticalDelta * verticalDelta;
            float score = dx * dx * 4f + verticalDelta * verticalDelta;
            if (!ReferenceEquals(room, currentRoom))
                score -= 0.01f;

            if (score < bestScore)
            {
                bestScore = score;
                best = new WalkableSurface(room, stepX, stepY, distance);
            }
        }

        if (best != null)
            return best;

        float clampedX = Math.Clamp(requestedX, currentRoom.XLeft, currentRoom.XRight);
        return new WalkableSurface(
            currentRoom,
            clampedX,
            currentRoom.FloorYAtX(clampedX),
            0);
    }

    public Room? ResolveRoom(float x, float y)
    {
        Room? exact = _map.RoomAt(x, y);
        if (exact != null) return exact;
        return SnapToNearestSurface(x, y)?.Room;
    }

    public RoomRoute? FindRoute(Room start, Room end)
    {
        if (ReferenceEquals(start, end))
            return new RoomRoute(start, end, Array.Empty<RoomNavigationLink>());

        var visited = new HashSet<Room> { start };
        var queue = new Queue<(Room Room, List<RoomNavigationLink> Links)>();
        queue.Enqueue((start, new List<RoomNavigationLink>()));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            IReadOnlyList<RoomNavigationLink> links = _map.GetNavigationLinks(current.Room);
            for (int i = 0; i < links.Count; i++)
            {
                RoomNavigationLink link = links[i];
                if (!visited.Add(link.To)) continue;

                var routeLinks = new List<RoomNavigationLink>(current.Links) { link };
                if (ReferenceEquals(link.To, end))
                    return new RoomRoute(start, end, routeLinks);

                queue.Enqueue((link.To, routeLinks));
            }
        }

        return null;
    }

    public int FirstHorizontalDirection(float fromX, float fromY, float targetX, float targetY)
    {
        Room? start = ResolveRoom(fromX, fromY);
        Room? end = ResolveRoom(targetX, targetY);
        if (start == null || end == null) return Direction(targetX - fromX);

        RoomRoute? route = FindRoute(start, end);
        if (route == null || route.Links.Count == 0)
            return Direction(targetX - fromX);

        return Direction(route.Links[0].FromX - fromX);
    }

    public ResolvedNavigationTarget? FindNearestReachableTarget(
        float fromX,
        float fromY,
        IEnumerable<NavigationTarget> targets)
    {
        Room? start = ResolveRoom(fromX, fromY);
        if (start == null) return null;

        ResolvedNavigationTarget? best = null;
        float bestCost = float.MaxValue;

        foreach (NavigationTarget target in targets)
        {
            Room? targetRoom = ResolveRoom(target.X, target.Y);
            if (targetRoom == null) continue;

            RoomRoute? route = FindRoute(start, targetRoom);
            if (route == null) continue;

            float cost = RouteCost(fromX, fromY, target.X, target.Y, route);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = new ResolvedNavigationTarget(
                    target.Id, target.X, target.Y, targetRoom, route, cost);
            }
        }

        return best;
    }

    private static int Direction(float dx)
    {
        const float DeadZone = 0.05f;
        if (MathF.Abs(dx) <= DeadZone) return 0;
        return dx > 0 ? 1 : -1;
    }

    private bool CanStepBetween(Room current, Room candidate)
    {
        if (ReferenceEquals(current, candidate))
            return true;

        IReadOnlyList<RoomNavigationLink> links = _map.GetNavigationLinks(current);
        for (int i = 0; i < links.Count; i++)
        {
            if (ReferenceEquals(links[i].To, candidate))
                return true;
        }

        return SurfacesTouch(current, candidate);
    }

    private static bool SurfacesTouch(Room a, Room b)
    {
        float overlapLeft = MathF.Max(a.XLeft, b.XLeft);
        float overlapRight = MathF.Min(a.XRight, b.XRight);
        if (overlapLeft <= overlapRight)
        {
            float x = Math.Clamp((overlapLeft + overlapRight) * 0.5f, overlapLeft, overlapRight);
            return MathF.Abs(a.FloorYAtX(x) - b.FloorYAtX(x)) <= SurfaceTouchTolerance;
        }

        Room left = a.XRight < b.XLeft ? a : b;
        Room right = ReferenceEquals(left, a) ? b : a;
        float gap = right.XLeft - left.XRight;
        if (gap > SurfaceGapTolerance)
            return false;

        return MathF.Abs(left.FloorYAtX(left.XRight) - right.FloorYAtX(right.XLeft))
               <= SurfaceTouchTolerance;
    }

    private static float RouteCost(float fromX, float fromY, float targetX, float targetY, RoomRoute route)
    {
        if (route.Links.Count == 0)
            return SquaredDistance(fromX, fromY, targetX, targetY);

        float cost = 0;
        float cx = fromX;
        float cy = fromY;
        for (int i = 0; i < route.Links.Count; i++)
        {
            RoomNavigationLink link = route.Links[i];
            cost += SquaredDistance(cx, cy, link.FromX, link.FromY);
            cost += SquaredDistance(link.FromX, link.FromY, link.ToX, link.ToY);
            cx = link.ToX;
            cy = link.ToY;
        }

        cost += SquaredDistance(cx, cy, targetX, targetY);
        return cost;
    }

    private static float SquaredDistance(float ax, float ay, float bx, float by)
    {
        float dx = bx - ax;
        float dy = by - ay;
        return dx * dx + dy * dy;
    }
}
