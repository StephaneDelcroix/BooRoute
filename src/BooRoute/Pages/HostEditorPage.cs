using BooRoute.ViewModels;
using BooRoute.Core.Localization;

namespace BooRoute.Pages;

public sealed class HostEditorPage : LocalizedPage
{
    public HostEditorPage(HostEditorViewModel viewModel) : base(viewModel)
    {
        BindingContext = viewModel;
        var text = viewModel.Text.Strings;
        Localize(() => Title = text.DemoHouse);
        BackgroundColor = Ui.Midnight;
        HideSoftInputOnTapped = true;
        var fields = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 14,
            Children =
            {
                Heading("heading_host.png", () => viewModel.Heading),
                Banner(() => text.HostDemoBanner),
                Text(() => text.HouseNickname, 15, bold: true),
                Entry(Binding.Create(static (HostEditorViewModel model) => model.Nickname),
                    () => text.NicknamePlaceholder, () => text.HouseNickname, "NicknameEntry", 60),
                Text(() => text.FictionalAddress, 15, bold: true),
                Entry(Binding.Create(static (HostEditorViewModel model) => model.Address),
                    () => text.AddressPlaceholder, () => text.FictionalAddress, "AddressEntry", 120),
                Text(() => text.DescriptionHeading, 15, bold: true),
                DescriptionEditor(text),
                Text(() => text.DailyHours, 15, bold: true),
                TimeFields(text),
                Text(() => text.ScareLevel, 15, bold: true),
                Picker(() => text.ScareLevel, () => viewModel.ScareOptions,
                    Binding.Create(static (HostEditorViewModel model) => model.ScareIndex), "HouseScarePicker"),
                Text(() => text.PorchStatus, 15, bold: true),
                Picker(() => text.PorchStatus, () => viewModel.StatusOptions,
                    Binding.Create(static (HostEditorViewModel model) => model.StatusIndex), "HouseStatusPicker"),
                Toggle(() => text.QuietVisitsAvailable,
                    Binding.Create(static (HostEditorViewModel model) => model.QuietVisits), "QuietVisitsSwitch"),
                Toggle(() => text.StepFreeAccess,
                    Binding.Create(static (HostEditorViewModel model) => model.StepFreeAccess), "StepFreeSwitch"),
                Ui.Errors(),
                Button(() => text.SaveHouse, "SaveHouseButton", viewModel.SaveCommand),
                Text(() => text.StorageNotice, 13, Ui.Muted)
            }
        };
        Content = new ScrollView { Content = fields };
    }

    private Entry Entry(BindingBase binding, Func<string> placeholder, Func<string> description, string id, int maximumLength)
    {
        var entry = new Entry
        {
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
        Localize(() =>
        {
            entry.Placeholder = placeholder();
            SemanticProperties.SetDescription(entry, description());
        });
        return entry;
    }

    private Editor DescriptionEditor(AppTextLocalized text)
    {
        var editor = new Editor
        {
            AutomationId = "DescriptionEditor",
            MaxLength = 500,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 105,
            TextColor = Ui.Cream,
            PlaceholderColor = Ui.Muted,
            BackgroundColor = Ui.Panel,
            FontSize = 16
        };
        editor.SetBinding(Editor.TextProperty, static (HostEditorViewModel model) => model.Description);
        Localize(() =>
        {
            editor.Placeholder = text.DescriptionPlaceholder;
            SemanticProperties.SetDescription(editor, text.DescriptionAccessibility);
        });
        return editor;
    }

    private View TimeFields(AppTextLocalized text)
    {
        var opens = new TimePicker { AutomationId = "OpeningTimePicker", TextColor = Ui.Cream, Format = "t" };
        opens.SetBinding(TimePicker.TimeProperty, static (HostEditorViewModel model) => model.OpensAt);
        var closes = new TimePicker { AutomationId = "ClosingTimePicker", TextColor = Ui.Cream, Format = "t" };
        closes.SetBinding(TimePicker.TimeProperty, static (HostEditorViewModel model) => model.ClosesAt);
        Localize(() =>
        {
            SemanticProperties.SetDescription(opens, text.OpeningTime);
            SemanticProperties.SetDescription(closes, text.ClosingTime);
            opens.Handler?.UpdateValue(nameof(ITimePicker.Format));
            closes.Handler?.UpdateValue(nameof(ITimePicker.Format));
        });
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 18 };
        grid.Add(new VerticalStackLayout { Children = { Text(() => text.Opens, 13, Ui.Muted), opens } });
        grid.Add(new VerticalStackLayout { Children = { Text(() => text.Closes, 13, Ui.Muted), closes } }, 1);
        return grid;
    }
}
