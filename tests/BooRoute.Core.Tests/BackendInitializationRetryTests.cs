namespace BooRoute.Core.Tests;

public sealed class BackendInitializationRetryTests
{
    [Fact]
    public async Task FailedFirstSeedSaveIsNotCachedAndCanBeRetried()
    {
        var clock = new FixedClock(new(2026, 10, 31, 18, 0, 0, TimeSpan.Zero));
        var store = new ControllableStore { FailWrites = true };
        var backend = new MockNeighborhoodBackend(store, clock);
        var changes = 0;
        backend.Changed += (_, _) => changes++;

        await Assert.ThrowsAsync<IOException>(() => backend.GetSnapshotAsync());

        Assert.Equal(1, store.LoadAttempts);
        Assert.Equal(1, store.SaveAttempts);
        Assert.Null(store.PersistedState);
        Assert.Equal(0, changes);
        var failedSeed = Assert.IsType<DemoState>(store.LastSaveAttempt);
        Assert.NotEmpty(failedSeed.Houses);
        Assert.All(failedSeed.Houses, house =>
        {
            Assert.Equal(new TimeOnly(16, 0), house.OpensAt);
            Assert.Equal(new TimeOnly(22, 0), house.ClosesAt);
        });

        clock.Now = clock.Now.AddHours(1);
        store.FailWrites = false;
        var snapshot = await backend.GetSnapshotAsync();

        Assert.Equal(2, store.LoadAttempts);
        Assert.Equal(2, store.SaveAttempts);
        var saved = Assert.IsType<DemoState>(store.PersistedState);
        Assert.NotSame(failedSeed, saved);
        Assert.NotEmpty(snapshot.Houses);
        Assert.Equal(saved.Houses, snapshot.Houses);
        Assert.Equal(snapshot.Passport, saved.Passport);
        Assert.Empty(saved.Passport);
        Assert.All(snapshot.Houses, house =>
        {
            Assert.Equal(new TimeOnly(17, 0), house.OpensAt);
            Assert.Equal(new TimeOnly(23, 0), house.ClosesAt);
        });
        Assert.Equal(0, changes);

        var cached = await backend.GetSnapshotAsync();

        Assert.Equal(2, store.LoadAttempts);
        Assert.Equal(2, store.SaveAttempts);
        Assert.Equal(saved.Houses, cached.Houses);
        Assert.Equal(cached.Passport, saved.Passport);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class ControllableStore : IDemoStateStore
    {
        public bool FailWrites { get; set; }
        public int LoadAttempts { get; private set; }
        public int SaveAttempts { get; private set; }
        public DemoState? LastSaveAttempt { get; private set; }
        public DemoState? PersistedState { get; private set; }

        public Task<DemoState?> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadAttempts++;
            return Task.FromResult(PersistedState);
        }

        public Task SaveAsync(DemoState state, CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            LastSaveAttempt = state;
            if (FailWrites)
                return Task.FromException(new IOException("Simulated seed save failure."));
            PersistedState = state;
            return Task.CompletedTask;
        }
    }
}
