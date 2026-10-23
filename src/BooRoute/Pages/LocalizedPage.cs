using BooRoute.ViewModels;

namespace BooRoute.Pages;

public abstract class LocalizedPage(ViewModelBase viewModel) : ContentPage
{
    private readonly List<Action> translations = [];

    protected void Localize(Action update)
    {
        translations.Add(update);
        update();
    }

    public void RefreshLocalization()
    {
        viewModel.RefreshLocalization();
        foreach (var translate in translations)
            translate();
    }

    protected Label Text(Func<string> value, double size = 16, Color? color = null, bool bold = false)
    {
        var label = Ui.Text(size: size, color: color, bold: bold);
        Localize(() => label.Text = value());
        return label;
    }

    protected View Heading(string art, Func<string> value, double size = 29)
    {
        var label = Text(value, size, bold: true);
        label.VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level1);
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 10 };
        grid.Add(Ui.Art(art, size + 5));
        grid.Add(label, 1);
        return grid;
    }

    protected Button Button(Func<string> title, string id, System.Windows.Input.ICommand command, bool secondary = false)
    {
        var button = Ui.Button("", id, command, secondary);
        Localize(() => button.Text = title());
        return button;
    }

    protected View Banner(Func<string> value) => Ui.Card(Text(value, 12, Ui.Mint), new Thickness(12, 10));

    protected View Toggle(Func<string> label, BindingBase binding, string id)
    {
        var toggle = new Switch { AutomationId = id, OnColor = Ui.Orange, HorizontalOptions = LayoutOptions.End };
        toggle.SetBinding(Switch.IsToggledProperty, binding);
        Localize(() => SemanticProperties.SetDescription(toggle, label()));
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        var title = Text(label, 15);
        title.VerticalOptions = LayoutOptions.Center;
        grid.Add(title);
        grid.Add(toggle, 1);
        return grid;
    }

    protected Picker Picker(Func<string> title, Func<string[]> options, BindingBase index, string id)
    {
        var picker = new Picker
        {
            AutomationId = id, TextColor = Ui.Cream, TitleColor = Ui.Muted,
            BackgroundColor = Ui.Panel, MinimumHeightRequest = 48, FontSize = 16
        };
        picker.SetBinding(Microsoft.Maui.Controls.Picker.SelectedIndexProperty, index);
        Localize(() =>
        {
            var selected = picker.SelectedIndex;
            picker.RemoveBinding(Microsoft.Maui.Controls.Picker.SelectedIndexProperty);
            picker.Title = title();
            SemanticProperties.SetDescription(picker, title());
            picker.ItemsSource = options();
            picker.SelectedIndex = selected;
            picker.SetBinding(Microsoft.Maui.Controls.Picker.SelectedIndexProperty, index);
        });
        return picker;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshLocalization();
    }
}
