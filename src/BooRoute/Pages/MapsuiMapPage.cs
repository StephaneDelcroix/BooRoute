#if !IOS && !MACCATALYST && !ANDROID && !WINDOWS
using BooRoute.Core;
using BooRoute.ViewModels;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Brush = Mapsui.Styles.Brush;
using Color = Microsoft.Maui.Graphics.Color;
using MapsuiColor = Mapsui.Styles.Color;

namespace BooRoute.Pages;

// .NET MAUI Maps has no Avalonia implementation, so desktop and browser draw the same map with
// Mapsui and OpenStreetMap tiles. Tapping a pin shows the house, like a native pin's info window.
public sealed class MapsuiMapPage : ContentPage
{
    private readonly MapViewModel viewModel;
    private readonly MapControl map;
    private readonly MemoryLayer pins = new("Houses") { Style = null };
    private readonly Border selection;
    private readonly Label selectionName;
    private readonly Label selectionStatus;
    private House? selectedHouse;

    public MapsuiMapPage(MapViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        BackgroundColor = Ui.Midnight;
        SafeAreaEdges = SafeAreaEdges.Container;
        Title = "Map";

        map = new MapControl { AutomationId = "NeighborhoodMap" };
        map.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Map.Layers.Add(pins);
        var (x, y) = SphericalMercator.FromLonLat(DemoMap.CenterLongitude, DemoMap.CenterLatitude);
        // Web Mercator stretches distances by 1 / cos(latitude).
        var radius = DemoMap.InitialRadiusMeters / Math.Cos(DemoMap.CenterLatitude * Math.PI / 180);
        map.Map.Navigator.ZoomToBox(new MRect(x - radius, y - radius, x + radius, y + radius));
        map.Map.Tapped += (_, args) =>
        {
            if (args.GetMapInfo([pins]).Feature?["card"] is HouseCard card)
                Select(card);
            else
                Select(null);
        };
        SemanticProperties.SetDescription(map, "Map of the demo neighborhood houses");

        selectionName = Ui.Text(size: 18, bold: true);
        selectionStatus = Ui.Text(size: 14, color: Ui.Muted);
        var open = Ui.Button("Visit details", "MapVisitDetailsButton", new Command(async () =>
        {
            if (selectedHouse is { } house)
                await viewModel.OpenHouseAsync(house);
        }));
        selection = Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { selectionName, selectionStatus, open } });
        selection.Margin = new Thickness(16);
        selection.VerticalOptions = LayoutOptions.End;
        selection.IsVisible = false;

        var layout = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        layout.Add(MapPage.Header());
        layout.Add(map, 0, 1);
        layout.Add(selection, 0, 1);
        Content = layout;

        viewModel.HousesChanged += (_, _) => UpdatePins();
    }

    private void UpdatePins()
    {
        pins.Features = viewModel.Houses.Select(house =>
        {
            var feature = new PointFeature(SphericalMercator.FromLonLat(house.Longitude, house.Latitude))
            {
                Styles =
                {
                    new SymbolStyle
                    {
                        SymbolScale = 0.6,
                        Fill = new Brush(ToMapsui(house.Card.Visited ? Ui.Mint : Ui.Orange)),
                        Outline = new Pen(ToMapsui(Ui.Midnight), 2)
                    }
                }
            };
            feature["card"] = house.Card;
            return feature;
        }).ToList();
        pins.FeaturesWereModified();
        map.Map.Refresh();

        // Keep the open card current, or close it if that house is no longer on the map.
        Select(viewModel.Houses.FirstOrDefault(house => house.Card.House.Id == selectedHouse?.Id)?.Card);
    }

    private void Select(HouseCard? card)
    {
        selectedHouse = card?.House;
        selection.IsVisible = card is not null;
        if (card is null)
            return;
        selectionName.Text = $"{card.Emoji} {card.Nickname}";
        selectionStatus.Text = string.IsNullOrEmpty(card.VisitStatus) ? card.Status : $"{card.Status} · {card.VisitStatus}";
    }

    private static MapsuiColor ToMapsui(Color color) => new()
    {
        R = (int)(color.Red * 255),
        G = (int)(color.Green * 255),
        B = (int)(color.Blue * 255),
        A = (int)(color.Alpha * 255)
    };

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.RefreshAsync();
    }
}
#endif
