using Microsoft.Extensions.Logging;
using BooRoute.Core;
using BooRoute.Core.Localization;

#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace BooRoute;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var localeSettings = DeviceLocalization.Configure();
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
#if ANDROID
            .ConfigureMauiHandlers(handlers => handlers.AddHandler<TimePicker, LocalizedTimePickerHandler>())
#endif
#if !WINDOWS
            .UseMauiMaps()
#endif
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddStronglyTypedLocalizations();
        builder.Services.AddSingleton(localeSettings);
        builder.Services.AddSingleton(provider => new AppText(provider.GetRequiredService<AppTextLocalized>())
        {
            FormatTime = DeviceLocalization.FormatTime
        });
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<IDemoStateStore>(_ =>
            new JsonDemoStateStore(Path.Combine(FileSystem.AppDataDirectory, "neighborhood.json")));
        builder.Services.AddSingleton<INeighborhoodBackend, MockNeighborhoodBackend>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.AddMauiDevFlowAgent();
#endif

        return builder.Build();
    }
}
