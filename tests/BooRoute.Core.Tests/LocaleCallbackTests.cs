using System.Globalization;
using BooRoute.Core.Localization;
using BooRoute.ViewModels;

namespace BooRoute.Core.Tests;

[CollectionDefinition("App locale override", DisableParallelization = true)]
public sealed class AppLocaleOverrideCollection;

[Collection("App locale override")]
public sealed class LocaleCallbackTests
{
    [Fact]
    public void ExistingCardRefreshesInPlaceWithoutChangingHouseVisitOrArtwork()
    {
        var now = new DateTimeOffset(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);
        var house = new House(Guid.NewGuid(), "Chez Zoé", "Holzmarkt 2a, 50676 Köln",
            "My own description.", new(17, 0), new(21, 0), ScareLevel.Friendly, HouseStatus.LightsOn, true, true);
        try
        {
            AppText.UseCultures(CultureInfo.GetCultureInfo("en-CA"), CultureInfo.GetCultureInfo("en-CA"));
            var card = new HouseCard(house, true, now);
            var art = card.ScareArt;
            var changes = 0;
            card.PropertyChanged += (_, _) => changes++;
            AppText.UseCultures(CultureInfo.GetCultureInfo("fr-CA"), CultureInfo.GetCultureInfo("fr-CA"));
            card.RefreshLocalization();

            Assert.Same(house, card.House);
            Assert.True(card.Visited);
            Assert.Equal(art, card.ScareArt);
            Assert.Equal("✓ Visité", card.VisitStatus);
            Assert.Equal("Voir les détails", card.ViewButtonText);
            Assert.Equal(1, changes);
        }
        finally
        {
            AppText.UseCultures(null, null);
        }
    }

    [Fact]
    public void CardRefreshPicksUpChangedHourCycleEvenWhenCultureNameIsUnchanged()
    {
        var pattern = "h:mm tt";
        var text = new AppText(AppText.Default.Strings)
        {
            FormatTime = time => time.ToString(pattern, CultureInfo.GetCultureInfo("en-CA"))
        };
        var house = new House(Guid.NewGuid(), "Venue", "Cologne", "Demo only.",
            new(17, 15), new(21, 45), ScareLevel.Friendly, HouseStatus.LightsOn, false, false);
        var card = new HouseCard(house, false, DateTimeOffset.UtcNow, text);
        Assert.Equal("5:15 PM – 9:45 PM", card.Hours);
        pattern = "HH:mm";
        card.RefreshLocalization();
        Assert.Equal("17:15 – 21:45", card.Hours);
    }

    [Fact]
    public async Task ExistingCallbackUsesSelectedLanguageInsteadOfCapturedAmbientCulture()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            var french = CultureInfo.GetCultureInfo("fr-CA");
            AppText.UseCultures(french, french);
            await Task.Run(() =>
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                Assert.Equal("Explorer", AppText.Default.Strings.Explore);
                Assert.Equal("1 maison · 1 accueille en ce moment", AppText.Default.Summary(1, 1));
                Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
                Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
            });
        }
        finally
        {
            AppText.UseCultures(null, null);
        }
        Assert.Equal(original, CultureInfo.CurrentUICulture);
    }

    [Fact]
    public void SelectedLocaleInvalidatesCardsEvenWhenCallbackStillHasOldCulture()
    {
        var now = new DateTimeOffset(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);
        var house = new House(Guid.NewGuid(), "Venue", "Holzmarkt 2a, 50676 Köln",
            "Demo only.", new(17, 0), new(21, 0), ScareLevel.Friendly, HouseStatus.LightsOn, true, true);
        var cards = new HouseCardCollection();
        try
        {
            var english = CultureInfo.GetCultureInfo("en-CA");
            AppText.UseCultures(english, english);
            cards.Update([house], new HashSet<Guid> { house.Id }, now);
            var previous = Assert.Single(cards);
            var french = CultureInfo.GetCultureInfo("fr-CA");
            AppText.UseCultures(french, french);
            cards.Update([house], new HashSet<Guid> { house.Id }, now);
            Assert.NotSame(previous, Assert.Single(cards));
            Assert.Equal("✓ Visité", cards[0].VisitStatus);
            Assert.Equal(house, cards[0].House);
        }
        finally
        {
            AppText.UseCultures(null, null);
        }
    }
}
