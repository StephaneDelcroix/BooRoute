using BooRoute.ViewModels;

namespace BooRoute.Pages;

public sealed class HouseDetailsPage : ContentPage
{
    private readonly HouseDetailsViewModel viewModel;
    private readonly PumpkinConfettiView confetti = new();

    public HouseDetailsPage(HouseDetailsViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "House details";
        BackgroundColor = Ui.Midnight;
        var house = viewModel.House;
        var card = viewModel.Card;
        var visit = Ui.Button("", "ToggleVisitedButton", viewModel.ToggleVisitedCommand);
        visit.SetBinding(Button.TextProperty, static (HouseDetailsViewModel model) => model.VisitButtonText);
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
                    Ui.DemoBanner("DEMO DESTINATION · Not a participating host · Do not visit for treats"),
                    status, Ui.Text($"Simulated welcoming hours: {card.Hours}", 17),
                    Ui.Text(house.Description, 18),
                    Ui.Card(new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            Ui.Text($"Scare level: {house.ScareLevel}", 17, bold: true),
                            Ui.Text(house.QuietVisits ? "✓ Quiet visits available" : "Quiet visits not indicated", 16),
                            Ui.Text(house.StepFreeAccess ? "✓ Step-free access indicated" : "Step-free access not indicated", 16),
                            Ui.Text("Scare levels, quiet visits and accessibility are demo data, not verified venue information.", 13, Ui.Muted)
                        }
                    }),
                    Ui.Errors(), visit,
                    Ui.Text("Your visit history stays on this device. No accounts. No tracking.", 13, Ui.Muted)
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
