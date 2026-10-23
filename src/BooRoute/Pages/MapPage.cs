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
        var text = viewModel.Text.Strings;
        Title = text.Map;

        map = new Map(MapSpan.FromCenterAndRadius(
            new Location(DemoMap.CenterLatitude, DemoMap.CenterLongitude), Distance.FromMeters(DemoMap.InitialRadiusMeters)))
        {
            AutomationId = "NeighborhoodMap",
            MapType = MapType.Street,
            IsShowingUser = false
        };
        SemanticProperties.SetDescription(map, text.MapAccessibility);

        var summary = Ui.Bound(Binding.Create(static (MapViewModel model) => model.Summary), 14, Ui.Muted);
        summary.AutomationId = "MapHouseCountLabel";
        var header = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(22, 12, 22, 10),
            Children =
            {
                Ui.Heading("heading_map.png", text.MapHeading, 26),
                Ui.DemoBanner(text.MapBanner),
                summary,
                Ui.Errors()
            }
        };

        var layout = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        layout.Add(header);
        layout.Add(map, 0, 1);
        Content = layout;

    }

    private void OnHousesChanged(object? sender, EventArgs e) => UpdatePins();

    private void UpdatePins()
    {
        map.Pins.Clear();
        foreach (var (card, latitude, longitude) in viewModel.Houses)
        {
            var pin = new Pin
            {
                Label = $"{card.Nickname} · {viewModel.Text.ScareName(card.House.ScareLevel)}",
                Address = string.IsNullOrEmpty(card.PassportStatus) ? card.Status : $"{card.Status} · {card.PassportStatus}",
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
        viewModel.HousesChanged += OnHousesChanged;
        await viewModel.RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        StopUpdates();
        base.OnDisappearing();
    }

    public void StopUpdates() => viewModel.HousesChanged -= OnHousesChanged;
}
