namespace BooRoute.Core.Tests;

public sealed class HouseFilterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultFiltersIncludeAllHousesInInputOrder()
    {
        var houses = Houses();

        Assert.Equal(houses, HouseFilters.Apply(houses, Now));
    }

    [Theory]
    [InlineData(ScareLevel.Friendly, 1, 3)]
    [InlineData(ScareLevel.Spooky, 2, 5)]
    [InlineData(ScareLevel.Scary, 0, 4)]
    public void ScareLevelMatchesExactlyInInputOrder(ScareLevel scareLevel, int first, int second)
    {
        var houses = Houses();

        Assert.Equal(new[] { houses[first], houses[second] },
            HouseFilters.Apply(houses, Now, scareLevel: scareLevel));
    }

    [Fact]
    public void QuietOnlyIncludesOnlyQuietHousesInInputOrder()
    {
        var houses = Houses();

        Assert.Equal(new[] { houses[0], houses[2], houses[3] },
            HouseFilters.Apply(houses, Now, quietOnly: true));
    }

    [Fact]
    public void CollectedOnlyIncludesMatchingHousesInInputOrderAndIgnoresAbsentIds()
    {
        var houses = Houses();
        var collected = new HashSet<Guid> { houses[4].Id, HouseId(99), houses[1].Id };

        Assert.Equal(new[] { houses[1], houses[4] },
            HouseFilters.Apply(houses, Now, collectedOnly: collected));
        Assert.Equal(houses, HouseFilters.Apply(houses, Now));
    }

    private static House[] Houses() =>
    [
        CreateHouse(4, ScareLevel.Scary, true),
        CreateHouse(2, ScareLevel.Friendly, false, HouseStatus.Closed),
        CreateHouse(6, ScareLevel.Spooky, true),
        CreateHouse(1, ScareLevel.Friendly, true),
        CreateHouse(5, ScareLevel.Scary, false, HouseStatus.OutOfTreats),
        CreateHouse(3, ScareLevel.Spooky, false)
    ];

    private static House CreateHouse(int id, ScareLevel scareLevel, bool quietVisits,
        HouseStatus status = HouseStatus.LightsOn) =>
        new(HouseId(id), $"Pumpkin Porch {id}", $"{id} Make-Believe Lane",
            "Friendly pumpkins.", new(17, 0), new(21, 0), scareLevel, status, quietVisits, true);

    private static Guid HouseId(int id) => Guid.Parse($"00000000-0000-0000-0000-{id:D12}");
}
