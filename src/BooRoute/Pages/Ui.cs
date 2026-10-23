using BooRoute.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace BooRoute.Pages;

public static class Ui
{
    public static Color Midnight => Color.FromArgb("#171125");
    public static Color Panel => Color.FromArgb("#271D3A");
    public static Color Orange => Color.FromArgb("#FFAA61");
    public static Color Cream => Color.FromArgb("#FFF4DF");
    public static Color Muted => Color.FromArgb("#C3B5D6");
    public static Color Mint => Color.FromArgb("#B9E88C");

    public const string FontFamily = "OpenSansRegular";

    public static Label Text(string value = "", double size = 16, Color? color = null, bool bold = false) =>
        new()
        {
            Text = value,
            FontFamily = Ui.FontFamily,
            FontSize = size,
            TextColor = color ?? Cream,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap
        };

    public static Label Bound(string property, double size = 16, Color? color = null, bool bold = false)
    {
        var label = Text(size: size, color: color, bold: bold);
        label.SetBinding(Label.TextProperty, property);
        return label;
    }

    public static Border Card(View content, Thickness? padding = null) => new()
    {
        BackgroundColor = Panel,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 20 },
        Padding = padding ?? new Thickness(18),
        Content = content
    };

    public static Button Button(string title, string id, System.Windows.Input.ICommand command, bool secondary = false) =>
        new()
        {
            Text = title,
            AutomationId = id,
            Command = command,
            FontFamily = Ui.FontFamily,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 14,
            MinimumHeightRequest = 48,
            BackgroundColor = secondary ? Panel : Orange,
            TextColor = secondary ? Cream : Midnight,
            Padding = new Thickness(18, 10)
        };

    public static View Errors()
    {
        var errors = Bound(nameof(ViewModelBase.ErrorMessage), 14, Orange);
        errors.AutomationId = "ErrorMessage";
        var spinner = new ActivityIndicator { Color = Orange, HeightRequest = 24 };
        spinner.SetBinding(ActivityIndicator.IsRunningProperty, nameof(ViewModelBase.IsBusy));
        spinner.SetBinding(VisualElement.IsVisibleProperty, nameof(ViewModelBase.IsBusy));
        return new VerticalStackLayout { Spacing = 4, Children = { errors, spinner } };
    }

    public static View DemoBanner(string text = "DEMO · Public Cologne venues, not participating hosts · All Halloween details simulated") =>
        Card(Text(text, 12, Mint), new Thickness(12, 10));

    public static View Toggle(string label, string property, string id)
    {
        var toggle = new Switch { AutomationId = id, OnColor = Orange, HorizontalOptions = LayoutOptions.End };
        toggle.SetBinding(Switch.IsToggledProperty, property);
        SemanticProperties.SetDescription(toggle, label);
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        var title = Text(label, 15);
        title.VerticalOptions = LayoutOptions.Center;
        grid.Add(title);
        grid.Add(toggle, 1);
        return grid;
    }

    public static Picker Picker(string title, string items, string index, string id)
    {
        var picker = new Picker
        {
            Title = title,
            AutomationId = id,
            TextColor = Cream,
            TitleColor = Muted,
            BackgroundColor = Panel,
            MinimumHeightRequest = 48,
            FontFamily = Ui.FontFamily,
            FontSize = 16
        };
        picker.SetBinding(Microsoft.Maui.Controls.Picker.ItemsSourceProperty, items);
        picker.SetBinding(Microsoft.Maui.Controls.Picker.SelectedIndexProperty, index);
        SemanticProperties.SetDescription(picker, title);
        return picker;
    }
}
