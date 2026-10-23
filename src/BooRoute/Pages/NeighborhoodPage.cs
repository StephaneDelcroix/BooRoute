using System.Globalization;
using BooRoute.ViewModels;

namespace BooRoute.Pages;

public sealed class NeighborhoodPage : ContentPage
{
    private readonly NeighborhoodViewModel viewModel;
    private readonly IDispatcherTimer timer;
    private static readonly WelcomingColorConverter StatusColorConverter = new();
    private static readonly ImageFileConverter ArtConverter = new();

    public NeighborhoodPage(NeighborhoodViewModel viewModel)
    {
        this.viewModel = viewModel;
        var text = viewModel.Text.Strings;
        BindingContext = viewModel;
        BackgroundColor = Ui.Midnight;
        // CollectionView has no SafeAreaEdges of its own: keep the list below the nav bar,
        // but let it run to the bottom edge so it scrolls behind the tab bar.
        SafeAreaEdges = new(SafeAreaRegions.Container, SafeAreaRegions.Container, SafeAreaRegions.Container, SafeAreaRegions.None);
        Title = viewModel.Mode switch
        {
            NeighborhoodMode.Host => text.DemoHost,
            _ => text.Explore
        };
        var heading = viewModel.Mode switch
        {
            NeighborhoodMode.Host => text.HostHeading,
            _ => "BooRoute"
        };
        var headingArt = viewModel.Mode switch
        {
            NeighborhoodMode.Host => "heading_host.png",
            _ => "heading_explore.png"
        };
        var subtitle = viewModel.Mode switch
        {
            NeighborhoodMode.Host => text.HostSubtitle,
            _ => text.ExploreSubtitle
        };
        var header = new VerticalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(0, 12, 0, 10),
            Children = { Ui.Heading(headingArt, heading), Ui.Text(subtitle, 14, Ui.Muted), Ui.DemoBanner() }
        };
        if (viewModel.Mode == NeighborhoodMode.Explore)
        {
            header.Add(Ui.Toggle(text.WelcomingOnly,
                Binding.Create(static (NeighborhoodViewModel model) => model.WelcomingOnly), "WelcomingOnlySwitch"));
            header.Add(Ui.Toggle(text.QuietOnly,
                Binding.Create(static (NeighborhoodViewModel model) => model.QuietOnly), "QuietOnlySwitch"));
            header.Add(Ui.Picker(text.ScareLevel,
                Binding.Create(static (NeighborhoodViewModel model) => model.ScareOptions),
                Binding.Create(static (NeighborhoodViewModel model) => model.ScareIndex), "ScareFilterPicker"));
        }
        if (viewModel.Mode == NeighborhoodMode.Host)
            header.Add(Ui.Button(text.RegisterHouse, "AddHouseButton", viewModel.AddHouseCommand));
        var summary = Ui.Bound(Binding.Create(static (NeighborhoodViewModel model) => model.Summary), 14, Ui.Muted);
        summary.AutomationId = "HouseCountLabel";
        header.Add(summary);
        header.Add(Ui.Errors());
        header.Add(Ui.Button(text.RefreshNeighbourhood, "RefreshButton", viewModel.RefreshCommand, secondary: true));

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
                    Ui.Text(text.EmptyHeading, 24, bold: true),
                    Ui.Text(text.EmptyHouses, 16, Ui.Muted)
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
        var art = Ui.Art(null, 36);
        art.SetBinding(Image.SourceProperty, static (HouseCard card) => card.ScareArt, converter: ArtConverter);
        top.Add(art, 1);
        var button = Ui.Button(viewModel.Mode == NeighborhoodMode.Host
                ? viewModel.Text.Strings.EditHouse : viewModel.Text.Strings.HouseStampDetails,
            "OpenHouseButton", viewModel.OpenHouseCommand, secondary: true);
        button.BackgroundColor = Color.FromArgb("#3A2B4F");
        button.SetBinding(Button.CommandParameterProperty, static (HouseCard card) => card);
        if (viewModel.Mode == NeighborhoodMode.Host)
            button.SetBinding(SemanticProperties.DescriptionProperty, static (HouseCard card) => card.EditDescription);
        else
            button.SetBinding(SemanticProperties.DescriptionProperty, static (HouseCard card) => card.ViewDescription);
        var address = Ui.Text(size: 14, color: Ui.Muted);
        address.SetBinding(Label.TextProperty, static (HouseCard card) => card.Address);
        var hours = Ui.Text(size: 15);
        hours.SetBinding(Label.TextProperty, static (HouseCard card) => card.Hours);
        var features = Ui.Text(size: 13, color: Ui.Muted);
        features.SetBinding(Label.TextProperty, static (HouseCard card) => card.Features);
        var stamp = Ui.Stamp(compact: true);
        stamp.SetBinding(BindingContextProperty, static (HouseCard card) => card.Stamp,
            converter: new StampPresentationConverter(viewModel.Text));
        var passport = new Grid
        {
            ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)],
            ColumnSpacing = 12
        };
        passport.Add(stamp);
        passport.Add(Ui.Bound(Binding.Create(static (HouseCard card) => card.PassportStatus), 14, Ui.Mint), 1);
        passport.SetBinding(IsVisibleProperty, static (HouseCard card) => card.IsCollected);
        var content = new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                top, address, status, hours, features, passport, button
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

    private sealed class ImageFileConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is string file ? ImageSource.FromFile(file) : null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class StampPresentationConverter(BooRoute.Core.Localization.AppText text) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is BooRoute.Core.StampDesign design ? new StampPresentation(design, text) : null;

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
        StopUpdates();
        base.OnDisappearing();
    }

    public void StopUpdates() => timer.Stop();
}
