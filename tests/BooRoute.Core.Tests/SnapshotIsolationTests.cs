namespace BooRoute.Core.Tests;

public sealed class SnapshotIsolationTests
{
    [Fact]
    public async Task UpdatingHouseDoesNotChangeOlderSnapshot()
    {
        var house = Sample();
        var backend = CreateBackend(house);
        var before = await backend.GetSnapshotAsync();
        var updated = house with { Nickname = "Updated porch", Status = HouseStatus.OutOfTreats };

        await backend.SaveHouseAsync(updated);
        var after = await backend.GetSnapshotAsync();

        Assert.Equal(house, Assert.Single(before.Houses));
        Assert.Equal(updated, Assert.Single(after.Houses));
        Assert.Empty(before.Passport);
        Assert.Empty(after.Passport);
    }

    [Fact]
    public async Task AddingHouseDoesNotChangeOlderSnapshot()
    {
        var house = Sample();
        var backend = CreateBackend(house);
        var before = await backend.GetSnapshotAsync();
        var added = Sample();

        await backend.SaveHouseAsync(added);
        var after = await backend.GetSnapshotAsync();

        Assert.Equal(house, Assert.Single(before.Houses));
        Assert.Equal(new[] { house, added }, after.Houses);
        Assert.Empty(before.Passport);
        Assert.Empty(after.Passport);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangingPassportDoesNotChangeOlderSnapshot(bool collected)
    {
        var house = Sample();
        var other = Sample();
        var backend = CreateBackend(house, other);
        await backend.SetStampCollectedAsync(other.Id, true);
        await backend.SetStampCollectedAsync(house.Id, !collected);
        var before = await backend.GetSnapshotAsync();

        await backend.SetStampCollectedAsync(house.Id, collected);
        var after = await backend.GetSnapshotAsync();

        Assert.Equal(!collected, before.Passport.Any(entry => entry.HouseId == house.Id));
        Assert.Equal(collected, after.Passport.Any(entry => entry.HouseId == house.Id));
        Assert.Equal(collected ? 1 : 2, before.Passport.Count);
        Assert.Equal(collected ? 2 : 1, after.Passport.Count);
        Assert.Contains(before.Passport, entry => entry.HouseId == other.Id);
        Assert.Contains(after.Passport, entry => entry.HouseId == other.Id);
        Assert.Equal(new[] { house, other }, before.Houses);
        Assert.Equal(new[] { house, other }, after.Houses);
    }

    [Fact]
    public async Task ChangingReturnedHouseArrayDoesNotChangeBackendSnapshots()
    {
        var house = Sample();
        var backend = CreateBackend(house);
        var snapshot = await backend.GetSnapshotAsync();
        var another = await backend.GetSnapshotAsync();
        var houses = Assert.IsType<House[]>(snapshot.Houses);

        houses[0] = house with { Nickname = "Changed only in snapshot" };
        var later = await backend.GetSnapshotAsync();

        Assert.Equal(house, Assert.Single(another.Houses));
        Assert.Equal(house, Assert.Single(later.Houses));
    }

    [Fact]
    public async Task ChangingReturnedPassportArrayDoesNotChangeBackendSnapshots()
    {
        var house = Sample();
        var other = Sample();
        var backend = CreateBackend(house, other);
        await backend.SetStampCollectedAsync(house.Id, true);
        var snapshot = await backend.GetSnapshotAsync();
        var another = await backend.GetSnapshotAsync();
        var passport = Assert.IsType<PassportEntry[]>(snapshot.Passport);

        passport[0] = new(other.Id, null);
        var later = await backend.GetSnapshotAsync();

        Assert.Equal(house.Id, Assert.Single(another.Passport).HouseId);
        Assert.Equal(house.Id, Assert.Single(later.Passport).HouseId);
    }

    private static House Sample() => new(Guid.NewGuid(), "Pumpkin Porch", "13 Make-Believe Lane",
        "Friendly pumpkins.", new(17, 0), new(21, 0), ScareLevel.Friendly,
        HouseStatus.LightsOn, true, true);

    private static MockNeighborhoodBackend CreateBackend(params House[] houses) =>
        new(new MemoryStore(new(houses.ToList(), [])), TimeProvider.System);

    private sealed class MemoryStore(DemoState state) : IDemoStateStore
    {
        public Task<DemoState?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DemoState?>(state);

        public Task SaveAsync(DemoState value, CancellationToken cancellationToken = default)
        {
            state = value;
            return Task.CompletedTask;
        }
    }
}
