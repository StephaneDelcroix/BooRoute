using Avalonia.Controls.Maui.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace BooRoute.Browser;

public class AvaloniaApp : MauiAvaloniaApplication
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
