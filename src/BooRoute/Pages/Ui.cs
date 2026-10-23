using BooRoute.ViewModels;
using BooRoute.Core.Localization;
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

    public static Label Text(string value = "", double size = 16, Color? color = null, bool bold = false) =>
        new()
        {
            Text = value,
            FontSize = size,
            TextColor = color ?? Cream,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap
        };

    public static Label Bound(BindingBase binding, double size = 16, Color? color = null, bool bold = false)
    {
        var label = Text(size: size, color: color, bold: bold);
        label.SetBinding(Label.TextProperty, binding);
        return label;
    }

    // Decorative art. Nearby text always says the same thing, so screen readers skip it.
    public static Image Art(ImageSource? source, double size)
    {
        var image = new Image
        {
            WidthRequest = size,
            HeightRequest = size,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center
        };
        if (source is not null)
            image.Source = source;
        AutomationProperties.SetIsInAccessibleTree(image, false);
        return image;
    }

    public static View Heading(string art, string text, double size = 29)
    {
        var label = Text(text, size, bold: true);
        label.VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level1);
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 10 };
        grid.Add(Art(art, size + 5));
        grid.Add(label, 1);
        return grid;
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
            FontSize = 16,
            LineBreakMode = LineBreakMode.WordWrap,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 14,
            MinimumHeightRequest = 48,
            BackgroundColor = secondary ? Panel : Orange,
            TextColor = secondary ? Cream : Midnight,
            Padding = new Thickness(18, 10)
        };

    public static View Errors()
    {
        var errors = Bound(Binding.Create(static (ViewModelBase model) => model.ErrorMessage), 14, Orange);
        errors.AutomationId = "ErrorMessage";
        var spinner = new ActivityIndicator { AutomationId = "LoadingIndicator", Color = Orange, HeightRequest = 24 };
        spinner.SetBinding(ActivityIndicator.IsRunningProperty, static (ViewModelBase model) => model.IsBusy);
        spinner.SetBinding(VisualElement.IsVisibleProperty, static (ViewModelBase model) => model.IsBusy);
        return new VerticalStackLayout { Spacing = 4, Children = { errors, spinner } };
    }

    public static Border Stamp(bool compact = false)
    {
        var symbol = Bound(Binding.Create(static (StampPresentation stamp) => stamp.Symbol), compact ? 28 : 40);
        symbol.HorizontalTextAlignment = TextAlignment.Center;
        var accent = Bound(Binding.Create(static (StampPresentation stamp) => stamp.Accent), compact ? 12 : 15, Midnight, true);
        accent.HorizontalTextAlignment = TextAlignment.Center;
        var stamp = Card(new VerticalStackLayout { Spacing = 2, Children = { symbol, accent } },
            new Thickness(compact ? 8 : 12));
        stamp.SetBinding(VisualElement.BackgroundColorProperty, static (StampPresentation stamp) => stamp.Color);
        stamp.SetBinding(SemanticProperties.DescriptionProperty, static (StampPresentation stamp) => stamp.Description);
        return stamp;
    }

    public static View DemoBanner(string? text = null) =>
        Card(Text(text ?? AppText.Default.Strings.DemoBanner, 12, Mint), new Thickness(12, 10));

    public static View Toggle(string label, BindingBase binding, string id)
    {
        var toggle = new Switch { AutomationId = id, OnColor = Orange, HorizontalOptions = LayoutOptions.End };
        toggle.SetBinding(Switch.IsToggledProperty, binding);
        SemanticProperties.SetDescription(toggle, label);
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        var title = Text(label, 15);
        title.VerticalOptions = LayoutOptions.Center;
        grid.Add(title);
        grid.Add(toggle, 1);
        return grid;
    }

    public static Picker Picker(string title, BindingBase items, BindingBase index, string id)
    {
        var picker = new Picker
        {
            Title = title,
            AutomationId = id,
            TextColor = Cream,
            TitleColor = Muted,
            BackgroundColor = Panel,
            MinimumHeightRequest = 48,
            FontSize = 16
        };
        picker.SetBinding(Microsoft.Maui.Controls.Picker.ItemsSourceProperty, items);
        picker.SetBinding(Microsoft.Maui.Controls.Picker.SelectedIndexProperty, index);
        SemanticProperties.SetDescription(picker, title);
        return picker;
    }
}
