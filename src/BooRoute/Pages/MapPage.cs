using BooRoute.Core;
using BooRoute.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Map = Microsoft.Maui.Controls.Maps.Map;

namespace BooRoute.Pages;

public sealed class MapPage : ContentPage
{
    private readonly MapViewModel viewModel;
    private readonly Map map;

    public MapPage(MapViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        BackgroundColor = Ui.Midnight;
        SafeAreaEdges = SafeAreaEdges.Container;
        Title = "Map";

        map = new Map(MapSpan.FromCenterAndRadius(
            new Location(DemoMap.CenterLatitude, DemoMap.CenterLongitude), Distance.FromMeters(DemoMap.InitialRadiusMeters)))
        {
            AutomationId = "NeighborhoodMap",
            MapType = MapType.Street,
            IsShowingUser = false
        };
        SemanticProperties.SetDescription(map, "Map of the demo neighborhood houses");

        var summary = Ui.Bound(nameof(viewModel.Summary), 14, Ui.Muted);
        summary.AutomationId = "MapHouseCountLabel";
        var header = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(22, 12, 22, 10),
            Children =
            {
                Ui.Text("🗺️ The demo neighborhood", 26, bold: true),
                Ui.DemoBanner("DEMO · Real public venues · Treats and hours are simulated"),
                summary,
                Ui.Errors()
            }
        };

        var layout = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        layout.Add(header);
        layout.Add(map, 0, 1);
        Content = layout;

        viewModel.HousesChanged += (_, _) => UpdatePins();
    }

    private void UpdatePins()
    {
        map.Pins.Clear();
        foreach (var (card, latitude, longitude) in viewModel.Houses)
        {
            var pin = new Pin
            {
                Label = $"{card.Emoji} {card.Nickname}",
                Address = string.IsNullOrEmpty(card.VisitStatus) ? card.Status : $"{card.Status} · {card.VisitStatus}",
                Location = new Location(latitude, longitude),
                Type = PinType.Place
            };
            var house = card.House;
            pin.InfoWindowClicked += async (_, args) =>
            {
                args.HideInfoWindow = true;
                await viewModel.OpenHouseAsync(house);
            };
            map.Pins.Add(pin);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.RefreshAsync();
    }
}
