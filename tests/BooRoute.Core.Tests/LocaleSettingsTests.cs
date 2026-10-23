using System.Globalization;
using BooRoute.Core.Localization;

namespace BooRoute.Core.Tests;

public sealed class LocaleSettingsTests
{
    [Fact]
    public void ReturningToDeviceModeReadsCurrentCulturesInsteadOfStartupValues()
    {
        var device = (CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("en-CA"));
        var settings = new LocaleSettings(new MemoryLocaleStore(), () => device);
        settings.Select("fr-CA");
        device = (CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("fr-FR"));
        settings.Select("");

        Assert.Equal("de-DE", settings.Culture.Name);
        Assert.Equal("fr-FR", settings.UICulture.Name);
    }

    [Fact]
    public void ResumeRefreshesDeviceModeWithoutWritingPreferences()
    {
        var store = new MemoryLocaleStore();
        var device = (CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("en-CA"));
        var settings = new LocaleSettings(store, () => device);
        device = (CultureInfo.GetCultureInfo("fr-CA"), CultureInfo.GetCultureInfo("fr-CA"));
        settings.RefreshDeviceCultures();

        Assert.Equal("fr-CA", settings.Culture.Name);
        Assert.Equal("fr-CA", settings.UICulture.Name);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void DeviceRefreshDoesNotReplaceExplicitChoice()
    {
        var device = (CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("en-CA"));
        var settings = new LocaleSettings(new MemoryLocaleStore(), () => device);
        settings.Select("fr-CA");
        device = (CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("de-DE"));
        settings.RefreshDeviceCultures();

        Assert.Equal("fr-CA", settings.Culture.Name);
        Assert.Equal("fr-CA", settings.UICulture.Name);
    }

    [Fact]
    public void DeviceModePreservesSeparateLanguageAndRegionalSettings()
    {
        var settings = Create(new MemoryLocaleStore());
        Assert.Equal("", settings.SelectedLocale);
        Assert.Equal("de-DE", settings.Culture.Name);
        Assert.Equal("en-US", settings.UICulture.Name);
    }

    [Theory]
    [InlineData("en-CA")]
    [InlineData("fr-CA")]
    public void SelectionPersistsLanguageAndRegionAcrossInstances(string locale)
    {
        var store = new MemoryLocaleStore();
        Create(store).Select(locale);
        var restored = Create(store);
        Assert.Equal(locale, restored.SelectedLocale);
        Assert.Equal(locale, restored.Culture.Name);
        Assert.Equal(locale, restored.UICulture.Name);
    }

    [Fact]
    public void DeviceModeRestoresOriginalSettingsAfterAnOverride()
    {
        var store = new MemoryLocaleStore();
        var settings = Create(store);
        settings.Select("fr-CA");
        settings.Select("");
        Assert.Equal("", store.Locale);
        Assert.Equal("de-DE", settings.Culture.Name);
        Assert.Equal("en-US", settings.UICulture.Name);
    }

    [Theory]
    [InlineData("xx-not-a-locale")]
    [InlineData("de-DE")]
    public void UnsupportedSelectionsDoNotChangeTheSavedLocale(string locale)
    {
        var store = new MemoryLocaleStore { Locale = "en-CA" };
        var settings = Create(store);
        Assert.Throws<ArgumentException>(() => settings.Select(locale));
        Assert.Equal("en-CA", store.Locale);
        Assert.Equal("en-CA", settings.SelectedLocale);
    }

    [Fact]
    public void UnsupportedSavedLocaleIsReportedWithoutOverwritingIt()
    {
        var store = new MemoryLocaleStore { Locale = "unsupported" };
        Assert.Throws<InvalidDataException>(() => Create(store));
        Assert.Equal("unsupported", store.Locale);
    }

    [Fact]
    public void FailedPreferenceWriteDoesNotChangeTheActiveSelection()
    {
        var store = new MemoryLocaleStore { Locale = "en-CA", FailWrites = true };
        var settings = Create(store);
        Assert.Throws<IOException>(() => settings.Select("fr-CA"));
        Assert.Equal("en-CA", settings.SelectedLocale);
        Assert.Equal("en-CA", store.Locale);
    }

    private static LocaleSettings Create(ILocaleStore store) =>
        new(store, CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("en-US"));

    private sealed class MemoryLocaleStore : ILocaleStore
    {
        public string Locale { get; set; } = "";
        public bool FailWrites { get; set; }
        public int Writes { get; private set; }
        public string Read() => Locale;
        public void Write(string locale)
        {
            if (FailWrites)
                throw new IOException("Simulated preference write failure.");
            Writes++;
            Locale = locale;
        }
    }
}
