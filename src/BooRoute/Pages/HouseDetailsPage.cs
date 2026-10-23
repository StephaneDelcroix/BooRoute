using BooRoute.ViewModels;

namespace BooRoute.Pages;

public sealed class HouseDetailsPage : ContentPage
{
    private readonly HouseDetailsViewModel viewModel;
    private readonly PumpkinConfettiView confetti = new();
    private readonly Border stamp;

    public HouseDetailsPage(HouseDetailsViewModel viewModel)
    {
        this.viewModel = viewModel;
        var text = viewModel.Text.Strings;
        BindingContext = viewModel;
        Title = text.HouseDetails;
        BackgroundColor = Ui.Midnight;
        var house = viewModel.House;
        var card = viewModel.Card;
        var collect = Ui.Button("", "CollectStampButton", viewModel.CollectStampCommand);
        collect.SetBinding(Button.TextProperty, static (HouseDetailsViewModel model) => model.StampButtonText);
        collect.SetBinding(IsEnabledProperty, static (HouseDetailsViewModel model) => model.CanCollect);
        var remove = Ui.Button(text.RemoveStamp, "RemoveStampButton", viewModel.RemoveStampCommand, secondary: true);
        remove.SetBinding(IsVisibleProperty, static (HouseDetailsViewModel model) => model.IsCollected);
        stamp = Ui.Stamp();
        var presentation = new StampPresentation(card.Stamp, viewModel.Text);
        stamp.BindingContext = presentation;
        stamp.AutomationId = "HouseStampPreview";
        var status = Ui.Text(card.Status, 17, card.IsWelcoming ? Ui.Mint : Ui.Muted, true);
        status.AutomationId = "HouseStatusLabel";
        var name = Ui.Text(house.Nickname, 32, bold: true);
        name.AutomationId = "HouseNicknameLabel";
        SemanticProperties.SetHeadingLevel(name, SemanticHeadingLevel.Level1);
        var details = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 18,
                Children =
                {
                    Ui.Art(card.ScareArt, 72), name, Ui.Text(house.Address, 16, Ui.Muted),
                    Ui.DemoBanner(text.FictionalDestination),
                    status, Ui.Text(text.DailyHoursDescriptionFormat(card.Hours), 17),
                    Ui.Text(house.Description, 18),
                    Ui.Card(new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            Ui.Text(text.ScareDescriptionFormat(viewModel.Text.ScareName(house.ScareLevel)), 17, bold: true),
                            Ui.Text(house.QuietVisits ? text.QuietAvailable : text.QuietNotIndicated, 16),
                            Ui.Text(house.StepFreeAccess ? text.StepFreeIndicated : text.StepFreeNotIndicated, 16),
                            Ui.Text(text.AccessibilityDisclaimer, 13, Ui.Muted)
                        }
                    }),
                    Ui.Text(text.YourStamp, 22, bold: true),
                    stamp, Ui.Text(presentation.Description, 14, Ui.Muted),
                    Ui.Errors(), collect, remove,
                    Ui.Text(text.PassportPrivacy, 13, Ui.Muted)
                }
            }
        };
        Content = new Grid { Children = { details, confetti } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        viewModel.StampCollected += OnStampCollected;
        await viewModel.LoadAsync();
    }

    private async void OnStampCollected(object? sender, EventArgs args)
    {
        stamp.CancelAnimations();
        confetti.Burst();
        if (!PumpkinConfettiView.MotionEnabled)
            return;
        stamp.Scale = 0.65;
        stamp.Opacity = 0;
        await Task.WhenAll(stamp.ScaleToAsync(1, 350, Easing.SpringOut), stamp.FadeToAsync(1, 350));
    }

    protected override void OnDisappearing()
    {
        viewModel.StampCollected -= OnStampCollected;
        confetti.Stop();
        stamp.CancelAnimations();
        stamp.Scale = 1;
        stamp.Opacity = 1;
        base.OnDisappearing();
    }
}
