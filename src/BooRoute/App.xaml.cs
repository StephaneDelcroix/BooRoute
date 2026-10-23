namespace BooRoute;

public partial class App : Application
{
    private readonly AppShell shell;

    private readonly Core.Localization.LocaleSettings localeSettings;

    public App(AppShell shell, Core.Localization.LocaleSettings localeSettings)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
        this.shell = shell;
        this.localeSettings = localeSettings;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(shell);
        window.Activated += (_, _) => DeviceLocalization.Apply(localeSettings);
        window.Resumed += async (_, _) => await shell.RefreshAsync();
        return window;
    }
}