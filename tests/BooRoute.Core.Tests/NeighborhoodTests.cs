using System.Text.Json;

namespace BooRoute.Core.Tests;

public sealed class NeighborhoodTests
{
    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 10, 31, hour, minute, 0, TimeSpan.Zero);
    private static House Sample() => new(Guid.NewGuid(), "Pumpkin Porch", "13 Make-Believe Lane",
        "Friendly pumpkins.", new(17, 0), new(21, 0), ScareLevel.Friendly,
        HouseStatus.LightsOn, true, true);

    [Theory]
    [InlineData(16, 59, false)]
    [InlineData(17, 0, true)]
    [InlineData(20, 59, true)]
    [InlineData(21, 0, false)]
    public void WelcomingHoursHaveInclusiveOpeningAndExclusiveClosing(int hour, int minute, bool expected) =>
        Assert.Equal(expected, Sample().IsWelcoming(At(hour, minute)));

    [Theory]
    [InlineData(22, true)]
    [InlineData(23, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(12, false)]
    public void OvernightHoursCrossMidnight(int hour, bool expected)
    {
        var house = Sample() with { OpensAt = new(22, 0), ClosesAt = new(2, 0) };
        Assert.Equal(expected, house.IsWelcoming(At(hour)));
    }

    [Theory]
    [InlineData(HouseStatus.Closed)]
    [InlineData(HouseStatus.OutOfTreats)]
    public void NonWelcomingStatusOverridesHours(HouseStatus status) =>
        Assert.False((Sample() with { Status = status }).IsWelcoming(At(18)));

    [Fact]
    public void EqualHoursAreInvalidAndNeverWelcome()
    {
        var house = Sample() with { OpensAt = new(17, 0), ClosesAt = new(17, 0) };
        Assert.NotEmpty(house.Validate());
        Assert.False(house.IsWelcoming(At(17)));
    }

    [Fact]
    public void ValidationRequiresFieldsAndKnownEnumValues()
    {
        var house = Sample() with { Nickname = " ", Address = "", Description = "", ScareLevel = (ScareLevel)90 };
        Assert.Equal(4, house.Validate().Count);
        Assert.Empty(Sample().Validate());
    }

    [Fact]
    public void FiltersComposeAndVisitsStayPrivate()
    {
        var wanted = Sample();
        House[] houses =
        [
            wanted, Sample() with { QuietVisits = false },
            Sample() with { Status = HouseStatus.OutOfTreats },
            Sample() with { ScareLevel = ScareLevel.Scary }
        ];
        var result = HouseFilters.Apply(houses, At(18), true, ScareLevel.Friendly, true, new HashSet<Guid> { wanted.Id });
        Assert.Equal(wanted, Assert.Single(result));
        Assert.Empty(HouseFilters.Apply(houses, At(18), visitedOnly: new HashSet<Guid>()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(23)]
    public void SeedAlwaysIncludesTenCologneVenuesAndWelcomingStops(int hour)
    {
        var state = DemoSeed.Create(At(hour));
        Assert.Equal(10, state.Houses.Count);
        Assert.Equal(7, state.Houses.Count(house => house.IsWelcoming(At(hour))));
        Assert.All(state.Houses, house =>
        {
            Assert.EndsWith("Köln", house.Address);
            Assert.DoesNotContain("Make-Believe", house.Address);
            Assert.Contains("not a registered Halloween host", house.Description);
            Assert.Contains("simulated", house.Description);
            Assert.Empty(house.Validate());
        });
        Assert.Empty(state.Visited);
    }

    [Fact]
    public void SeedUsesPublicVenuesAroundMicrosoftCologne()
    {
        var state = DemoSeed.Create(At(18));
        (string Name, string Address)[] expected =
        [
            ("Microsoft Cologne", "Holzmarkt 2a, 50676 Köln"),
            ("art'otel Cologne", "Holzmarkt 4, 50676 Köln"),
            ("Schokoladenmuseum", "Am Schokoladenmuseum 1a, 50678 Köln"),
            ("Deutsches Sport & Olympia Museum", "Im Zollhafen 1, 50678 Köln"),
            ("Trinitatiskirche", "Filzengraben 4, 50676 Köln"),
            ("NH Köln Altstadt", "Holzmarkt 47, 50676 Köln"),
            ("Motel One Köln-Altstadt", "Tel-Aviv-Straße 6, 50676 Köln"),
            ("Wasserturm Hotel Cologne", "Kaygasse 2, 50676 Köln"),
            ("Museum Schnütgen", "Cäcilienstraße 29-33, 50667 Köln"),
            ("Rautenstrauch-Joest-Museum", "Cäcilienstraße 29-33, 50667 Köln")
        ];
        Assert.Equal(expected, state.Houses.Select(house => (house.Nickname, house.Address)));
        Assert.Equal(10, state.Houses.Select(house => house.Id).Distinct().Count());
    }

    [Fact]
    public async Task ExistingFictionalAddressesAndVisitsAreNotReplacedByNewSeed()
    {
        var original = Sample();
        var store = new MemoryStore();
        await store.SaveAsync(new([original], [original.Id]));
        var restored = await new MockNeighborhoodBackend(store, new FixedClock(At(18))).GetSnapshotAsync();
        Assert.Equal(original, Assert.Single(restored.Houses));
        Assert.Contains(original.Id, restored.Visited);
    }

    [Fact]
    public async Task HostUpdatesAndVisitsPersistAcrossBackendInstances()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"booroute-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");
        try
        {
            var store = new JsonDemoStateStore(path);
            var backend = new MockNeighborhoodBackend(store, new FixedClock(At(18)));
            var initial = await backend.GetSnapshotAsync();
            var house = initial.Houses[0] with { Status = HouseStatus.OutOfTreats, Nickname = "Updated porch" };
            var changed = 0;
            backend.Changed += (_, _) => changed++;
            await backend.SaveHouseAsync(house);
            await backend.SetVisitedAsync(house.Id, true);
            var restored = await new MockNeighborhoodBackend(new JsonDemoStateStore(path), new FixedClock(At(19))).GetSnapshotAsync();
            Assert.Equal(house, restored.Houses.Single(saved => saved.Id == house.Id));
            Assert.Contains(house.Id, restored.Visited);
            Assert.Equal(2, changed);
            Assert.Equal(10, restored.Houses.Count);
            await backend.SetVisitedAsync(house.Id, false);
            Assert.Empty((await backend.GetSnapshotAsync()).Visited);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentChangesAreNotLost()
    {
        var backend = new MockNeighborhoodBackend(new MemoryStore(), new FixedClock(At(18)));
        var houses = (await backend.GetSnapshotAsync()).Houses;
        await Task.WhenAll(houses.Select(house => backend.SetVisitedAsync(house.Id, true)));
        Assert.Equal(10, (await backend.GetSnapshotAsync()).Visited.Count);
    }

    [Fact]
    public async Task NewHouseIsValidatedAndAddedNotDuplicated()
    {
        var backend = new MockNeighborhoodBackend(new MemoryStore(), new FixedClock(At(18)));
        var house = Sample();
        await backend.SaveHouseAsync(house);
        await backend.SaveHouseAsync(house with { Description = "Updated" });
        Assert.Equal(11, (await backend.GetSnapshotAsync()).Houses.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => backend.SaveHouseAsync(house with { Nickname = "" }));
        await Assert.ThrowsAsync<ArgumentException>(() => backend.SetVisitedAsync(Guid.NewGuid(), true));
    }

    [Fact]
    public async Task FailedWriteDoesNotPublishUnpersistedChanges()
    {
        var store = new MemoryStore();
        var backend = new MockNeighborhoodBackend(store, new FixedClock(At(18)));
        var house = (await backend.GetSnapshotAsync()).Houses[0];
        var events = 0;
        backend.Changed += (_, _) => events++;
        store.FailWrites = true;
        await Assert.ThrowsAsync<IOException>(() => backend.SaveHouseAsync(house with { Nickname = "Not saved" }));
        Assert.Equal(house, (await backend.GetSnapshotAsync()).Houses[0]);
        Assert.Equal(0, events);
    }

    [Fact]
    public async Task CorruptDataIsReportedWithoutOverwritingIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"booroute-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");
        try
        {
            var store = new JsonDemoStateStore(path);
            await File.WriteAllTextAsync(path, "{ broken");
            var backend = new MockNeighborhoodBackend(store, new FixedClock(At(18)));
            await Assert.ThrowsAsync<JsonException>(() => backend.GetSnapshotAsync());
            Assert.Equal("{ broken", await File.ReadAllTextAsync(path));
            await File.WriteAllTextAsync(path, """{"Houses":null,"Visited":[]}""");
            await Assert.ThrowsAsync<InvalidDataException>(() => backend.GetSnapshotAsync());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class MemoryStore : IDemoStateStore
    {
        private DemoState? state;
        public bool FailWrites { get; set; }
        public Task<DemoState?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(state);
        public Task SaveAsync(DemoState value, CancellationToken cancellationToken = default)
        {
            if (FailWrites)
                throw new IOException("Simulated disk failure");
            state = value;
            return Task.CompletedTask;
        }
    }
}
