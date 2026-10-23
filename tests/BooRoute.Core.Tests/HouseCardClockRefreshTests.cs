using System.Collections.Specialized;
using BooRoute.Core;
using BooRoute.ViewModels;

namespace BooRoute.Core.Tests;

public sealed class HouseCardClockRefreshTests
{
    [Theory]
    [InlineData(true, HouseStatus.Closed)]
    [InlineData(true, HouseStatus.OutOfTreats)]
    [InlineData(false, HouseStatus.Closed)]
    [InlineData(false, HouseStatus.OutOfTreats)]
    public void ClockRefreshOnlyReplacesTheCardWhoseWelcomingStateChanges(
        bool opening, HouseStatus inactiveStatus)
    {
        House[] houses =
        [
            House(1, new(18, 0), new(20, 0), inactiveStatus),
            House(2, new(18, 0), new(20, 0)),
            House(3, new(17, 0), new(22, 0)),
            House(4, new(21, 0), new(23, 0))
        ];
        var before = new DateTimeOffset(2026, 10, 31, opening ? 17 : 19, 59, 0, TimeSpan.Zero);
        var cards = new HouseCardCollection();
        HashSet<Guid> visited = [];
        cards.Update(houses, visited, before);
        var original = cards.ToArray();
        var notifications = Observe(cards);
        var inactiveDisplay = inactiveStatus == HouseStatus.Closed ? "Closed" : "Out of treats";

        Assert.False(original[0].IsWelcoming);
        Assert.Equal(inactiveDisplay, original[0].Status);
        Assert.Equal(!opening, original[1].IsWelcoming);
        Assert.True(original[2].IsWelcoming);
        Assert.False(original[3].IsWelcoming);

        cards.Update(houses, visited, before.AddMinutes(1));

        AssertSelectiveReplacements(houses, original, cards, notifications, 1);
        Assert.Equal(opening, cards[1].IsWelcoming);
        Assert.Equal(opening ? "Lights on · Welcome!" : "Lights on · Outside hours", cards[1].Status);
        Assert.Equal(!opening, original[1].IsWelcoming);
        Assert.Equal(opening ? "Lights on · Outside hours" : "Lights on · Welcome!", original[1].Status);
        Assert.False(cards[0].IsWelcoming);
        Assert.Equal(inactiveDisplay, cards[0].Status);
        Assert.True(cards[2].IsWelcoming);
        Assert.Equal("Lights on · Welcome!", cards[2].Status);
        Assert.False(cards[3].IsWelcoming);
        Assert.Equal("Lights on · Outside hours", cards[3].Status);
    }

    [Fact]
    public void ClockRefreshReplacesEachChangedCardAtItsOriginalIndex()
    {
        House[] houses =
        [
            House(1, new(18, 0), new(22, 0)),
            House(2, new(17, 0), new(22, 0)),
            House(3, new(17, 0), new(18, 0), HouseStatus.OutOfTreats),
            House(4, new(17, 0), new(18, 0))
        ];
        var before = new DateTimeOffset(2026, 10, 31, 17, 59, 0, TimeSpan.Zero);
        var cards = new HouseCardCollection();
        HashSet<Guid> visited = [];
        cards.Update(houses, visited, before);
        var original = cards.ToArray();
        var notifications = Observe(cards);

        cards.Update(houses, visited, before.AddMinutes(1));

        AssertSelectiveReplacements(houses, original, cards, notifications, 0, 3);
        Assert.False(original[0].IsWelcoming);
        Assert.Equal("Lights on · Outside hours", original[0].Status);
        Assert.True(cards[0].IsWelcoming);
        Assert.Equal("Lights on · Welcome!", cards[0].Status);
        Assert.True(original[3].IsWelcoming);
        Assert.Equal("Lights on · Welcome!", original[3].Status);
        Assert.False(cards[3].IsWelcoming);
        Assert.Equal("Lights on · Outside hours", cards[3].Status);
        Assert.True(cards[1].IsWelcoming);
        Assert.Equal("Lights on · Welcome!", cards[1].Status);
        Assert.False(cards[2].IsWelcoming);
        Assert.Equal("Out of treats", cards[2].Status);
    }

    [Fact]
    public void OvernightRefreshPreservesCardsAtMidnightAndOnlyReplacesTheClosingCard()
    {
        House[] houses =
        [
            House(1, new(22, 0), new(1, 0)),
            House(2, new(21, 0), new(3, 0)),
            House(3, new(22, 0), new(1, 0), HouseStatus.Closed)
        ];
        var before = new DateTimeOffset(2026, 10, 31, 23, 59, 0, TimeSpan.Zero);
        var cards = new HouseCardCollection();
        HashSet<Guid> visited = [];
        cards.Update(houses, visited, before);
        var original = cards.ToArray();
        var notifications = Observe(cards);

        cards.Update(houses, visited, before.AddMinutes(1));

        AssertSelectiveReplacements(houses, original, cards, notifications);
        Assert.True(cards[0].IsWelcoming);
        Assert.True(cards[1].IsWelcoming);
        Assert.False(cards[2].IsWelcoming);

        cards.Update(houses, visited, new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero));

        AssertSelectiveReplacements(houses, original, cards, notifications, 0);
        Assert.True(original[0].IsWelcoming);
        Assert.Equal("Lights on · Welcome!", original[0].Status);
        Assert.False(cards[0].IsWelcoming);
        Assert.Equal("Lights on · Outside hours", cards[0].Status);
        Assert.True(cards[1].IsWelcoming);
        Assert.Equal("Lights on · Welcome!", cards[1].Status);
        Assert.False(cards[2].IsWelcoming);
        Assert.Equal("Closed", cards[2].Status);
    }

    private static List<NotifyCollectionChangedEventArgs> Observe(HouseCardCollection cards)
    {
        var notifications = new List<NotifyCollectionChangedEventArgs>();
        cards.CollectionChanged += (_, args) => notifications.Add(args);
        return notifications;
    }

    private static void AssertSelectiveReplacements(
        House[] houses, HouseCard[] original, HouseCardCollection cards,
        List<NotifyCollectionChangedEventArgs> notifications, params int[] changedIndices)
    {
        Assert.Equal(houses.Select(house => house.Id), cards.Select(card => card.House.Id));
        for (var index = 0; index < houses.Length; index++)
        {
            Assert.Same(houses[index], cards[index].House);
            Assert.False(cards[index].IsCollected);
            Assert.Empty(cards[index].PassportStatus);
            if (changedIndices.Contains(index))
                Assert.NotSame(original[index], cards[index]);
            else
                Assert.Same(original[index], cards[index]);
        }

        Assert.Equal(changedIndices.Length, notifications.Count);
        for (var index = 0; index < changedIndices.Length; index++)
        {
            var changedIndex = changedIndices[index];
            var notification = notifications[index];
            Assert.Equal(NotifyCollectionChangedAction.Replace, notification.Action);
            Assert.Equal(changedIndex, notification.OldStartingIndex);
            Assert.Equal(changedIndex, notification.NewStartingIndex);
            Assert.Same(original[changedIndex], Assert.Single(notification.OldItems!));
            Assert.Same(cards[changedIndex], Assert.Single(notification.NewItems!));
        }
    }

    private static House House(int id, TimeOnly opensAt, TimeOnly closesAt,
        HouseStatus status = HouseStatus.LightsOn) =>
        new(new Guid(id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), $"House {id}", "123 Fictional Lane",
            "A fictional porch.", opensAt, closesAt, ScareLevel.Friendly, status, true, true);
}
