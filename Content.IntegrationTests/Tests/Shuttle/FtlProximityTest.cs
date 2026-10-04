using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Shuttles.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Shuttle;



public sealed class FtlProximityTest : GameTest
{

    // Public because they appear in the signature of a public [TestCaseSource] test method.
    private readonly record struct RectSize(int Width, int Height);

    private readonly record struct FTLPositioning(RectSize Station, RectSize Shuttle);
    private readonly record struct FTLContext(TestMapData Station, TestMapData Shuttle, EntityUid Target);

    private static IEnumerable<(Vector2i, Tile)> RectTiles(RectSize rectangle)
    {
        return Enumerable.Range(0, rectangle.Width)
            .SelectMany(_ => Enumerable.Range(0, rectangle.Height),
                (x, y) => (new Vector2i(x, y), new Tile(1)));
    }

    private async Task AssertNoOverlap(FTLContext scenario, FTLPositioning positionContext)
    {
        var server = Pair.Server;
        var mapSys = server.System<SharedMapSystem>();
        var shuttleSys = server.System<ShuttleSystem>();
        var xformSys = server.System<SharedTransformSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.SetTiles(scenario.Station.Grid, [.. RectTiles(positionContext.Station)]);
            mapSys.SetTiles(scenario.Shuttle.Grid, [.. RectTiles(positionContext.Shuttle)]);

            var arrived = shuttleSys.TryFTLProximity(scenario.Shuttle.Grid, scenario.Target);
            Assert.That(arrived, Is.True);

            var stationBox = xformSys.GetWorldMatrix(scenario.Station.Grid.Owner)
                .TransformBox(scenario.Station.Grid.Comp.LocalAABB);
            var shuttleBox = xformSys.GetWorldMatrix(scenario.Shuttle.Grid.Owner)
                .TransformBox(scenario.Shuttle.Grid.Comp.LocalAABB);

            Assert.That(shuttleBox.Intersects(stationBox), Is.False,
                $"sizes={positionContext} station={stationBox} shuttle={shuttleBox}");
        });
    }

    [Test]
    public async Task ShuttleDoesNotOverlapTargetGrid()
    {
        var station = await Pair.CreateTestMap();
        var shuttle = await Pair.CreateTestMap();
        var context = new FTLContext(station, shuttle, station.Grid.Owner);
        var positioning = new FTLPositioning(new RectSize(12, 8), new RectSize(3, 2));
        await AssertNoOverlap(context, positioning);
    }


    [Test]
    public async Task ShuttleDoesNotOverlapStationWhenTargetIsMap()
    {
        var station = await Pair.CreateTestMap();
        var shuttle = await Pair.CreateTestMap();
        var context = new FTLContext(station, shuttle, station.MapUid);
        var positioning = new FTLPositioning(new RectSize(12, 8), new RectSize(3, 2));
        await AssertNoOverlap(context, positioning);
    }

    private const int MinSize = 1;
    private const int MaxStationSize = 224;
    private const int MaxShuttleSize = 100;

    // Each seed is one test run. The seed shows in the test name, so a failing run can be replayed.
    private static IEnumerable<int> Seeds()
    {
        return Enumerable.Range(0, 30);
    }

    [TestCaseSource(nameof(Seeds))]
    public async Task ShuttleDoesNotOverlapTargetGridRandomSizes(int seed)
    {
        var rng = new Random(seed);
        var station = await Pair.CreateTestMap();
        var shuttle = await Pair.CreateTestMap();
        var context = new FTLContext(station, shuttle, station.Grid.Owner);
        // Shuttle first, then a station strictly larger than it in both dimensions.
        var shuttleSize = new RectSize(rng.Next(MinSize, MaxShuttleSize + 1), rng.Next(MinSize, MaxShuttleSize + 1));
        var stationSize = new RectSize(
            rng.Next(shuttleSize.Width + 1, MaxStationSize + 1),
            rng.Next(shuttleSize.Height + 1, MaxStationSize + 1));
        var positioning = new FTLPositioning(stationSize, shuttleSize);
        await AssertNoOverlap(context, positioning);
    }

    // TODO 5: repeat many times with different random choices.
    //  ShuttleSystem uses an injected IRobustRandom (ShuttleSystem.cs:39).
    //  Need to investigate how to seed that instance so a failing run can be replayed.
    //   (EntityTableTest.SeededRand seeds its own RobustRandom, which is a different instance.)
}
