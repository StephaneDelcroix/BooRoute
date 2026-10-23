namespace BooRoute.Core.Tests;

public sealed class BackendRejectedWriteTests
{
    [Fact]
    public async Task InvalidHouseIsRejectedBeforeStoreAccessAndLaterSaveSucceeds()
    {
        var house = Sample();
        Assert.Empty(house.Validate());
        var original = new DemoState([house], [new(house.Id, null)]);
        var store = new CountingStore(original);
        var backend = new MockNeighborhoodBackend(store, new FixedClock());
        var changed = 0;
        backend.Changed += (_, _) => changed++;

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => backend.SaveHouseAsync(house with { Nickname = "" }));

        Assert.Equal("house", error.ParamName);
        Assert.Equal(0, store.LoadCount);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, changed);
        Assert.Same(original, store.State);
        Assert.Equal(house, Assert.Single(store.State.Houses));
        Assert.Equal(house.Id, Assert.Single(store.State.Passport).HouseId);

        var unchanged = await backend.GetSnapshotAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(house, Assert.Single(unchanged.Houses));
        Assert.Equal(house.Id, Assert.Single(unchanged.Passport).HouseId);
        Assert.Equal(1, store.LoadCount);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, changed);

        var updated = house with { Nickname = "Updated porch" };
        await backend.SaveHouseAsync(updated).WaitAsync(TimeSpan.FromSeconds(5));
        var afterSave = await backend.GetSnapshotAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(updated, Assert.Single(afterSave.Houses));
        Assert.Equal(house.Id, Assert.Single(afterSave.Passport).HouseId);
        Assert.Equal(updated, Assert.Single(store.State.Houses));
        Assert.Equal(house.Id, Assert.Single(store.State.Passport).HouseId);
        Assert.Equal(1, store.LoadCount);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, changed);
        Assert.Equal(house, Assert.Single(unchanged.Houses));
        Assert.Equal(house.Id, Assert.Single(unchanged.Passport).HouseId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnknownHouseCollectionIsRejectedWithoutChangesAndLaterCollectionChangeSucceeds(bool collected)
    {
        var house = Sample();
        var other = house with
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Nickname = "Ghost Garden",
            Address = "15 Make-Believe Lane"
        };
        House[] houses = [house, other];
        var initialPassport = new List<PassportEntry> { new(other.Id, null) };
        if (!collected)
            initialPassport.Add(new(house.Id, null));
        var original = new DemoState(houses.ToList(), new(initialPassport));
        var store = new CountingStore(original);
        var backend = new MockNeighborhoodBackend(store, new FixedClock());
        var before = await backend.GetSnapshotAsync();
        var changed = 0;
        backend.Changed += (_, _) => changed++;
        var unknownId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => backend.SetStampCollectedAsync(unknownId, collected));

        Assert.Equal("houseId", error.ParamName);
        Assert.Equal(1, store.LoadCount);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, changed);
        Assert.Same(original, store.State);
        Assert.Equal(houses, store.State.Houses);
        Assert.Equal(initialPassport, store.State.Passport);
        var unchanged = await backend.GetSnapshotAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(houses, unchanged.Houses);
        Assert.Equal(initialPassport, unchanged.Passport);
        Assert.Equal(1, store.LoadCount);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, changed);

        await backend.SetStampCollectedAsync(house.Id, collected).WaitAsync(TimeSpan.FromSeconds(5));
        var afterChange = await backend.GetSnapshotAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var expectedPassport = new List<PassportEntry> { new(other.Id, null) };
        if (collected)
            expectedPassport.Add(new(house.Id, new FixedClock().GetUtcNow()));

        Assert.Equal(houses, afterChange.Houses);
        Assert.Equal(expectedPassport, afterChange.Passport);
        Assert.Equal(houses, store.State.Houses);
        Assert.Equal(expectedPassport, store.State.Passport);
        Assert.Equal(1, store.LoadCount);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, changed);
        Assert.Equal(houses, before.Houses);
        Assert.Equal(initialPassport, before.Passport);
    }

    private static House Sample() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Pumpkin Porch", "13 Make-Believe Lane", "Friendly pumpkins.",
        new(17, 0), new(21, 0), ScareLevel.Friendly, HouseStatus.LightsOn, true, true);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class CountingStore(DemoState state) : IDemoStateStore
    {
        public DemoState State { get; private set; } = state;
        public int LoadCount { get; private set; }
        public int SaveCount { get; private set; }

        public Task<DemoState?> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult<DemoState?>(new(State.Houses.ToList(), new(State.Passport)));
        }

        public Task SaveAsync(DemoState value, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            State = new(value.Houses.ToList(), new(value.Passport));
            return Task.CompletedTask;
        }
    }
}
