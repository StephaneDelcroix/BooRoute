using BooRoute.ViewModels;

namespace BooRoute.Pages;

public sealed class PassportPage : ContentPage
{
    private readonly PassportViewModel viewModel;

    public PassportPage(PassportViewModel viewModel)
    {
        this.viewModel = viewModel;
        var text = viewModel.Text.Strings;
        BindingContext = viewModel;
        Title = text.Passport;
        BackgroundColor = Ui.Midnight;
        SafeAreaEdges = SafeAreaEdges.Container;
        var heading = Ui.Heading("heading_visits.png", text.PassportHeading);
        var progress = new ProgressBar { ProgressColor = Ui.Orange, AutomationId = "PassportProgress" };
        progress.SetBinding(ProgressBar.ProgressProperty, static (PassportViewModel model) => model.Progress);
        var achievements = new VerticalStackLayout { Spacing = 10, AutomationId = "PassportAchievements" };
        BindableLayout.SetItemsSource(achievements, viewModel.Achievements);
        BindableLayout.SetItemTemplate(achievements, new DataTemplate(() =>
        {
            var title = Ui.Bound(Binding.Create(static (AchievementCard card) => card.Title), 18, bold: true);
            title.SetBinding(Label.TextColorProperty, static (AchievementCard card) => card.Color);
            return Ui.Card(new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    title, Ui.Bound(Binding.Create(static (AchievementCard card) => card.Description), 14, Ui.Muted),
                    Ui.Bound(Binding.Create(static (AchievementCard card) => card.Status), 13, Ui.Muted)
                }
            });
        }));
        var header = new VerticalStackLayout
        {
            Padding = new Thickness(22, 12),
            Spacing = 12,
            Children =
            {
                heading,
                Ui.Text(text.PassportPrivacy, 14, Ui.Muted),
                Ui.Bound(Binding.Create(static (PassportViewModel model) => model.ProgressText), 17, bold: true), progress,
                Ui.Text(text.Achievements, 22, bold: true), achievements,
                Ui.Errors(), Ui.Button(text.RefreshPassport, "RefreshPassportButton", viewModel.RefreshCommand, secondary: true),
                Ui.Text(text.CollectedStamps, 22, bold: true)
            }
        };
        var stamps = new CollectionView
        {
            AutomationId = "PassportStamps",
            SelectionMode = SelectionMode.None,
            ItemsLayout = new GridItemsLayout(2, ItemsLayoutOrientation.Vertical)
            {
                HorizontalItemSpacing = 10, VerticalItemSpacing = 10
            },
            Header = header,
            EmptyView = new VerticalStackLayout
            {
                AutomationId = "PassportEmpty",
                Padding = 26,
                Spacing = 12,
                Children =
                {
                    Ui.Text(text.PassportEmptyHeading, 24, bold: true),
                    Ui.Text(text.PassportEmptyDescription, 16, Ui.Muted)
                }
            },
            ItemTemplate = new DataTemplate(() => BuildStamp(viewModel))
        };
        stamps.SetBinding(ItemsView.ItemsSourceProperty, static (PassportViewModel model) => model.Stamps);
        Content = stamps;
    }

    private static View BuildStamp(PassportViewModel viewModel)
    {
        var stamp = Ui.Stamp();
        stamp.SetBinding(BindingContextProperty, static (PassportStamp entry) => entry.Stamp);
        var open = Ui.Button(viewModel.Text.Strings.ViewHouse, "OpenPassportHouseButton", viewModel.OpenHouseCommand, secondary: true);
        open.SetBinding(Button.CommandParameterProperty, static (PassportStamp entry) => entry);
        open.SetBinding(SemanticProperties.DescriptionProperty, static (PassportStamp entry) => entry.ViewDescription);
        var card = Ui.Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                stamp, Ui.Bound(Binding.Create(static (PassportStamp entry) => entry.Nickname), 18, bold: true),
                Ui.Bound(Binding.Create(static (PassportStamp entry) => entry.CollectedAt), 13, Ui.Muted),
                Ui.Bound(Binding.Create(static (PassportStamp entry) => entry.Features), 13, Ui.Muted), open
            }
        }, new Thickness(12));
        card.Margin = new Thickness(6, 0, 6, 12);
        var tap = new TapGestureRecognizer { Command = viewModel.OpenHouseCommand };
        tap.SetBinding(TapGestureRecognizer.CommandParameterProperty, static (PassportStamp entry) => entry);
        card.GestureRecognizers.Add(tap);
        return card;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.RefreshAsync();
    }
}
