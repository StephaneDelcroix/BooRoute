using BooRoute.Core;
using BooRoute.Pages;
using BooRoute.ViewModels;
using Microsoft.Extensions.Logging;

namespace BooRoute;

public partial class AppShell : Shell
{
    private readonly List<NeighborhoodViewModel> viewModels = [];

    public AppShell(INeighborhoodBackend backend, TimeProvider clock, ILoggerFactory logging)
    {
        InitializeComponent();
        var tabs = new TabBar();
        foreach (var mode in Enum.GetValues<NeighborhoodMode>())
        {
            var viewModel = new NeighborhoodViewModel(backend, clock, logging, mode);
            viewModels.Add(viewModel);
            var page = new NeighborhoodPage(viewModel);
            var tab = new Tab
            {
                Title = page.Title,
                Route = mode.ToString().ToLowerInvariant(),
                Icon = mode switch
                {
                    NeighborhoodMode.Explore => "tab_explore.png",
                    NeighborhoodMode.Visited => "tab_visited.png",
                    NeighborhoodMode.Host => "tab_host.png",
                    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown neighborhood mode.")
                }
            };
            tab.Items.Add(new ShellContent { Title = page.Title, Content = page });
            tabs.Items.Add(tab);
        }
        Items.Add(tabs);
    }

    public async Task RefreshAsync()
    {
        foreach (var viewModel in viewModels)
            await viewModel.RefreshAsync();
    }
}
