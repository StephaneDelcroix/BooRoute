using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BooRoute.Core.Localization;

public sealed class AppText
{
    private sealed record Cultures(CultureInfo Formatting, CultureInfo Resources);
    private static Cultures? selectedCultures;
    private static readonly ResourceManagerStringLocalizerFactory DefaultFactory =
        new(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);

    // Domain validation also runs outside DI (including when loading saved data).
    public static AppText Default { get; } = new(new(new StringLocalizer<AppText>(DefaultFactory)));

    public AppText(AppTextLocalized strings) =>
        Strings = new(new CultureLocalizer(strings.Localizer));

    public AppTextLocalized Strings { get; }
    public static CultureInfo FormattingCulture => selectedCultures?.Formatting ?? CultureInfo.CurrentCulture;
    public static CultureInfo ResourceCulture => selectedCultures?.Resources ?? CultureInfo.CurrentUICulture;
    public static event EventHandler? CultureChanged;

    public static void UseCultures(CultureInfo? formatting, CultureInfo? resources)
    {
        selectedCultures = formatting is null && resources is null ? null :
            new(formatting ?? throw new ArgumentNullException(nameof(formatting)),
                resources ?? throw new ArgumentNullException(nameof(resources)));
        CultureChanged?.Invoke(null, EventArgs.Empty);
    }

    public Func<TimeOnly, string> FormatTime { get; init; } =
        static time => time.ToString("t", FormattingCulture);

    public string Hours(TimeOnly opens, TimeOnly closes) =>
        Strings.HoursFormat(FormatTime(opens), FormatTime(closes));

    public string Summary(int houses, int welcoming) => Strings.SummaryFormat(
        houses switch
        {
            0 => Strings.HouseCountNone,
            1 => Strings.HouseCountOne,
            _ => Strings.HouseCountManyFormat(houses)
        },
        welcoming switch
        {
            0 => Strings.WelcomingCountNone,
            1 => Strings.WelcomingCountOne,
            _ => Strings.WelcomingCountManyFormat(welcoming)
        });

    public string ScareName(ScareLevel level) => level switch
    {
        ScareLevel.Friendly => Strings.ScareFriendly,
        ScareLevel.Spooky => Strings.ScareSpooky,
        ScareLevel.Scary => Strings.ScareScary,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };

    public string StatusName(HouseStatus status) => status switch
    {
        HouseStatus.LightsOn => Strings.LightsOn,
        HouseStatus.OutOfTreats => Strings.OutOfTreats,
        HouseStatus.Closed => Strings.Closed,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    // Native callbacks and existing async operations can retain the previous ambient culture.
    private sealed class CultureLocalizer(IStringLocalizer inner) : IStringLocalizer<AppText>
    {
        public LocalizedString this[string name]
        {
            get
            {
                using var scope = new CultureScope();
                return inner[name];
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                using var scope = new CultureScope();
                return inner[name, arguments];
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            using var scope = new CultureScope();
            return inner.GetAllStrings(includeParentCultures).ToArray();
        }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo formatting = CultureInfo.CurrentCulture;
        private readonly CultureInfo resources = CultureInfo.CurrentUICulture;

        public CultureScope()
        {
            if (selectedCultures is { } cultures)
            {
                CultureInfo.CurrentCulture = cultures.Formatting;
                CultureInfo.CurrentUICulture = cultures.Resources;
            }
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = formatting;
            CultureInfo.CurrentUICulture = resources;
        }
    }
}
