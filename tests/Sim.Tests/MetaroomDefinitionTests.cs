using System;
using System.IO;
using System.Linq;
using CreaturesReborn.Sim.World;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class MetaroomDefinitionTests
{
    [Fact]
    public void MetaroomDefinitionJson_RoundTripsDoorAndEditableObjects()
    {
        var definition = new MetaroomDefinition
        {
            Id = "treehouse",
            Name = "Treehouse",
            BackgroundPath = "res://art/metaroom/metaroom-right-connector-v2.png",
            BackgroundWidth = 1983,
            BackgroundHeight = 793,
            WorldWidth = 40,
            WorldHeight = 13.5f,
            BackdropCenterY = 8.45f,
        };
        definition.Paths.Add(new MetaroomPathDefinition
        {
            Id = "bottom-path",
            Name = "Bottom Path",
            Kind = MetaroomPathKind.Floor,
            CeilingOffset = 3,
            RoomType = RoomType.IndoorWood,
            Points =
            {
                new MetaroomPoint(-10, 3.35f),
                new MetaroomPoint(0, 3.75f),
                new MetaroomPoint(10, 3.35f),
            },
        });
        definition.Objects.Add(new MetaroomObjectDefinition
        {
            Id = "door-treehouse-right",
            Name = "Right Door",
            Kind = MetaroomObjectKind.Door,
            Position = new MetaroomPoint(18.5f, 7.65f),
            Scale = 1.2f,
            Door = new DoorDefinition
            {
                TargetMetaroomId = "forest",
                TargetDoorId = "door-forest-left",
                TransitionMode = DoorTransitionMode.Portal,
                Bidirectional = true,
                CaptureRadius = 0.8f,
                ExitOffset = new MetaroomPoint(-0.6f, 0),
                Permeability = 90,
            },
        });

        string json = MetaroomDefinitionJson.Serialize(definition);
        MetaroomDefinition loaded = MetaroomDefinitionJson.Deserialize(json);

        Assert.Equal("treehouse", loaded.Id);
        Assert.Equal(1983, loaded.BackgroundWidth);
        Assert.Equal(MetaroomPathKind.Floor, loaded.Paths[0].Kind);
        Assert.Equal(3, loaded.Paths[0].Points.Count);
        Assert.Equal(MetaroomObjectKind.Door, loaded.Objects[0].Kind);
        Assert.Equal(DoorTransitionMode.Portal, loaded.Objects[0].Door!.TransitionMode);
        Assert.Equal("forest", loaded.Objects[0].Door!.TargetMetaroomId);
        Assert.Equal(0.8f, loaded.Objects[0].Door!.CaptureRadius, precision: 2);
    }

    [Fact]
    public void MetaroomDefinitionJson_SaveAndLoadFileUsesProjectFriendlyFormatting()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"metaroom-{Guid.NewGuid():N}.json");
        try
        {
            var definition = new MetaroomDefinition
            {
                Id = "test-room",
                Name = "Test Room",
                BackgroundPath = "res://art/metaroom/test.png",
                WorldWidth = 20,
                WorldHeight = 10,
            };

            MetaroomDefinitionJson.Save(tempPath, definition);
            string text = File.ReadAllText(tempPath);
            MetaroomDefinition loaded = MetaroomDefinitionJson.Load(tempPath);

            Assert.Contains("\"id\": \"test-room\"", text);
            Assert.Contains("\"paths\": []", text);
            Assert.Equal("test-room", loaded.Id);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void MetaroomWorldBuilder_ConvertsPolylineSegmentsToSlopedRooms()
    {
        var definition = new MetaroomDefinition
        {
            Id = "treehouse",
            Name = "Treehouse",
            WorldWidth = 40,
            WorldHeight = 14,
        };
        definition.Paths.Add(new MetaroomPathDefinition
        {
            Id = "ramp",
            Kind = MetaroomPathKind.Ramp,
            CeilingOffset = 2.5f,
            RoomType = RoomType.IndoorWood,
            Points =
            {
                new MetaroomPoint(-5, 2),
                new MetaroomPoint(0, 4),
                new MetaroomPoint(5, 3),
            },
        });

        var map = new GameMap();
        MetaroomBuildResult result = MetaroomWorldBuilder.Build(map, new[] { definition });

        Assert.Single(map.MetaRooms);
        Assert.Equal(2, map.AllRooms.Count);
        Assert.Equal(2, result.PathSegments.Count);
        Assert.Equal(3.0f, map.AllRooms[0].FloorYAtX(-2.5f), precision: 2);
        Assert.Equal(3.5f, map.AllRooms[1].FloorYAtX(2.5f), precision: 2);

        var navigation = new RoomNavigation(map);
        WalkableSurface? step = navigation.ProjectHorizontalStep(-0.2f, 3.92f, 0.2f);
        Assert.NotNull(step);
        Assert.Equal(3.96f, step!.Value.Y, precision: 2);
    }

    [Fact]
    public void MetaroomWorldBuilder_AddsCrossMetaroomDoorRoutes()
    {
        MetaroomDefinition left = DoorRoom("treehouse", "right-door", "forest", "left-door", 0, 10);
        MetaroomDefinition right = DoorRoom("forest", "left-door", "treehouse", "right-door", 100, 0);

        var map = new GameMap();
        MetaroomBuildResult result = MetaroomWorldBuilder.Build(map, new[] { left, right });

        Assert.Equal(2, map.MetaRooms.Count);
        Assert.Equal(2, result.Doors.Count);

        DoorEndpoint source = result.Doors["treehouse:right-door"];
        DoorEndpoint target = result.Doors["forest:left-door"];
        var route = new RoomNavigation(map).FindRoute(source.Room!, target.Room!);

        Assert.NotNull(route);
        Assert.Single(route!.Links);
        Assert.Equal(RoomLinkKind.Door, route.Links[0].Kind);
        Assert.Equal(10, route.Links[0].FromX, precision: 2);
        Assert.Equal(99.5f, route.Links[0].ToX, precision: 2);
    }

    private static MetaroomDefinition DoorRoom(
        string id,
        string doorId,
        string targetMetaroomId,
        string targetDoorId,
        float originX,
        float doorX)
    {
        var definition = new MetaroomDefinition
        {
            Id = id,
            Name = id,
            WorldX = originX,
            WorldWidth = 20,
            WorldHeight = 10,
        };
        definition.Paths.Add(new MetaroomPathDefinition
        {
            Id = $"{id}-floor",
            Kind = MetaroomPathKind.Floor,
            Points =
            {
                new MetaroomPoint(0, 2),
                new MetaroomPoint(10, 2),
            },
        });
        definition.Objects.Add(new MetaroomObjectDefinition
        {
            Id = doorId,
            Kind = MetaroomObjectKind.Door,
            Position = new MetaroomPoint(doorX, 2),
            Door = new DoorDefinition
            {
                TargetMetaroomId = targetMetaroomId,
                TargetDoorId = targetDoorId,
                TransitionMode = DoorTransitionMode.Portal,
                Bidirectional = true,
                ExitOffset = new MetaroomPoint(-0.5f, 0),
            },
        });
        return definition;
    }
}
