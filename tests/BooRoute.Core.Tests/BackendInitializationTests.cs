namespace BooRoute.Core.Tests;

public sealed class BackendInitializationTests
{
    [Fact]
    public async Task MissingStateIsSeededAndSavedBeforeSnapshotReturns()
    {
        var saveCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new CountingStore(null) { SaveCompletion = saveCompletion.Task };
        var backend = new MockNeighborhoodBackend(store, new FixedClock());

        var pendingSnapshot = backend.GetSnapshotAsync();

        Assert.Equal(1, store.LoadCount);
        Assert.Equal(1, store.SaveCount);
        Assert.False(pendingSnapshot.IsCompleted);
        Assert.Null(store.SavedState);

        saveCompletion.SetResult();
        var snapshot = await pendingSnapshot;
        var saved = Assert.IsType<DemoState>(store.SavedState);

        Assert.NotEmpty(saved.Houses);
        Assert.All(saved.Houses, house =>
        {
            Assert.Empty(house.Validate());
            Assert.Equal(new TimeOnly(16, 0), house.OpensAt);
            Assert.Equal(new TimeOnly(22, 0), house.ClosesAt);
        });
        Assert.Empty(saved.Passport);
        Assert.Equal(saved.Houses, snapshot.Houses);
        Assert.Equal(saved.Passport, snapshot.Passport);
        Assert.Equal(1, store.LoadCount);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task ExistingStateIsReturnedUnchangedWithoutSaving()
    {
        var existing = ExistingState();
        var expectedHouses = existing.Houses.ToArray();
        var expectedPassport = existing.Passport.ToArray();
        var store = new CountingStore(existing);
        var backend = new MockNeighborhoodBackend(store, new FixedClock());

        var snapshot = await backend.GetSnapshotAsync();

        Assert.Equal(expectedHouses, snapshot.Houses);
        Assert.Equal(expectedPassport[0], Assert.Single(snapshot.Passport));
        Assert.Equal(expectedHouses, existing.Houses);
        Assert.Equal(expectedPassport[0], Assert.Single(existing.Passport));
        Assert.Equal(1, store.LoadCount);
        Assert.Equal(0, store.SaveCount);
        Assert.Null(store.SavedState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedSnapshotsReuseInitializedState(bool hasExistingState)
    {
        var existing = hasExistingState ? ExistingState() : null;
        var store = new CountingStore(existing);
        var backend = new MockNeighborhoodBackend(store, new FixedClock());
        var first = await backend.GetSnapshotAsync();
        var initialized = existing ?? Assert.IsType<DemoState>(store.SavedState);
        var expectedHouses = initialized.Houses.ToArray();
        var expectedPassport = initialized.Passport.ToArray();
        store.State = new([], []);

        Assert.NotEmpty(expectedHouses);
        Assert.Equal(expectedHouses, first.Houses);
        Assert.Equal(expectedPassport, first.Passport);
        for (var i = 0; i < 3; i++)
        {
            var snapshot = await backend.GetSnapshotAsync();
            Assert.Equal(expectedHouses, snapshot.Houses);
            Assert.Equal(expectedPassport, snapshot.Passport);
        }

        Assert.Equal(1, store.LoadCount);
        Assert.Equal(hasExistingState ? 0 : 1, store.SaveCount);
        if (!hasExistingState)
        {
            var saved = Assert.IsType<DemoState>(store.SavedState);
            Assert.Equal(expectedHouses, saved.Houses);
            Assert.Equal(expectedPassport, saved.Passport);
        }
    }

    private static DemoState ExistingState()
    {
        House first = new(new Guid("11111111-1111-1111-1111-111111111111"),
            "Pumpkin Porch", "13 Make-Believe Lane", "Friendly pumpkins.",
            new(17, 0), new(21, 0), ScareLevel.Friendly, HouseStatus.LightsOn, true, true);
        var second = first with
        {
            Id = new Guid("22222222-2222-2222-2222-222222222222"),
            Nickname = "Moon Porch",
            Address = "14 Make-Believe Lane",
            Status = HouseStatus.Closed
        };
        return new([first, second], [new(second.Id, null)]);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class CountingStore(DemoState? state) : IDemoStateStore
    {
        public DemoState? State { get; set; } = state;
        public DemoState? SavedState { get; private set; }
        public int LoadCount { get; private set; }
        public int SaveCount { get; private set; }
        public Task SaveCompletion { get; init; } = Task.CompletedTask;

        public Task<DemoState?> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult(State);
        }

        public async Task SaveAsync(DemoState value, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            await SaveCompletion;
            State = SavedState = value;
        }
    }
}
