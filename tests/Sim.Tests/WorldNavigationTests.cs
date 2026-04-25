using CreaturesReborn.Sim.World;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class WorldNavigationTests
{
    [Fact]
    public void RoomContainsPoint_SupportsGodotUpwardCeilings()
    {
        var room = new Room
        {
            XLeft = 0,
            XRight = 10,
            YLeftFloor = 2,
            YRightFloor = 4,
            YLeftCeiling = 6,
            YRightCeiling = 8,
        };

        Assert.True(room.ContainsPoint(5, 5));
        Assert.False(room.ContainsPoint(5, 1));
        Assert.False(room.ContainsPoint(5, 9));
    }

    [Fact]
    public void SnapToNearestSurface_UsesSlopedFloorAtRequestedX()
    {
        var map = new GameMap();
        var metaRoom = map.AddMetaRoom(0, 0, 20, 20, "test");
        var room = map.AddRoom(metaRoom.Id, 0, 10, 6, 8, 2, 4);

        var nav = new RoomNavigation(map);
        var surface = nav.SnapToNearestSurface(5, 12);

        Assert.NotNull(surface);
        Assert.Same(room, surface.Value.Room);
        Assert.Equal(5, surface.Value.X);
        Assert.Equal(3, surface.Value.Y, precision: 3);
    }

    [Fact]
    public void FindRoute_CanTraverseStairAndElevatorLinks()
    {
        var map = new GameMap();
        var metaRoom = map.AddMetaRoom(0, 0, 40, 20, "test");
        var lower = map.AddRoom(metaRoom.Id, 0, 10, 4, 4, 1, 1);
        var middle = map.AddRoom(metaRoom.Id, 12, 22, 8, 8, 5, 5);
        var upper = map.AddRoom(metaRoom.Id, 24, 34, 12, 12, 9, 9);

        map.ConnectRooms(lower, middle, RoomLinkKind.Stair, 9, 1, 12, 5);
        map.ConnectRooms(middle, upper, RoomLinkKind.Elevator, 22, 5, 24, 9);

        var nav = new RoomNavigation(map);
        var route = nav.FindRoute(lower, upper);

        Assert.NotNull(route);
        Assert.Equal(2, route.Links.Count);
        Assert.Equal(RoomLinkKind.Stair, route.Links[0].Kind);
        Assert.Equal(RoomLinkKind.Elevator, route.Links[1].Kind);
        Assert.Equal(1, nav.FirstHorizontalDirection(1, 1, 30, 9));
    }

    [Fact]
    public void FindNearestReachableTarget_IgnoresDisconnectedCloserTargets()
    {
        var map = new GameMap();
        var metaRoom = map.AddMetaRoom(0, 0, 60, 20, "test");
        var start = map.AddRoom(metaRoom.Id, 0, 10, 4, 4, 1, 1);
        var connected = map.AddRoom(metaRoom.Id, 30, 40, 8, 8, 5, 5);
        map.AddRoom(metaRoom.Id, 12, 20, 4, 4, 1, 1);
        map.ConnectRooms(start, connected, RoomLinkKind.Stair, 10, 1, 30, 5);

        var nav = new RoomNavigation(map);
        var target = nav.FindNearestReachableTarget(
            2,
            1,
            new[]
            {
                new NavigationTarget(15, 1, "disconnected"),
                new NavigationTarget(35, 5, "connected"),
            });

        Assert.NotNull(target);
        Assert.Equal("connected", target.Value.Id);
        Assert.Same(connected, target.Value.Room);
    }
}
