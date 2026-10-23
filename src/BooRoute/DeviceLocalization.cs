using System.Globalization;
using BooRoute.Core.Localization;
using Microsoft.Maui.Handlers;

namespace BooRoute;

internal static class DeviceLocalization
{
    private static string? localeOverride;
    private static readonly (CultureInfo Culture, CultureInfo UICulture) InitialDeviceCultures =
        (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
    internal static bool HasOverride => localeOverride is not null;

    private static (CultureInfo Culture, CultureInfo UICulture) ReadDeviceCultures()
    {
#if ANDROID
        var resources = Platform.CurrentActivity?.Resources ?? Android.App.Application.Context.Resources;
        var language = resources?.Configuration?.Locales?.Get(0)?.ToLanguageTag();
        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            Android.App.Application.Context.GetSystemService(Android.Content.Context.LocaleService) is Android.App.LocaleManager manager &&
            manager.ApplicationLocales is { IsEmpty: false } locales)
            language = locales.Get(0)?.ToLanguageTag();
        var region = Java.Util.Locale.Default?.ToLanguageTag();
#elif IOS || MACCATALYST
        var language = Foundation.NSBundle.MainBundle.PreferredLocalizations.FirstOrDefault();
        var region = Foundation.NSLocale.CurrentLocale.LocaleIdentifier.Split('@')[0].Replace('_', '-');
#else
        return InitialDeviceCultures;
#endif
#if ANDROID || IOS || MACCATALYST
        return (string.IsNullOrEmpty(region) ? InitialDeviceCultures.Culture : CultureInfo.GetCultureInfo(region),
            string.IsNullOrEmpty(language) ? InitialDeviceCultures.UICulture : CultureInfo.GetCultureInfo(language));
#endif
    }

    public static LocaleSettings Configure()
    {
        var settings = new LocaleSettings(new PreferenceLocaleStore(), ReadDeviceCultures);
        Apply(settings);

        TimePickerHandler.Mapper.AppendToMapping(nameof(ITimePicker.Time), UpdateTimePicker);
        TimePickerHandler.Mapper.AppendToMapping(nameof(ITimePicker.Format), UpdateTimePicker);
        return settings;
    }

    public static void Apply(LocaleSettings settings)
    {
        settings.RefreshDeviceCultures();
        localeOverride = settings.SelectedLocale.Length == 0 ? null : settings.SelectedLocale;
        CultureInfo.CurrentCulture = settings.Culture;
        CultureInfo.CurrentUICulture = settings.UICulture;
        CultureInfo.DefaultThreadCurrentCulture = settings.Culture;
        CultureInfo.DefaultThreadCurrentUICulture = settings.UICulture;
        AppText.UseCultures(settings.Culture, settings.UICulture);
    }

    public static string FormatTime(TimeOnly time)
    {
#if ANDROID
        using var locale = Java.Util.Locale.ForLanguageTag(localeOverride ?? AppText.FormattingCulture.Name);
        var skeleton = Android.Text.Format.DateFormat.Is24HourFormat(Android.App.Application.Context) ? "Hm" : "hm";
        var pattern = Android.Text.Format.DateFormat.GetBestDateTimePattern(locale, skeleton)
            ?? throw new InvalidOperationException("Android did not provide a time pattern.");
        using var formatter = (localeOverride is null
            ? Android.Text.Format.DateFormat.GetTimeFormat(Android.App.Application.Context)
            : new Java.Text.SimpleDateFormat(pattern, locale))
            ?? throw new InvalidOperationException("Android did not provide a time formatter.");
        using var utc = Java.Util.TimeZone.GetTimeZone("UTC")
            ?? throw new InvalidOperationException("Android did not provide the UTC time zone.");
        formatter.TimeZone = utc;
        using var date = new Java.Util.Date((long)time.ToTimeSpan().TotalMilliseconds);
        return formatter.Format(date);
#elif IOS || MACCATALYST
        using var locale = PickerLocale();
        using var formatter = new Foundation.NSDateFormatter
        {
            DateStyle = Foundation.NSDateFormatterStyle.None,
            TimeStyle = Foundation.NSDateFormatterStyle.Short,
            Locale = locale,
            TimeZone = Foundation.NSTimeZone.FromName("UTC")
        };
        using var date = Foundation.NSDate.FromTimeIntervalSince1970(time.ToTimeSpan().TotalSeconds);
        return formatter.ToString(date);
#else
        return time.ToString("t", CultureInfo.CurrentCulture);
#endif
    }

#if IOS || MACCATALYST
    private static Foundation.NSLocale PickerLocale()
    {
        if (localeOverride is null)
            return new Foundation.NSLocale(Foundation.NSLocale.CurrentLocale.LocaleIdentifier);
        var devicePattern = Foundation.NSDateFormatter.GetDateFormatFromTemplate("j", 0,
            Foundation.NSLocale.CurrentLocale);
        return new Foundation.NSLocale($"{localeOverride}-u-hc-{(devicePattern.Contains('a') ? "h12" : "h23")}");
    }
#endif

    private static void UpdateTimePicker(ITimePickerHandler handler, ITimePicker picker)
    {
#if ANDROID || IOS
        // MAUI's field text uses .NET culture; the native dialog can use a user-overridden hour cycle.
        handler.PlatformView.Text = picker.Time is { } time ? FormatTime(TimeOnly.FromTimeSpan(time)) : "";
#endif
#if IOS
        if (handler.PlatformView.InputView is UIKit.UIDatePicker nativePicker)
        {
            using var locale = PickerLocale();
            nativePicker.Locale = locale;
        }
#elif MACCATALYST
        using var locale = PickerLocale();
        handler.PlatformView.Locale = locale;
#endif
    }
}
