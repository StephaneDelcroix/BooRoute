using System.Globalization;

namespace BooRoute.Core.Localization;

public interface ILocaleStore
{
    string Read();
    void Write(string locale);
}

public sealed class LocaleSettings
{
    private readonly ILocaleStore store;
    private readonly Func<(CultureInfo Culture, CultureInfo UICulture)> readDeviceCultures;
    private CultureInfo deviceCulture = CultureInfo.InvariantCulture;
    private CultureInfo deviceUICulture = CultureInfo.InvariantCulture;

    public static IReadOnlyList<string> SupportedLocales { get; } =
        Array.AsReadOnly(new[] { "", "en-CA", "fr-CA" });

    public string SelectedLocale { get; private set; }
    public CultureInfo Culture => SelectedLocale.Length == 0
        ? deviceCulture : CultureInfo.GetCultureInfo(SelectedLocale);
    public CultureInfo UICulture => SelectedLocale.Length == 0
        ? deviceUICulture : CultureInfo.GetCultureInfo(SelectedLocale);

    public LocaleSettings(ILocaleStore store, CultureInfo deviceCulture, CultureInfo deviceUICulture)
        : this(store, () => (deviceCulture, deviceUICulture))
    {
    }

    public LocaleSettings(ILocaleStore store, Func<(CultureInfo Culture, CultureInfo UICulture)> readDeviceCultures)
    {
        this.store = store;
        this.readDeviceCultures = readDeviceCultures;
        RefreshDeviceCultures();
        SelectedLocale = store.Read();
        if (!SupportedLocales.Contains(SelectedLocale))
            throw new InvalidDataException("The saved locale is not supported.");
    }

    public void Select(string locale)
    {
        if (!SupportedLocales.Contains(locale))
            throw new ArgumentException("Choose a supported locale.", nameof(locale));
        if (locale.Length == 0)
            RefreshDeviceCultures();
        store.Write(locale);
        SelectedLocale = locale;
    }

    public void RefreshDeviceCultures() => (deviceCulture, deviceUICulture) = readDeviceCultures();
}
