using System.Globalization;
using BooRoute.ViewModels;

namespace BooRoute.Pages;

public sealed class NeighborhoodPage : ContentPage
{
    private readonly NeighborhoodViewModel viewModel;
    private readonly IDispatcherTimer timer;
    private static readonly WelcomingColorConverter StatusColorConverter = new();

    public NeighborhoodPage(NeighborhoodViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        BackgroundColor = Ui.Midnight;
        SafeAreaEdges = SafeAreaEdges.Container;
        Title = viewModel.Mode switch
        {
            NeighborhoodMode.Host => "Demo host",
            NeighborhoodMode.Visited => "My visits",
            _ => "Explore"
        };
        var heading = viewModel.Mode switch
        {
            NeighborhoodMode.Host => "🏠 Your porch, your rules.",
            NeighborhoodMode.Visited => "🎃 A trail of good treats.",
            _ => "🎃 BooRoute"
        };
        var subtitle = viewModel.Mode switch
        {
            NeighborhoodMode.Host => "Demo hosting only. No accounts or verified ownership.",
            NeighborhoodMode.Visited => "Your visits stay on this device. Nobody else sees them.",
            _ => "Explore demo stops around Microsoft Cologne."
        };
        var header = new VerticalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(0, 12, 0, 10),
            Children = { Ui.Text(heading, 29, bold: true), Ui.Text(subtitle, 14, Ui.Muted), Ui.DemoBanner() }
        };
        if (viewModel.Mode == NeighborhoodMode.Explore)
        {
            header.Add(Ui.Toggle("Welcoming visitors now", nameof(viewModel.WelcomingOnly), "WelcomingOnlySwitch"));
            header.Add(Ui.Toggle("Quiet visits only", nameof(viewModel.QuietOnly), "QuietOnlySwitch"));
            header.Add(Ui.Picker("Scare level", nameof(viewModel.ScareOptions), nameof(viewModel.ScareIndex), "ScareFilterPicker"));
        }
        if (viewModel.Mode == NeighborhoodMode.Host)
            header.Add(Ui.Button("+ Register a demo house", "AddHouseButton", viewModel.AddHouseCommand));
        var summary = Ui.Bound(nameof(viewModel.Summary), 14, Ui.Muted);
        summary.AutomationId = "HouseCountLabel";
        header.Add(summary);
        header.Add(Ui.Errors());
        header.Add(Ui.Button("Refresh neighborhood", "RefreshButton", viewModel.RefreshCommand, secondary: true));

        var layout = new GridItemsLayout(1, ItemsLayoutOrientation.Vertical)
        {
            HorizontalItemSpacing = HouseGridLayout.ColumnSpacing
        };
        var collection = new CollectionView
        {
            AutomationId = "HousesCollection",
            Margin = new Thickness(HouseGridLayout.PageInset, 0),
            SelectionMode = SelectionMode.None,
            Header = header,
            ItemsLayout = layout,
            ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepItemsInView,
            ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
            EmptyView = new VerticalStackLayout
            {
                Padding = 26,
                Spacing = 12,
                Children =
                {
                    Ui.Text("No houses here… yet.", 24, bold: true),
                    Ui.Text(viewModel.Mode == NeighborhoodMode.Visited
                        ? "Open a house in Explore and mark it as visited."
                        : "Try a different filter or register a demo house.", 16, Ui.Muted)
                }
            },
            ItemTemplate = new DataTemplate(() => BuildHouseCard(viewModel))
        };
        collection.SetBinding(ItemsView.ItemsSourceProperty, static (NeighborhoodViewModel model) => model.Houses);
        collection.SizeChanged += (_, _) =>
        {
            var span = HouseGridLayout.GetSpan(collection.Width);
            if (layout.Span != span)
                layout.Span = span;
        };
        Content = collection;
        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(30);
        timer.Tick += async (_, _) => await viewModel.RefreshAsync();
    }

    private static View BuildHouseCard(NeighborhoodViewModel viewModel)
    {
        var status = Ui.Text(size: 14, bold: true);
        status.SetBinding(Label.TextProperty, static (HouseCard card) => card.Status);
        status.SetBinding(Label.TextColorProperty, static (HouseCard card) => card.IsWelcoming,
            converter: StatusColorConverter);
        var top = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        var nickname = Ui.Text(size: 22, bold: true);
        nickname.SetBinding(Label.TextProperty, static (HouseCard card) => card.Nickname);
        SemanticProperties.SetHeadingLevel(nickname, SemanticHeadingLevel.Level2);
        top.Add(nickname);
        var emoji = Ui.Text(size: 32);
        emoji.SetBinding(Label.TextProperty, static (HouseCard card) => card.Emoji);
        top.Add(emoji, 1);
        var button = Ui.Button(viewModel.Mode == NeighborhoodMode.Host ? "Edit house & status" : "Visit details",
            "OpenHouseButton", viewModel.OpenHouseCommand, secondary: true);
        button.BackgroundColor = Color.FromArgb("#3A2B4F");
        button.SetBinding(Button.CommandParameterProperty, static (HouseCard card) => card);
        button.SetBinding(SemanticProperties.DescriptionProperty,
            static (HouseCard card) => card.Nickname,
            stringFormat: viewModel.Mode == NeighborhoodMode.Host ? "Edit {0}" : "View {0}");
        var address = Ui.Text(size: 14, color: Ui.Muted);
        address.SetBinding(Label.TextProperty, static (HouseCard card) => card.Address);
        var hours = Ui.Text(size: 15);
        hours.SetBinding(Label.TextProperty, static (HouseCard card) => card.Hours);
        var features = Ui.Text(size: 13, color: Ui.Muted);
        features.SetBinding(Label.TextProperty, static (HouseCard card) => card.Features);
        var visitStatus = Ui.Text(size: 14, color: Ui.Mint);
        visitStatus.SetBinding(Label.TextProperty, static (HouseCard card) => card.VisitStatus);
        var content = new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                top, address, status, hours, features, visitStatus, button
            }
        };
        var card = Ui.Card(content);
        card.Margin = new Thickness(0, 0, 0, 14);
        return card;
    }

    private sealed class WelcomingColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is true ? Ui.Mint : Ui.Muted;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        timer.Start();
        await viewModel.RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        timer.Stop();
        base.OnDisappearing();
    }
}
