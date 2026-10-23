using System.Collections.Specialized;
using BooRoute.Core;
using BooRoute.Pages;
using BooRoute.ViewModels;

namespace BooRoute.Core.Tests;

public sealed class HouseCollectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<Guid> NoVisits = [];

    [Fact]
    public void UnchangedRefreshPreservesAllCardsAndEmitsNoNotifications()
    {
        var houses = Enumerable.Range(0, 10_000).Select(index => House($"House {index}")).ToArray();
        var cards = new HouseCardCollection();
        cards.Update(houses, NoVisits, Now);
        var original = cards.ToArray();
        var notifications = 0;
        cards.CollectionChanged += (_, _) => notifications++;

        cards.Update(houses.Select(house => house with { }), NoVisits, Now.AddSeconds(30));

        Assert.Equal(0, notifications);
        for (var index = 0; index < cards.Count; index++)
            Assert.Same(original[index], cards[index]);
    }

    [Fact]
    public void EditingOneHouseOnlyReplacesThatCard()
    {
        var first = House("First");
        var second = House("Second");
        var cards = new HouseCardCollection();
        cards.Update([first, second], NoVisits, Now);
        var unchanged = cards[0];
        var actions = Observe(cards);

        cards.Update([first, second with { Address = "A new fictional address" }], NoVisits, Now);

        Assert.Same(unchanged, cards[0]);
        Assert.Equal("A new fictional address", cards[1].Address);
        Assert.Equal([NotifyCollectionChangedAction.Replace], actions);
    }

    [Fact]
    public void StampsAndTimeBoundariesUpdateWithoutResettingTheCollection()
    {
        var house = House("Porch");
        var cards = new HouseCardCollection();
        cards.Update([house], NoVisits, Now);
        var actions = Observe(cards);

        cards.Update([house], new HashSet<Guid> { house.Id }, Now);
        Assert.Equal("Passport stamp collected", cards[0].PassportStatus);
        Assert.True(cards[0].IsWelcoming);

        cards.Update([house], NoVisits, Now.AddHours(2));
        Assert.Empty(cards[0].PassportStatus);
        Assert.False(cards[0].IsWelcoming);
        Assert.Equal("Lights on · Outside hours", cards[0].Status);
        Assert.Equal([NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Replace], actions);
    }

    [Fact]
    public void FilteringReorderingAndInsertingPreserveSurvivingCards()
    {
        var first = House("First");
        var second = House("Second");
        var third = House("Third");
        var added = House("Added");
        var cards = new HouseCardCollection();
        cards.Update([first, second, third], NoVisits, Now);
        var firstCard = cards[0];
        var thirdCard = cards[2];
        var actions = Observe(cards);

        cards.Update([third, added, first], NoVisits, Now);

        Assert.Equal([third.Id, added.Id, first.Id], cards.Select(card => card.House.Id));
        Assert.Same(thirdCard, cards[0]);
        Assert.Same(firstCard, cards[2]);
        Assert.Equal([NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Move,
            NotifyCollectionChangedAction.Add], actions);
    }

    [Fact]
    public void EmptyAndRepopulatedSourcesDoNotReset()
    {
        var cards = new HouseCardCollection();
        var actions = Observe(cards);
        cards.Update([], NoVisits, Now);
        Assert.Empty(actions);

        var house = House("Only house");
        cards.Update([house], NoVisits, Now);
        cards.Update([], NoVisits, Now);
        Assert.Empty(cards);
        cards.Update([house], NoVisits, Now);

        Assert.Single(cards);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
    }

    [Fact]
    public void DuplicateKeysAreRejectedBeforeChangingPublishedItems()
    {
        var house = House("Only house");
        var cards = new HouseCardCollection();
        cards.Update([house], NoVisits, Now);
        var original = cards[0];
        var actions = Observe(cards);

        Assert.Throws<ArgumentException>(() => cards.Update([house, house], NoVisits, Now));

        Assert.Same(original, Assert.Single(cards));
        Assert.Empty(actions);
    }

    [Fact]
    public void RandomizedSnapshotsMatchTheRequestedOrderAndContent()
    {
        var random = new Random(42);
        var houses = Enumerable.Range(0, 50).Select(index => House($"House {index}")).ToArray();
        var cards = new HouseCardCollection();
        var actions = Observe(cards);
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var next = houses.Where(_ => random.Next(2) == 0)
                .OrderBy(_ => random.Next())
                .Select(house => random.Next(3) == 0 ? house with { Status = HouseStatus.Closed } : house)
                .ToArray();
            var visited = next.Where(_ => random.Next(2) == 0).Select(house => house.Id).ToHashSet();

            cards.Update(next, visited, Now);

            Assert.Equal(next, cards.Select(card => card.House));
            Assert.All(cards, card => Assert.Equal(visited.Contains(card.House.Id), card.IsCollected));
        }
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
    }

    [Theory]
    [InlineData(320, 1)]
    [InlineData(390, 1)]
    [InlineData(653, 1)]
    [InlineData(654, 2)]
    [InlineData(724, 2)]
    [InlineData(987, 2)]
    [InlineData(988, 3)]
    [InlineData(1322, 4)]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    public void GridUsesAvailableWidthWithExactMinimumCardAndGutterSizes(double width, int expected)
    {
        var span = HouseGridLayout.GetSpan(width);
        Assert.Equal(expected, span);
        if (span > 1)
            Assert.True((width - (span - 1) * HouseGridLayout.ColumnSpacing) / span >=
                        HouseGridLayout.MinimumCardWidth);
    }

    private static List<NotifyCollectionChangedAction> Observe(HouseCardCollection cards)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        cards.CollectionChanged += (_, args) => actions.Add(args.Action);
        return actions;
    }

    private static House House(string name) => new(Guid.NewGuid(), name, "123 Fictional Lane",
        "A fictional porch.", new(17, 0), new(20, 0), ScareLevel.Friendly, HouseStatus.LightsOn, true, true);
}
