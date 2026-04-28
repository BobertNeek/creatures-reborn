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
    public void FindSurfaceBelow_ReturnsHighestWalkableSurfaceUnderPoint()
    {
        var map = new GameMap();
        var metaRoom = map.AddMetaRoom(0, 0, 20, 20, "test");
        var lower = map.AddRoom(metaRoom.Id, 0, 10, 4, 4, 1, 1);
        var upper = map.AddRoom(metaRoom.Id, 0, 10, 10, 10, 7, 7);

        var nav = new RoomNavigation(map);
        var fromAboveUpper = nav.FindSurfaceBelow(5, 12);
        var fromBelowUpper = nav.FindSurfaceBelow(5, 6);

        Assert.NotNull(fromAboveUpper);
        Assert.Same(upper, fromAboveUpper.Value.Room);
        Assert.Equal(7, fromAboveUpper.Value.Y, precision: 3);

        Assert.NotNull(fromBelowUpper);
        Assert.Same(lower, fromBelowUpper.Value.Room);
        Assert.Equal(1, fromBelowUpper.Value.Y, precision: 3);
    }

    [Fact]
    public void FindSurfaceBelow_DoesNotSnapHorizontallyOntoDistantLedges()
    {
        var map = new GameMap();
        var metaRoom = map.AddMetaRoom(0, 0, 40, 20, "test");
        map.AddRoom(metaRoom.Id, 0, 10, 4, 4, 1, 1);

        var nav = new RoomNavigation(map);
        var surface = nav.FindSurfaceBelow(20, 10);

        Assert.Null(surface);
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

    [Fact]
    public void ProjectHorizontalStep_ClampsAtEndOfCurrentSurface()
    {
        var map = new GameMap();
        var metaRoom = map.AddMetaRoom(0, 0, 40, 20, "test");
        var ledge = map.AddRoom(metaRoom.Id, 0, 10, 8, 8, 5, 5);
        map.AddRoom(metaRoom.Id, 20, 30, 8, 8, 5, 5);

        var nav = new RoomNavigation(map);
        var step = nav.ProjectHorizontalStep(9.8f, 5.0f, 10.4f);

        Assert.NotNull(step);
        Assert.Same(ledge, step.Value.Room);
        Assert.Equal(10.0f, step.Value.X, precision: 3);
        Assert.Equal(5.0f, step.Value.Y, precision: 3);
    }

    [Fact]
    public void ProjectHorizontalStep_TransfersAcrossAdjacentRampSurfaces()
    {
        var map = new GameMap();
        var metaRoom = map.AddMetaRoom(0, 0, 40, 20, "test");
        var lower = map.AddRoom(metaRoom.Id, 0, 10, 5, 5, 1, 1);
        var ramp = map.AddRoom(metaRoom.Id, 10, 14, 5, 9, 1, 5);
        var upper = map.AddRoom(metaRoom.Id, 14, 20, 9, 9, 5, 5);
        map.ConnectRooms(lower, ramp, RoomLinkKind.Stair, 10, 1, 10, 1);
        map.ConnectRooms(ramp, upper, RoomLinkKind.Stair, 14, 5, 14, 5);

        var nav = new RoomNavigation(map);
        var rampStep = nav.ProjectHorizontalStep(9.9f, 1.0f, 10.2f);
        var upperStep = nav.ProjectHorizontalStep(13.9f, 4.9f, 14.2f);

        Assert.NotNull(rampStep);
        Assert.Same(ramp, rampStep.Value.Room);
        Assert.Equal(10.2f, rampStep.Value.X, precision: 3);
        Assert.Equal(1.2f, rampStep.Value.Y, precision: 3);

        Assert.NotNull(upperStep);
        Assert.Same(upper, upperStep.Value.Room);
        Assert.Equal(14.2f, upperStep.Value.X, precision: 3);
        Assert.Equal(5.0f, upperStep.Value.Y, precision: 3);
    }
}
