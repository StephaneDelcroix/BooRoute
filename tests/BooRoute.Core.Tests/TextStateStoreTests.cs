using System.Text.Json;

namespace BooRoute.Core.Tests;

public sealed class TextStateStoreTests
{
    private static House Sample() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Pumpkin Porch", "13 Make-Believe Lane", "Friendly pumpkins.",
        new(17, 0), new(21, 0), ScareLevel.Friendly, HouseStatus.LightsOn, true, true);

    [Fact]
    public async Task MissingTextLoadsAsNoSavedState()
    {
        var store = new TextDemoStateStore(() => null, _ => throw new InvalidOperationException());

        Assert.Null(await store.LoadAsync());
    }

    [Theory]
    [MemberData(nameof(SavedStateValidationTests.InvalidSavedStates), MemberType = typeof(SavedStateValidationTests))]
    public async Task InvalidSavedTextIsRejectedWithoutBeingOverwritten(string json)
    {
        var saved = json;
        var store = new TextDemoStateStore(() => saved, text => saved = text);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());

        Assert.Equal(json, saved);
    }

    [Fact]
    public async Task SavedStateRoundTrips()
    {
        string? saved = null;
        var store = new TextDemoStateStore(() => saved, text => saved = text);
        var house = Sample();

        await store.SaveAsync(new DemoState([house], [house.Id]));
        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(house, Assert.Single(loaded.Houses));
        Assert.Equal(house.Id, Assert.Single(loaded.Visited));
    }

    [Fact]
    public async Task BackendSeedsAndPersistsThroughTextStore()
    {
        string? saved = null;
        var store = new TextDemoStateStore(() => saved, text => saved = text);
        var backend = new MockNeighborhoodBackend(store, TimeProvider.System);

        var snapshot = await backend.GetSnapshotAsync();
        await backend.SetVisitedAsync(snapshot.Houses[0].Id, true);

        var reloaded = await new MockNeighborhoodBackend(store, TimeProvider.System).GetSnapshotAsync();
        Assert.Equal(snapshot.Houses, reloaded.Houses);
        Assert.Contains(snapshot.Houses[0].Id, reloaded.Visited);
        Assert.NotNull(JsonSerializer.Deserialize<DemoState>(saved!));
    }
}
