using BooRoute.Core;
using BooRoute.Core.Localization;
using BooRoute.Pages;
using BooRoute.ViewModels;
using Microsoft.Extensions.Logging;

namespace BooRoute;

public partial class AppShell : Shell
{
    private readonly List<NeighborhoodViewModel> viewModels = [];
    private readonly List<NeighborhoodPage> neighborhoodPages = [];
    private readonly LocaleSettings localeSettings;
    private readonly AppText text;
    private readonly ILoggerFactory logging;
#if !WINDOWS
    private readonly MapViewModel mapViewModel;
    private MapPage? mapPage;
#endif

    public AppShell(INeighborhoodBackend backend, TimeProvider clock, ILoggerFactory logging, AppText text, LocaleSettings localeSettings)
    {
        InitializeComponent();
        this.localeSettings = localeSettings;
        this.text = text;
        this.logging = logging;
        foreach (var mode in Enum.GetValues<NeighborhoodMode>())
            viewModels.Add(new NeighborhoodViewModel(backend, clock, logging, text, mode));
#if !WINDOWS
        // .NET MAUI Maps has no Windows implementation.
        mapViewModel = new MapViewModel(backend, clock, logging, text);
#endif
        BuildTabs();
    }

    private void BuildTabs(bool selectSettings = false)
    {
        foreach (var oldPage in neighborhoodPages)
            oldPage.StopUpdates();
        neighborhoodPages.Clear();
#if !WINDOWS
        mapPage?.StopUpdates();
#endif
        Items.Clear();
        var tabs = new TabBar();
        foreach (var viewModel in viewModels)
        {
            var page = new NeighborhoodPage(viewModel);
            neighborhoodPages.Add(page);
            var icon = viewModel.Mode switch
            {
                NeighborhoodMode.Explore => "tab_explore.png",
                NeighborhoodMode.Visited => "tab_visited.png",
                NeighborhoodMode.Host => "tab_host.png",
                _ => throw new ArgumentOutOfRangeException(nameof(viewModel.Mode), viewModel.Mode, "Unknown neighborhood mode.")
            };
            var tab = new Tab
            {
                Title = page.Title,
                Route = viewModel.Mode.ToString().ToLowerInvariant(),
                Icon = icon
            };
            tab.Items.Add(new ShellContent { Title = page.Title, Content = page });
            tabs.Items.Add(tab);
        }
#if !WINDOWS
        mapPage = new MapPage(mapViewModel);
        var mapTab = new Tab { Title = mapPage.Title, Route = "map", Icon = "tab_map.png" };
        mapTab.Items.Add(new ShellContent { Title = mapPage.Title, Content = mapPage });
        tabs.Items.Insert(1, mapTab);
#endif
        var settingsPage = new SettingsPage(localeSettings, text, logging, () =>
        {
            DeviceLocalization.Apply(localeSettings);
            BuildTabs(selectSettings: true);
        });
        var settingsTab = new Tab { Title = settingsPage.Title, Route = "settings", Icon = "tab_settings.png" };
        settingsTab.Items.Add(new ShellContent { Title = settingsPage.Title, Content = settingsPage });
        tabs.Items.Add(settingsTab);
        Items.Add(tabs);
        if (selectSettings)
            tabs.CurrentItem = settingsTab;
    }

    public async Task RefreshAsync()
    {
        foreach (var viewModel in viewModels)
            await viewModel.RefreshAsync();
#if !WINDOWS
        await mapViewModel.RefreshAsync();
#endif
    }
}
