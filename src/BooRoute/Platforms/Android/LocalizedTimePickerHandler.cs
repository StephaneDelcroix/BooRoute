using Android.App;
using Android.Content.Res;
using BooRoute.Core.Localization;
using Microsoft.Maui.Handlers;

namespace BooRoute;

internal sealed class LocalizedTimePickerHandler : TimePickerHandler
{
    protected override TimePickerDialog CreateTimePickerDialog(int hour, int minute)
    {
        if (!DeviceLocalization.HasOverride)
            return base.CreateTimePickerDialog(hour, minute);
        using var configuration = new Configuration(Context.Resources?.Configuration);
        using var locale = Java.Util.Locale.ForLanguageTag(AppText.FormattingCulture.Name);
        configuration.SetLocale(locale);
        // Keep the activity window manager and copy its theme into the localized resources.
        using var context = new Android.Views.ContextThemeWrapper(Context, 0);
        context.ApplyOverrideConfiguration(configuration);
        var dialog = new TimePickerDialog(context, (_, args) =>
        {
            VirtualView.Time = new TimeSpan(args.HourOfDay, args.Minute, 0);
        }, hour, minute, Android.Text.Format.DateFormat.Is24HourFormat(Context));
        dialog.DismissEvent += (_, _) =>
        {
            VirtualView.IsFocused = false;
            VirtualView.IsOpen = false;
        };
        return dialog;
    }
}
