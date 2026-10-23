using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text;
using BooRoute.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using BooRoute.ViewModels;

namespace BooRoute.Core.Tests;

public sealed class LocalizationTests
{
    [Theory]
    [InlineData("en-CA", "Explore", "Refresh neighbourhood")]
    [InlineData("fr-CA", "Explorer", "Actualiser le voisinage")]
    [InlineData("fr-FR", "Explorer", "Actualiser le voisinage")]
    [InlineData("de-DE", "Explore", "Refresh neighbourhood")]
    public void GeneratedLocalizerUsesLanguageFallback(string culture, string explore, string refresh)
    {
        using var scope = new CultureScope(culture);
        using var services = new ServiceCollection().AddLogging().AddStronglyTypedLocalizations()
            .BuildServiceProvider();
        var text = services.GetRequiredService<AppTextLocalized>();

        Assert.Equal(explore, text.Explore);
        Assert.Equal(refresh, text.RefreshNeighbourhood);
        Assert.Equal(explore, AppText.Default.Strings.Explore);
    }

    [Fact]
    public void FrenchResourcesHaveEveryKeyAndMatchingFormatArguments()
    {
        var manager = new ResourceManager(typeof(AppText));
        var english = ReadResources(manager, CultureInfo.InvariantCulture);
        var french = ReadResources(manager, CultureInfo.GetCultureInfo("fr"));

        Assert.NotEmpty(english);
        Assert.Equal(english.Keys.Order(), french.Keys.Order());
        foreach (var (key, value) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(french[key]), key);
            Assert.Equal(CompositeFormat.Parse(value).MinimumArgumentCount,
                CompositeFormat.Parse(french[key]).MinimumArgumentCount);
        }
    }

    [Theory]
    [InlineData("en-CA")]
    [InlineData("fr-CA")]
    public void EveryGeneratedAccessorResolvesAndFormats(string culture)
    {
        using var scope = new CultureScope(culture);
        var text = AppText.Default.Strings;
        foreach (var property in typeof(AppTextLocalized).GetProperties()
                     .Where(property => property.PropertyType == typeof(string)))
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(text)), property.Name);
            Assert.False(text.Localizer[property.Name].ResourceNotFound, property.Name);
        }
        foreach (var method in typeof(AppTextLocalized).GetMethods().Where(method => method.Name.EndsWith("Format")))
        {
            var arguments = Enumerable.Repeat<object>("sample", method.GetParameters().Length).ToArray();
            var value = Assert.IsType<string>(method.Invoke(text, arguments));
            Assert.DoesNotContain("{0}", value);
            Assert.DoesNotContain("{1}", value);
        }
    }

    [Theory]
    [InlineData("en-CA", 0, 0, "No houses · None welcoming now")]
    [InlineData("en-CA", 1, 1, "1 house · 1 welcoming now")]
    [InlineData("en-CA", 10, 7, "10 houses · 7 welcoming now")]
    [InlineData("fr-CA", 0, 0, "Aucune maison · Aucune n’accueille en ce moment")]
    [InlineData("fr-CA", 1, 1, "1 maison · 1 accueille en ce moment")]
    [InlineData("fr-CA", 10, 7, "10 maisons · 7 accueillent en ce moment")]
    public void CountsHaveExplicitZeroSingularAndPluralForms(string culture, int houses, int welcoming, string expected)
    {
        using var scope = new CultureScope(culture);
        Assert.Equal(expected, AppText.Default.Summary(houses, welcoming));
    }

    [Theory]
    [InlineData("en-CA", "Friendly", "Out of treats")]
    [InlineData("fr-CA", "Amical", "Plus de friandises")]
    public void DisplayLabelsDoNotDependOnEnumNames(string culture, string scare, string status)
    {
        using var scope = new CultureScope(culture);
        Assert.Equal(scare, AppText.Default.ScareName(ScareLevel.Friendly));
        Assert.Equal(status, AppText.Default.StatusName(HouseStatus.OutOfTreats));
    }

    [Theory]
    [InlineData("en-CA", "Passport", "Collect passport stamp", "Previously collected", "Lavender · Ghost · Sparkles", "First Knock")]
    [InlineData("fr-CA", "Passeport", "Collectionner le timbre du passeport", "Collectionné auparavant", "Lavande · Fantôme · Étincelles", "Premier coup à la porte")]
    public void PassportLabelsDesignDescriptionsAndAchievementsFollowLocale(
        string culture, string passport, string collect, string legacy, string design, string firstKnock)
    {
        using var scope = new CultureScope(culture);
        var text = AppText.Default;
        Assert.Equal(passport, text.Strings.Passport);
        Assert.Equal(collect, text.Strings.CollectStamp);
        Assert.Equal(legacy, text.Strings.PreviouslyCollected);
        Assert.Equal(design, text.StampDescription(new(StampSymbol.Ghost, StampPalette.Lavender, StampAccent.Sparkles)));
        var house = Sample();
        var achievement = PassportAchievements.Evaluate([house], [new(house.Id, null)])
            .Single(item => item.Kind == PassportAchievementKind.FirstKnock);
        Assert.True(achievement.IsUnlocked);
        Assert.Equal(firstKnock, achievement.Title);
    }

    [Theory]
    [InlineData("en-CA")]
    [InlineData("fr-CA")]
    public void HoursUseRegionalShortTimeFormat(string culture)
    {
        using var scope = new CultureScope(culture);
        var opens = new TimeOnly(17, 15);
        var closes = new TimeOnly(21, 45);

        Assert.Equal($"{opens.ToString("t", CultureInfo.CurrentCulture)} – {closes.ToString("t", CultureInfo.CurrentCulture)}",
            AppText.Default.Hours(opens, closes));
    }

    [Theory]
    [InlineData("h:mm tt", "5:15 PM – 9:45 PM")]
    [InlineData("HH:mm", "17:15 – 21:45")]
    public void NativeHourCycleOverrideIsUsedForBothTimes(string pattern, string expected)
    {
        var text = new AppText(AppText.Default.Strings)
        {
            FormatTime = time => time.ToString(pattern, CultureInfo.GetCultureInfo("en-CA"))
        };

        Assert.Equal(expected, text.Hours(new(17, 15), new(21, 45)));
    }

    [Fact]
    public void LocaleChangeRefreshesCachedCardsWithoutChangingHousesOrStamps()
    {
        var house = Sample();
        var now = new DateTimeOffset(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);
        var cards = new HouseCardCollection();
        HouseCard english;
        using (new CultureScope("en-CA"))
        {
            cards.Update([house], new HashSet<Guid> { house.Id }, now);
            english = Assert.Single(cards);
        }
        using (new CultureScope("fr-CA"))
        {
            cards.Update([house], new HashSet<Guid> { house.Id }, now);
            var french = Assert.Single(cards);
            Assert.NotSame(english, french);
            Assert.Equal(house, french.House);
            Assert.True(french.IsCollected);
            Assert.Equal("Timbre du passeport collectionné", french.PassportStatus);
            Assert.Equal(english.Stamp, french.Stamp);
            Assert.Contains("Amical", french.Features);
            Assert.Equal(AppText.Default.Hours(house.OpensAt, house.ClosesAt), french.Hours);
            cards.Update([house], new HashSet<Guid> { house.Id }, now);
            Assert.Same(french, Assert.Single(cards));
        }
    }

    [Fact]
    public void ValidationUsesCurrentLanguageAndAcceptsAccentedFictionalAddresses()
    {
        using var scope = new CultureScope("fr-CA");
        var house = Sample();
        Assert.Empty(house.Validate());
        Assert.Equal("Donnez un surnom à votre maison.",
            Assert.Single((house with { Nickname = "" }).Validate()));
        Assert.Equal("Entrez un lieu public ou une adresse fictive.",
            Assert.Single((house with { Address = "" }).Validate()));
        Assert.Equal("Les heures d’ouverture et de fermeture doivent être différentes.",
            Assert.Single((house with { ClosesAt = house.OpensAt }).Validate()));
    }

    [Theory]
    [InlineData("en-CA", "not a registered Halloween host")]
    [InlineData("fr-CA", "n’est pas un hôte d’Halloween inscrit")]
    public void NewDemoDataKeepsRealVenueNamesAndTranslatesDisclaimer(string culture, string disclaimer)
    {
        using var scope = new CultureScope(culture);
        var state = DemoSeed.Create(new(2026, 10, 31, 18, 0, 0, TimeSpan.FromHours(-4)));

        Assert.Equal("Microsoft Cologne", state.Houses[0].Nickname);
        Assert.Equal("Holzmarkt 2a, 50676 Köln", state.Houses[0].Address);
        Assert.Contains(disclaimer, state.Houses[0].Description);
        Assert.All(state.Houses, house => Assert.Empty(house.Validate()));
        Assert.Equal(7, state.Houses.Count(house => house.IsWelcoming(new(2026, 10, 31, 18, 0, 0, TimeSpan.FromHours(-4)))));
    }

    [Theory]
    [InlineData("en-CA", "fr-CA")]
    [InlineData("fr-CA", "en-CA")]
    public async Task LanguageChangesPreserveSavedHousesStampsAndUserText(string firstLanguage, string nextLanguage)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"booroute-localization-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "neighborhood.json");
        try
        {
            NeighborhoodSnapshot saved;
            string json;
            var clock = new FixedClock();
            using (new CultureScope(firstLanguage))
            {
                var backend = new MockNeighborhoodBackend(new JsonDemoStateStore(path), clock);
                var initial = await backend.GetSnapshotAsync();
                var edited = Sample() with { Id = initial.Houses[0].Id };
                await backend.SaveHouseAsync(edited);
                await backend.SetStampCollectedAsync(edited.Id, true);
                saved = await backend.GetSnapshotAsync();
                json = await File.ReadAllTextAsync(path);
            }

            using (new CultureScope(nextLanguage))
            {
                var backend = new MockNeighborhoodBackend(new JsonDemoStateStore(path), clock);
                var restored = await backend.GetSnapshotAsync();

                Assert.Equal(saved.Houses, restored.Houses);
                Assert.Equal(saved.Passport, restored.Passport);
                Assert.Equal(json, await File.ReadAllTextAsync(path));
                await backend.SetStampCollectedAsync(saved.Houses[0].Id, false);
                var afterWrite = await new JsonDemoStateStore(path).LoadAsync();
                Assert.Equal(saved.Houses, afterWrite!.Houses);
                Assert.Empty(afterWrite.Passport);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static Dictionary<string, string> ReadResources(ResourceManager manager, CultureInfo culture) =>
        manager.GetResourceSet(culture, true, false)!.Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);

    private static House Sample() => new(Guid.NewGuid(), "Chez Zoé", "13, allée des Érables imaginaires",
        "Citrouilles éclairées et friandises — bienvenue !", new(17, 15), new(21, 45),
        ScareLevel.Friendly, HouseStatus.LightsOn, true, true);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 31, 22, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string name)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }
}
