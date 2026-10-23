using BooRoute.Core;
using BooRoute.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Map = Microsoft.Maui.Controls.Maps.Map;

namespace BooRoute.Pages;

public sealed class MapPage : LocalizedPage
{
    private readonly MapViewModel viewModel;
    private readonly Map map;

    public MapPage(MapViewModel viewModel) : base(viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        BackgroundColor = Ui.Midnight;
        SafeAreaEdges = SafeAreaEdges.Container;
        var text = viewModel.Text.Strings;
        Localize(() => Title = text.Map);

        map = new Map(MapSpan.FromCenterAndRadius(
            new Location(DemoMap.CenterLatitude, DemoMap.CenterLongitude), Distance.FromMeters(DemoMap.InitialRadiusMeters)))
        {
            AutomationId = "NeighborhoodMap",
            MapType = MapType.Street,
            IsShowingUser = false
        };
        Localize(() =>
        {
            SemanticProperties.SetDescription(map, text.MapAccessibility);
            foreach (var pin in map.Pins)
            {
                if (pin.BindingContext is HouseCard card)
                {
                    card.RefreshLocalization();
                    SetPinLabels(pin, card);
                }
            }
        });

        var summary = Ui.Bound(Binding.Create(static (MapViewModel model) => model.Summary), 14, Ui.Muted);
        summary.AutomationId = "MapHouseCountLabel";
        var header = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(22, 12, 22, 10),
            Children =
            {
                Heading("heading_map.png", () => text.MapHeading, 26),
                Banner(() => text.MapBanner),
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

    private void SetPinLabels(Pin pin, HouseCard card)
    {
        pin.Label = $"{card.Nickname} · {viewModel.Text.ScareName(card.House.ScareLevel)}";
        pin.Address = string.IsNullOrEmpty(card.VisitStatus) ? card.Status : $"{card.Status} · {card.VisitStatus}";
    }

    private void UpdatePins()
    {
        map.Pins.Clear();
        foreach (var (card, latitude, longitude) in viewModel.Houses)
        {
            var pin = new Pin
            {
                BindingContext = card,
                Location = new Location(latitude, longitude),
                Type = PinType.Place
            };
            SetPinLabels(pin, card);
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
