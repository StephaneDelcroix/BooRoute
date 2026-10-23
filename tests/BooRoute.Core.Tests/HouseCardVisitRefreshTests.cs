using System.Collections.Specialized;
using BooRoute.ViewModels;

namespace BooRoute.Core.Tests;

public sealed class HouseCardVisitRefreshTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 18, 0, 0, TimeSpan.FromHours(-4));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void ChangingOneStampReplacesOnlyThatCardAtTheSameIndex(int affectedIndex, bool wasVisited)
    {
        var houses = CreateHouses();
        var visited = houses.Where((_, index) => index != affectedIndex && index % 2 == 0)
            .Select(house => house.Id).ToHashSet();
        if (wasVisited)
            visited.Add(houses[affectedIndex].Id);
        var cards = new HouseCardCollection();
        cards.Update(houses, visited, Now);
        var original = cards.ToArray();
        var notifications = new List<NotifyCollectionChangedEventArgs>();
        cards.CollectionChanged += (_, args) => notifications.Add(args);
        var nextVisited = new HashSet<Guid>(visited);
        if (wasVisited)
            nextVisited.Remove(houses[affectedIndex].Id);
        else
            nextVisited.Add(houses[affectedIndex].Id);

        cards.Update(houses, nextVisited, Now);

        Assert.Equal(houses.Select(house => house.Id), cards.Select(card => card.House.Id));
        for (var index = 0; index < houses.Length; index++)
        {
            if (index == affectedIndex)
                Assert.NotSame(original[index], cards[index]);
            else
                Assert.Same(original[index], cards[index]);
            var expectedVisited = nextVisited.Contains(houses[index].Id);
            Assert.Equal(expectedVisited, cards[index].IsCollected);
            Assert.Equal(expectedVisited ? "Passport stamp collected" : "", cards[index].PassportStatus);
            Assert.Equal(original[index].Stamp, cards[index].Stamp);
        }
        var notification = Assert.Single(notifications);
        Assert.Equal(NotifyCollectionChangedAction.Replace, notification.Action);
        Assert.Equal(affectedIndex, notification.OldStartingIndex);
        Assert.Equal(affectedIndex, notification.NewStartingIndex);
        Assert.NotNull(notification.OldItems);
        Assert.NotNull(notification.NewItems);
        Assert.Same(original[affectedIndex], Assert.Single(notification.OldItems.Cast<HouseCard>()));
        Assert.Same(cards[affectedIndex], Assert.Single(notification.NewItems.Cast<HouseCard>()));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 5)]
    [InlineData(4, 0)]
    public void ChangingOnlyUnknownVisitsPreservesAllCardsAndEmitsNoNotifications(
        int previousUnknownId, int nextUnknownId)
    {
        var houses = CreateHouses();
        var visited = new HashSet<Guid>();
        if (previousUnknownId != 0)
            visited.Add(HouseId(previousUnknownId));
        var cards = new HouseCardCollection();
        cards.Update(houses, visited, Now);
        var original = cards.ToArray();
        var notifications = new List<NotifyCollectionChangedEventArgs>();
        cards.CollectionChanged += (_, args) => notifications.Add(args);
        var nextVisited = new HashSet<Guid>();
        if (nextUnknownId != 0)
            nextVisited.Add(HouseId(nextUnknownId));

        cards.Update(houses, nextVisited, Now);

        Assert.Equal(houses.Select(house => house.Id), cards.Select(card => card.House.Id));
        for (var index = 0; index < houses.Length; index++)
        {
            Assert.Same(original[index], cards[index]);
            Assert.False(cards[index].IsCollected);
            Assert.Empty(cards[index].PassportStatus);
        }
        Assert.Empty(notifications);
    }

    private static Guid HouseId(int index) => Guid.Parse($"10000000-0000-4000-8000-{index:D12}");

    private static House[] CreateHouses() => Enumerable.Range(1, 3)
        .Select(index => new House(HouseId(index), $"House {index}", $"{index} Fictional Lane",
            "A fictional porch.", new(17, 0), new(20, 0), ScareLevel.Friendly,
            HouseStatus.LightsOn, true, true))
        .ToArray();
}
