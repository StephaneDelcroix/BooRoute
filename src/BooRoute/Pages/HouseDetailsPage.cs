using BooRoute.ViewModels;

namespace BooRoute.Pages;

public sealed class HouseDetailsPage : LocalizedPage
{
    private readonly HouseDetailsViewModel viewModel;
    private readonly PumpkinConfettiView confetti = new();

    public HouseDetailsPage(HouseDetailsViewModel viewModel) : base(viewModel)
    {
        this.viewModel = viewModel;
        var text = viewModel.Text.Strings;
        BindingContext = viewModel;
        Localize(() => Title = text.HouseDetails);
        BackgroundColor = Ui.Midnight;
        var house = viewModel.House;
        var card = viewModel.Card;
        var visit = Ui.Button("", "ToggleVisitedButton", viewModel.ToggleVisitedCommand);
        visit.SetBinding(Microsoft.Maui.Controls.Button.TextProperty, static (HouseDetailsViewModel model) => model.VisitButtonText);
        var status = Text(() => card.Status, 17, card.IsWelcoming ? Ui.Mint : Ui.Muted, true);
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
                    Banner(() => text.FictionalDestination),
                    status, Text(() => text.DailyHoursDescriptionFormat(card.Hours), 17),
                    Ui.Text(house.Description, 18),
                    Ui.Card(new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            Text(() => text.ScareDescriptionFormat(viewModel.Text.ScareName(house.ScareLevel)), 17, bold: true),
                            Text(() => house.QuietVisits ? text.QuietAvailable : text.QuietNotIndicated, 16),
                            Text(() => house.StepFreeAccess ? text.StepFreeIndicated : text.StepFreeNotIndicated, 16),
                            Text(() => text.AccessibilityDisclaimer, 13, Ui.Muted)
                        }
                    }),
                    Ui.Errors(), visit,
                    Text(() => text.VisitPrivacy, 13, Ui.Muted)
                }
            }
        };
        Content = new Grid { Children = { details, confetti } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        viewModel.VisitMarked += OnVisitMarked;
        await viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        viewModel.VisitMarked -= OnVisitMarked;
        confetti.Stop();
        base.OnDisappearing();
    }

    private void OnVisitMarked(object? sender, EventArgs e) => confetti.Burst();
}
