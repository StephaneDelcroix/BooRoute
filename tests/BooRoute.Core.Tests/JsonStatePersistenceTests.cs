namespace BooRoute.Core.Tests;

public sealed class JsonStatePersistenceTests
{
    [Theory]
    [InlineData(ScareLevel.Friendly, HouseStatus.LightsOn, true, false)]
    [InlineData(ScareLevel.Spooky, HouseStatus.OutOfTreats, false, true)]
    [InlineData(ScareLevel.Scary, HouseStatus.Closed, true, true)]
    [InlineData(ScareLevel.Friendly, HouseStatus.Closed, false, false)]
    public async Task SaveThenLoadPreservesHouseFieldsAndStamps(
        ScareLevel scareLevel, HouseStatus status, bool quietVisits, bool stepFreeAccess)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"booroute-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");
        try
        {
            var store = new JsonDemoStateStore(path);
            var house = new House(
                Guid.NewGuid(), "Pumpkin Porch", "13 Make-Believe Lane",
                "Pumpkins, \"ghosts\", and a welcome.\nBring a bag!",
                new TimeOnly(17, 23, 45).Add(TimeSpan.FromTicks(1234567)),
                new TimeOnly(21, 34, 56).Add(TimeSpan.FromTicks(7654321)),
                scareLevel, status, quietVisits, stepFreeAccess);
            var unvisitedHouse = house with { Id = Guid.NewGuid(), Nickname = "Ghost Garden" };
            var state = new DemoState([house, unvisitedHouse], [new(house.Id, null)]);

            await store.SaveAsync(state);

            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
            var loaded = await new JsonDemoStateStore(path).LoadAsync();
            Assert.NotNull(loaded);
            Assert.Equal(state.Houses, loaded.Houses);
            Assert.Equal(state.Passport, loaded.Passport);
            Assert.DoesNotContain(loaded.Passport, entry => entry.HouseId == unvisitedHouse.Id);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
            if (Directory.Exists(directory))
                Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task LaterSaveReplacesHousesAndStamps()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"booroute-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");
        try
        {
            var store = new JsonDemoStateStore(path);
            var retainedHouse = new House(
                Guid.NewGuid(), "Pumpkin Porch", "13 Make-Believe Lane",
                "Friendly pumpkins.", new(17, 0), new(21, 0),
                ScareLevel.Friendly, HouseStatus.LightsOn, true, true);
            var removedHouse = retainedHouse with
            {
                Id = Guid.NewGuid(),
                Nickname = "A much longer nickname for the house that will be removed"
            };
            await store.SaveAsync(new DemoState(
                [retainedHouse, removedHouse], [new(retainedHouse.Id, null), new(removedHouse.Id, null)]));
            Assert.False(File.Exists(path + ".tmp"));

            var initial = await new JsonDemoStateStore(path).LoadAsync();
            Assert.NotNull(initial);
            Assert.Equal(new[] { retainedHouse, removedHouse }, initial.Houses);
            Assert.Equal([retainedHouse.Id, removedHouse.Id], initial.Passport.Select(entry => entry.HouseId));

            var updatedHouse = retainedHouse with { Nickname = "Updated porch", QuietVisits = false };
            var addedHouse = retainedHouse with { Id = Guid.NewGuid(), Nickname = "New porch" };
            var replacement = new DemoState([updatedHouse, addedHouse], [new(addedHouse.Id, null)]);

            await store.SaveAsync(replacement);

            Assert.False(File.Exists(path + ".tmp"));
            var loaded = await new JsonDemoStateStore(path).LoadAsync();
            Assert.NotNull(loaded);
            Assert.Equal(replacement.Houses, loaded.Houses);
            Assert.Equal(addedHouse.Id, Assert.Single(loaded.Passport).HouseId);
            Assert.DoesNotContain(loaded.Houses, house => house.Id == removedHouse.Id);
            Assert.DoesNotContain(loaded.Passport, entry => entry.HouseId == retainedHouse.Id);
            Assert.DoesNotContain(loaded.Passport, entry => entry.HouseId == removedHouse.Id);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
            if (Directory.Exists(directory))
                Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task MissingFileLoadsAsNullWithoutCreatingDataFile()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"booroute-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");
        try
        {
            var store = new JsonDemoStateStore(path);

            var loaded = await store.LoadAsync();

            Assert.Null(loaded);
            Assert.False(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
            if (Directory.Exists(directory))
                Directory.Delete(directory);
        }
    }
}
