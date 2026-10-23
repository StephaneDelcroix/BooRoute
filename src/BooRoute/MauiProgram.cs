using Microsoft.Extensions.Logging;
using BooRoute.Core;
#if !IOS && !MACCATALYST && !ANDROID && !WINDOWS
using Avalonia.Controls.Maui;
using Avalonia.Controls.Maui.Essentials;
using Avalonia.Controls.Maui.SkiaSharp.Views;
#endif

#if DEBUG && (IOS || MACCATALYST || ANDROID || WINDOWS)
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace BooRoute;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
#if IOS || MACCATALYST || ANDROID
            .UseMauiMaps()
#endif
#if !IOS && !MACCATALYST && !ANDROID && !WINDOWS
            .UseAvaloniaApp(useSingleViewLifetime: OperatingSystem.IsBrowser())
            .UseAvaloniaEssentials()
            .UseAvaloniaSkiaSharp(forceSoftwareRendering: OperatingSystem.IsBrowser())
#endif
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        // The browser's file system is in memory, so keep browser data in localStorage through Preferences.
        builder.Services.AddSingleton<IDemoStateStore>(_ => OperatingSystem.IsBrowser()
            ? new TextDemoStateStore(() => Preferences.Get("neighborhood", null), json => Preferences.Set("neighborhood", json))
            : new JsonDemoStateStore(Path.Combine(FileSystem.AppDataDirectory, "neighborhood.json")));
        builder.Services.AddSingleton<INeighborhoodBackend, MockNeighborhoodBackend>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
#if DEBUG && (IOS || MACCATALYST || ANDROID || WINDOWS)
        builder.AddMauiDevFlowAgent();
#endif

        return builder.Build();
    }
}
