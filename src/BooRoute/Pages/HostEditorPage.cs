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
        var fields = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 14,
            Children =
            {
                Ui.Heading("heading_host.png", viewModel.Heading),
                Ui.DemoBanner("DEMO HOST · Public venues or fictional addresses only · No private homes · Not real registration"),
                Ui.Text("House nickname", 15, bold: true),
                Entry(Binding.Create(static (HostEditorViewModel model) => model.Nickname, mode: BindingMode.TwoWay),
                    "e.g. The Pumpkin Porch", "NicknameEntry", 60),
                Ui.Text("Public venue or fictional address", 15, bold: true),
                Entry(Binding.Create(static (HostEditorViewModel model) => model.Address, mode: BindingMode.TwoWay),
                    "e.g. 13 Make-Believe Lane", "AddressEntry", 120),
                Ui.Text("Tell families what to expect", 15, bold: true),
                DescriptionEditor(),
                Ui.Text("Daily welcoming hours", 15, bold: true),
                TimeFields(),
                Ui.Text("Scare level", 15, bold: true),
                Ui.Picker("Scare level",
                    Binding.Create(static (HostEditorViewModel model) => model.ScareOptions),
                    Binding.Create(static (HostEditorViewModel model) => model.ScareIndex, mode: BindingMode.TwoWay),
                    "HouseScarePicker"),
                Ui.Text("Porch status", 15, bold: true),
                Ui.Picker("Porch status",
                    Binding.Create(static (HostEditorViewModel model) => model.StatusOptions),
                    Binding.Create(static (HostEditorViewModel model) => model.StatusIndex, mode: BindingMode.TwoWay),
                    "HouseStatusPicker"),
                Ui.Toggle("Quiet visits available",
                    Binding.Create(static (HostEditorViewModel model) => model.QuietVisits, mode: BindingMode.TwoWay),
                    "QuietVisitsSwitch"),
                Ui.Toggle("Step-free access",
                    Binding.Create(static (HostEditorViewModel model) => model.StepFreeAccess, mode: BindingMode.TwoWay),
                    "StepFreeSwitch"),
                Ui.Errors(),
                Ui.Button("Save demo house", "SaveHouseButton", viewModel.SaveCommand),
                Ui.Text("Saved to the in-app mock backend on this device only.", 13, Ui.Muted)
            }
        };
        Content = new ScrollView { Content = fields };
    }

    private static Entry Entry(BindingBase binding, string placeholder, string id, int maximumLength)
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
            FontSize = 16,
            ReturnType = ReturnType.Next
        };
        entry.SetBinding(Microsoft.Maui.Controls.Entry.TextProperty, binding);
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
            FontSize = 16
        };
        editor.SetBinding(Editor.TextProperty, static (HostEditorViewModel model) => model.Description,
            mode: BindingMode.TwoWay);
        SemanticProperties.SetDescription(editor, "Description for visiting families");
        return editor;
    }

    private static View TimeFields()
    {
        var opens = new TimePicker { AutomationId = "OpeningTimePicker", TextColor = Ui.Cream, Format = "HH:mm" };
        opens.SetBinding(TimePicker.TimeProperty, static (HostEditorViewModel model) => model.OpensAt,
            mode: BindingMode.TwoWay);
        SemanticProperties.SetDescription(opens, "Opening time");
        var closes = new TimePicker { AutomationId = "ClosingTimePicker", TextColor = Ui.Cream, Format = "HH:mm" };
        closes.SetBinding(TimePicker.TimeProperty, static (HostEditorViewModel model) => model.ClosesAt,
            mode: BindingMode.TwoWay);
        SemanticProperties.SetDescription(closes, "Closing time");
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 18 };
        grid.Add(new VerticalStackLayout { Children = { Ui.Text("Opens", 13, Ui.Muted), opens } });
        grid.Add(new VerticalStackLayout { Children = { Ui.Text("Closes", 13, Ui.Muted), closes } }, 1);
        return grid;
    }
}
