using BooRoute.ViewModels;

namespace BooRoute.Core.Tests;

public sealed class HouseCardStatusTests
{
    [Theory]
    [InlineData(16, false, "Lights on · Outside hours")]
    [InlineData(18, true, "Lights on · Welcome!")]
    [InlineData(21, false, "Lights on · Outside hours")]
    public void LightsOnStatusReflectsWhetherTheHouseIsWithinHours(
        int localHour, bool expectedWelcoming, string expectedStatus)
    {
        var house = Sample(HouseStatus.LightsOn, new(17, 0), new(20, 0));
        var now = new DateTimeOffset(2026, 10, 31, localHour, 0, 0, TimeSpan.Zero);

        var card = new HouseCard(house, collected: false, localNow: now);

        Assert.Equal(expectedWelcoming, card.IsWelcoming);
        Assert.Equal(expectedStatus, card.Status);
    }

    [Theory]
    [InlineData(HouseStatus.Closed, 18, "Closed")]
    [InlineData(HouseStatus.Closed, 21, "Closed")]
    [InlineData(HouseStatus.OutOfTreats, 18, "Out of treats")]
    [InlineData(HouseStatus.OutOfTreats, 21, "Out of treats")]
    public void UnavailableStatusOverridesTheClock(
        HouseStatus status, int localHour, string expectedStatus)
    {
        var house = Sample(status, new(17, 0), new(20, 0));
        var now = new DateTimeOffset(2026, 10, 31, localHour, 0, 0, TimeSpan.Zero);

        var card = new HouseCard(house, collected: false, localNow: now);

        Assert.False(card.IsWelcoming);
        Assert.Equal(expectedStatus, card.Status);
    }

    [Theory]
    [InlineData(HouseStatus.LightsOn, 1, true, "Lights on · Welcome!")]
    [InlineData(HouseStatus.LightsOn, 3, true, "Lights on · Welcome!")]
    [InlineData(HouseStatus.LightsOn, 14, false, "Lights on · Outside hours")]
    [InlineData(HouseStatus.Closed, 3, false, "Closed")]
    [InlineData(HouseStatus.OutOfTreats, 3, false, "Out of treats")]
    public void OvernightStatusReflectsTheHoursAndAvailability(
        HouseStatus status, int hoursAfterOpening, bool expectedWelcoming, string expectedStatus)
    {
        var house = Sample(status, new(22, 0), new(2, 0));
        var opening = new DateTimeOffset(2026, 10, 31, 22, 0, 0, TimeSpan.FromHours(-4));

        var card = new HouseCard(house, collected: false, localNow: opening.AddHours(hoursAfterOpening));

        Assert.Equal(expectedWelcoming, card.IsWelcoming);
        Assert.Equal(expectedStatus, card.Status);
    }

    private static House Sample(HouseStatus status, TimeOnly opensAt, TimeOnly closesAt) =>
        new(new Guid("c49b95b2-c342-4e0e-93e5-57d16f3e59dc"), "Pumpkin Porch", "13 Make-Believe Lane",
            "Friendly pumpkins.", opensAt, closesAt, ScareLevel.Friendly, status, true, true);
}
