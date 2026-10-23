namespace BooRoute.Core.Tests;

public sealed class PassportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 18, 42, 0, TimeSpan.Zero);

    private static House Sample(ScareLevel level = ScareLevel.Friendly, bool quiet = true) =>
        new(Guid.NewGuid(), "Pumpkin Porch", "13 Make-Believe Lane", "Friendly pumpkins.",
            new(17, 0), new(21, 0), level, HouseStatus.LightsOn, quiet, true);

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", StampSymbol.Pumpkin, StampPalette.Pumpkin, StampAccent.Stars)]
    [InlineData("00112233-4455-6677-8899-aabbccddeeff", StampSymbol.Pumpkin, StampPalette.Pumpkin, StampAccent.Dots)]
    [InlineData("11111111-1111-1111-1111-111111111111", StampSymbol.Bat, StampPalette.Pumpkin, StampAccent.Dots)]
    [InlineData("01000000-0000-0000-0000-000000000000", StampSymbol.Ghost, StampPalette.Lavender, StampAccent.Sparkles)]
    [InlineData("02000000-0000-0000-0000-000000000000", StampSymbol.Bat, StampPalette.Mint, StampAccent.Dots)]
    [InlineData("03000000-0000-0000-0000-000000000000", StampSymbol.Cat, StampPalette.Rose, StampAccent.Stars)]
    [InlineData("04000000-0000-0000-0000-000000000000", StampSymbol.Moon, StampPalette.Pumpkin, StampAccent.Sparkles)]
    [InlineData("05000000-0000-0000-0000-000000000000", StampSymbol.Spider, StampPalette.Lavender, StampAccent.Dots)]
    public void KnownGuidProducesStableDesign(string id, StampSymbol symbol, StampPalette palette, StampAccent accent)
    {
        var expected = new StampDesign(symbol, palette, accent);
        Assert.Equal(expected, StampGenerator.Generate(Guid.Parse(id)));
        Assert.Equal(expected, StampGenerator.Generate(new Guid(Guid.Parse(id).ToByteArray())));
    }

    [Fact]
    public void HostMetadataDoesNotChangeStampIdentity()
    {
        var house = Sample();
        var changed = house with { ScareLevel = ScareLevel.Scary, QuietVisits = false, StepFreeAccess = false };
        Assert.Equal(StampGenerator.Generate(house.Id), StampGenerator.Generate(changed.Id));
    }

    [Fact]
    public async Task CollectionUsesInjectedClockAndIdempotentRecollectionPreservesTimestamp()
    {
        var house = Sample();
        var store = new MemoryStore(new([house], []));
        var clock = new MutableClock(Now);
        var backend = new MockNeighborhoodBackend(store, clock);
        var events = 0;
        backend.Changed += (_, _) => events++;
        await backend.SetStampCollectedAsync(house.Id, true);
        Assert.Equal(new PassportEntry(house.Id, Now), Assert.Single((await backend.GetSnapshotAsync()).Passport));
        clock.Now = Now.AddHours(1);
        await backend.SetStampCollectedAsync(house.Id, true);
        Assert.Equal(Now, Assert.Single((await backend.GetSnapshotAsync()).Passport).CollectedAt);
        Assert.Equal(1, store.Writes);
        Assert.Equal(1, events);
        await backend.SetStampCollectedAsync(house.Id, false);
        Assert.Empty((await backend.GetSnapshotAsync()).Passport);
        await backend.SetStampCollectedAsync(house.Id, false);
        Assert.Equal(2, store.Writes);
        Assert.Equal(2, events);
        await backend.SetStampCollectedAsync(house.Id, true);
        Assert.Equal(clock.Now, Assert.Single((await backend.GetSnapshotAsync()).Passport).CollectedAt);
    }

    [Fact]
    public async Task ConcurrentCollectionOfSameHousePersistsAndNotifiesOnce()
    {
        var house = Sample();
        var store = new MemoryStore(new([house], []));
        var backend = new MockNeighborhoodBackend(store, new MutableClock(Now));
        var events = 0;
        backend.Changed += (_, _) => Interlocked.Increment(ref events);
        await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => backend.SetStampCollectedAsync(house.Id, true)));
        Assert.Equal(new PassportEntry(house.Id, Now), Assert.Single((await backend.GetSnapshotAsync()).Passport));
        Assert.Equal(1, store.Writes);
        Assert.Equal(1, events);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnknownHouseFailsExplicitlyWithoutWritingOrNotifying(bool collected)
    {
        var store = new MemoryStore(new([Sample()], []));
        var backend = new MockNeighborhoodBackend(store, new MutableClock(Now));
        var events = 0;
        backend.Changed += (_, _) => events++;
        await Assert.ThrowsAsync<ArgumentException>(() => backend.SetStampCollectedAsync(Guid.NewGuid(), collected));
        Assert.Equal(0, store.Writes);
        Assert.Equal(0, events);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedCollectionOrRemovalDoesNotPublishChanges(bool collect)
    {
        var house = Sample();
        var original = new PassportEntry(house.Id, Now);
        var store = new MemoryStore(new([house], collect ? [] : [original])) { FailWrites = true };
        var backend = new MockNeighborhoodBackend(store, new MutableClock(Now));
        var events = 0;
        backend.Changed += (_, _) => events++;
        await Assert.ThrowsAsync<IOException>(() => backend.SetStampCollectedAsync(house.Id, collect));
        var snapshot = await backend.GetSnapshotAsync();
        Assert.Equal(!collect, snapshot.Passport.Any(entry => entry.HouseId == house.Id));
        Assert.Equal(0, events);
        store.FailWrites = false;
        await backend.SetStampCollectedAsync(house.Id, collect);
        Assert.Equal(collect, (await backend.GetSnapshotAsync()).Passport.Any(entry => entry.HouseId == house.Id));
        Assert.Equal(1, events);
    }

    [Theory]
    [InlineData(0, false, false, false)]
    [InlineData(1, true, false, false)]
    [InlineData(2, true, false, true)]
    [InlineData(3, true, true, true)]
    public void AchievementsReflectCurrentCollectedHouses(int count, bool first, bool explorer, bool quiet)
    {
        House[] houses = [Sample(), Sample(ScareLevel.Spooky), Sample(ScareLevel.Scary, false)];
        var passport = houses.Take(count).Select(house => new PassportEntry(house.Id, null));
        var results = PassportAchievements.Evaluate(houses, passport);
        Assert.Equal(first, results.Single(result => result.Kind == PassportAchievementKind.FirstKnock).IsUnlocked);
        Assert.Equal(explorer, results.Single(result => result.Kind == PassportAchievementKind.SpookyExplorer).IsUnlocked);
        Assert.Equal(quiet, results.Single(result => result.Kind == PassportAchievementKind.QuietNight).IsUnlocked);
        Assert.All(results, result =>
        {
            Assert.NotEmpty(result.Title);
            Assert.NotEmpty(result.Description);
        });
    }

    [Fact]
    public void DuplicateOrUnknownEntriesDoNotInflateAchievements()
    {
        var house = Sample();
        PassportEntry[] passport = [new(house.Id, Now), new(house.Id, null), new(Guid.NewGuid(), Now)];
        var results = PassportAchievements.Evaluate([house, house], passport);
        Assert.True(results.Single(result => result.Kind == PassportAchievementKind.FirstKnock).IsUnlocked);
        Assert.False(results.Single(result => result.Kind == PassportAchievementKind.QuietNight).IsUnlocked);
        Assert.False(results.Single(result => result.Kind == PassportAchievementKind.SpookyExplorer).IsUnlocked);
    }

    [Fact]
    public void AchievementsRelockAfterRemovalAndRecomputeHostMetadata()
    {
        House[] houses = [Sample(), Sample(ScareLevel.Spooky), Sample(ScareLevel.Scary, false)];
        var passport = houses.Select(house => new PassportEntry(house.Id, Now)).ToList();
        Assert.All(PassportAchievements.Evaluate(houses, passport), result => Assert.True(result.IsUnlocked));
        passport.Clear();
        Assert.All(PassportAchievements.Evaluate(houses, passport), result => Assert.False(result.IsUnlocked));
        passport.AddRange(houses.Select(house => new PassportEntry(house.Id, Now)));
        Assert.False(PassportAchievements.Evaluate(houses.Select(house => house with { QuietVisits = false }), passport)
            .Single(result => result.Kind == PassportAchievementKind.QuietNight).IsUnlocked);
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class MemoryStore(DemoState state) : IDemoStateStore
    {
        public int Writes { get; private set; }
        public bool FailWrites { get; set; }
        public Task<DemoState?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DemoState?>(state);

        public async Task SaveAsync(DemoState value, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            if (FailWrites)
                throw new IOException("Simulated disk failure");
            state = value;
            Writes++;
        }
    }
}
