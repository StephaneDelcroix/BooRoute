namespace BooRoute.Core.Tests;

public sealed class VisitWriteFailureTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedStampWritePreservesStateAndRetryPublishesOnlySavedChange(bool collected)
    {
        var house = new House(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Pumpkin Porch", "13 Make-Believe Lane", "Friendly pumpkins.",
            new(17, 0), new(21, 0), ScareLevel.Friendly, HouseStatus.LightsOn, true, true);
        var other = house with
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Nickname = "Ghost Garden",
            Address = "15 Make-Believe Lane"
        };
        House[] houses = [house, other];
        var initialVisits = new HashSet<Guid> { other.Id };
        if (!collected)
            initialVisits.Add(house.Id);
        var initialPassport = initialVisits.Select(id => new PassportEntry(id, null)).ToList();
        var store = new MemoryStore(new(houses.ToList(), initialPassport));
        var backend = new MockNeighborhoodBackend(store, TimeProvider.System);
        var before = await backend.GetSnapshotAsync();
        var events = 0;
        backend.Changed += (_, _) => events++;
        var failure = new IOException("Simulated stamp write failure.");
        store.WriteFailure = failure;

        var reported = await Assert.ThrowsAsync<IOException>(() => backend.SetStampCollectedAsync(house.Id, collected));

        Assert.Same(failure, reported);
        var afterFailure = await backend.GetSnapshotAsync();
        Assert.Equal(houses, afterFailure.Houses);
        Assert.Equal(initialPassport, afterFailure.Passport);
        Assert.Equal(houses, store.PersistedState.Houses);
        Assert.Equal(initialPassport, store.PersistedState.Passport);
        Assert.Equal(0, events);

        store.WriteFailure = null;
        await backend.SetStampCollectedAsync(house.Id, collected);

        var expectedVisits = new HashSet<Guid> { other.Id };
        if (collected)
            expectedVisits.Add(house.Id);
        var afterRetry = await backend.GetSnapshotAsync();
        Assert.Equal(houses, afterRetry.Houses);
        Assert.True(expectedVisits.SetEquals(afterRetry.Passport.Select(entry => entry.HouseId)));
        Assert.Equal(new PassportEntry(other.Id, null), afterRetry.Passport.Single(entry => entry.HouseId == other.Id));
        if (collected)
            Assert.NotNull(afterRetry.Passport.Single(entry => entry.HouseId == house.Id).CollectedAt);
        Assert.Equal(houses, store.PersistedState.Houses);
        Assert.Equal(afterRetry.Passport, store.PersistedState.Passport);
        Assert.Equal(houses, before.Houses);
        Assert.Equal(initialPassport, before.Passport);
        Assert.Equal(1, events);
    }

    private sealed class MemoryStore(DemoState initialState) : IDemoStateStore
    {
        public DemoState PersistedState { get; private set; } = initialState;
        public IOException? WriteFailure { get; set; }

        public Task<DemoState?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DemoState?>(new(PersistedState.Houses.ToList(), new(PersistedState.Passport)));

        public Task SaveAsync(DemoState state, CancellationToken cancellationToken = default)
        {
            if (WriteFailure is { } failure)
                throw failure;
            PersistedState = new(state.Houses.ToList(), new(state.Passport));
            return Task.CompletedTask;
        }
    }
}
