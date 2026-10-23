using BooRoute.ViewModels;

namespace BooRoute.Pages;

public sealed class HostEditorPage : ContentPage
{
    public HostEditorPage(HostEditorViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Demo house";
        BackgroundColor = Ui.Midnight;
        HideSoftInputOnTapped = true;
        SafeAreaEdges = SafeAreaEdges.Container;
        var fields = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 14,
            Children =
            {
                Ui.Text("🏠 " + viewModel.Heading, 29, bold: true),
                Ui.DemoBanner("DEMO HOST · Fictional addresses only · Not real registration"),
                Ui.Text("House nickname", 15, bold: true),
                Entry("Nickname", "e.g. The Pumpkin Porch", "NicknameEntry", 60),
                Ui.Text("Fictional address", 15, bold: true),
                Entry("Address", "e.g. 13 Make-Believe Lane", "AddressEntry", 120),
                Ui.Text("Tell families what to expect", 15, bold: true),
                DescriptionEditor(),
                Ui.Text("Daily welcoming hours", 15, bold: true),
                TimeFields(),
                Ui.Text("Scare level", 15, bold: true),
                Ui.Picker("Scare level", nameof(viewModel.ScareOptions), nameof(viewModel.ScareIndex), "HouseScarePicker"),
                Ui.Text("Porch status", 15, bold: true),
                Ui.Picker("Porch status", nameof(viewModel.StatusOptions), nameof(viewModel.StatusIndex), "HouseStatusPicker"),
                Ui.Toggle("Quiet visits available", nameof(viewModel.QuietVisits), "QuietVisitsSwitch"),
                Ui.Toggle("Step-free access", nameof(viewModel.StepFreeAccess), "StepFreeSwitch"),
                Ui.Errors(),
                Ui.Button("Save demo house", "SaveHouseButton", viewModel.SaveCommand),
                Ui.Text("Saved to the in-app mock backend on this device only.", 13, Ui.Muted)
            }
        };
        Content = new ScrollView { Content = fields };
    }

    private static Entry Entry(string property, string placeholder, string id, int maximumLength)
    {
        var entry = new Entry
        {
            Placeholder = placeholder,
            AutomationId = id,
            MaxLength = maximumLength,
            TextColor = Ui.Cream,
            PlaceholderColor = Ui.Muted,
            BackgroundColor = Ui.Panel,
            MinimumHeightRequest = 50,
            FontFamily = Ui.FontFamily,
            FontSize = 16,
            ReturnType = ReturnType.Next
        };
        entry.SetBinding(Microsoft.Maui.Controls.Entry.TextProperty, property);
        SemanticProperties.SetDescription(entry, placeholder);
        return entry;
    }

    private static Editor DescriptionEditor()
    {
        var editor = new Editor
        {
            Placeholder = "Decorations, treats, and how spooky it gets…",
            AutomationId = "DescriptionEditor",
            MaxLength = 500,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 105,
            TextColor = Ui.Cream,
            PlaceholderColor = Ui.Muted,
            BackgroundColor = Ui.Panel,
            FontFamily = Ui.FontFamily,
            FontSize = 16
        };
        editor.SetBinding(Editor.TextProperty, nameof(HostEditorViewModel.Description));
        SemanticProperties.SetDescription(editor, "Description for visiting families");
        return editor;
    }

    private static View TimeFields()
    {
        var opens = new TimePicker { AutomationId = "OpeningTimePicker", TextColor = Ui.Cream, FontFamily = Ui.FontFamily, Format = "HH:mm" };
        opens.SetBinding(TimePicker.TimeProperty, nameof(HostEditorViewModel.OpensAt));
        SemanticProperties.SetDescription(opens, "Opening time");
        var closes = new TimePicker { AutomationId = "ClosingTimePicker", TextColor = Ui.Cream, FontFamily = Ui.FontFamily, Format = "HH:mm" };
        closes.SetBinding(TimePicker.TimeProperty, nameof(HostEditorViewModel.ClosesAt));
        SemanticProperties.SetDescription(closes, "Closing time");
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 18 };
        grid.Add(new VerticalStackLayout { Children = { Ui.Text("Opens", 13, Ui.Muted), opens } });
        grid.Add(new VerticalStackLayout { Children = { Ui.Text("Closes", 13, Ui.Muted), closes } }, 1);
        return grid;
    }
}
