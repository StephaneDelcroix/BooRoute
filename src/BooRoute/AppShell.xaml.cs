using BooRoute.Core;
using BooRoute.Core.Localization;
using BooRoute.Pages;
using BooRoute.ViewModels;
using Microsoft.Extensions.Logging;

namespace BooRoute;

public partial class AppShell : Shell
{
    private readonly List<NeighborhoodViewModel> viewModels = [];
    private readonly MapViewModel? mapViewModel;
    private readonly LocaleSettings localeSettings;
    private readonly AppText text;
    private readonly ILoggerFactory logging;

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
        AppText.CultureChanged += (_, _) => RefreshLocalization();
    }

    private void RefreshLocalization()
    {
        var pages = new HashSet<Page>();
        foreach (var tab in Items.SelectMany(item => item.Items))
        {
            pages.UnionWith(tab.Navigation.NavigationStack);
            pages.UnionWith(tab.Navigation.ModalStack);
            pages.UnionWith(tab.Items.Select(content => content.Content).OfType<Page>());
        }
        foreach (var page in pages.OfType<LocalizedPage>())
            page.RefreshLocalization();
        foreach (var tab in Items.SelectMany(item => item.Items))
        {
            foreach (var content in tab.Items)
                if (content.Content is Page page)
                    content.Title = page.Title;
            tab.Title = tab.Items[0].Title;
        }
    }

    private void BuildTabs()
    {
        var tabs = new TabBar();
        foreach (var viewModel in viewModels)
        {
            var page = new NeighborhoodPage(viewModel);
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
        var mapPage = new MapPage(mapViewModel!);
        var mapTab = new Tab { Title = mapPage.Title, Route = "map", Icon = "tab_map.png" };
        mapTab.Items.Add(new ShellContent { Title = mapPage.Title, Content = mapPage });
        tabs.Items.Insert(1, mapTab);
#endif
        var settingsPage = new SettingsPage(localeSettings, text, logging, () => DeviceLocalization.Apply(localeSettings));
        var settingsTab = new Tab { Title = settingsPage.Title, Route = "settings", Icon = "tab_settings.png" };
        settingsTab.Items.Add(new ShellContent { Title = settingsPage.Title, Content = settingsPage });
        tabs.Items.Add(settingsTab);
        Items.Add(tabs);
    }

    public async Task RefreshAsync()
    {
        foreach (var viewModel in viewModels)
            await viewModel.RefreshAsync();
        if (mapViewModel is not null)
            await mapViewModel.RefreshAsync();
    }
}
