using BooRoute.Core.Localization;
using BooRoute.ViewModels;
using Microsoft.Extensions.Logging;

namespace BooRoute.Pages;

public sealed class SettingsPage : LocalizedPage
{
    public SettingsPage(LocaleSettings settings, AppText text, ILoggerFactory logging, Action applyLocale)
        : this(new SettingsViewModel(settings, text, logging, applyLocale), text)
    {
    }

    private SettingsPage(SettingsViewModel viewModel, AppText text) : base(viewModel)
    {
        var strings = text.Strings;
        BindingContext = viewModel;
        Localize(() => Title = strings.Settings);
        BackgroundColor = Ui.Midnight;
        SafeAreaEdges = SafeAreaEdges.Container;
        var heading = Text(() => strings.Settings, 29, bold: true);
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        var preview = Text(() => strings.LocalePreviewFormat(text.FormatTime(new TimeOnly(17, 15))), 16, Ui.Muted);
        preview.AutomationId = "LocalePreviewLabel";
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 18,
                Children =
                {
                    heading,
                    Text(() => strings.Locale, 18, bold: true),
                    Picker(() => strings.Locale, () => viewModel.Options,
                        Binding.Create(static (SettingsViewModel model) => model.SelectedIndex), "LocalePicker"),
                    preview,
                    Text(() => strings.LocaleNotice, 15, Ui.Muted),
                    Ui.Errors(),
                    Button(() => strings.ApplyLocale, "ApplyLocaleButton", viewModel.ApplyCommand)
                }
            }
        };
    }

    internal sealed class SettingsViewModel : ViewModelBase
    {
        private int selectedIndex;
        public string[] Options => [Text.Strings.SystemLocale, "English (Canada)", "Français (Canada)"];
        public int SelectedIndex { get => selectedIndex; set => Set(ref selectedIndex, value); }
        public System.Windows.Input.ICommand ApplyCommand { get; }

        public SettingsViewModel(LocaleSettings settings, AppText text, ILoggerFactory logging, Action applyLocale)
            : base(logging, text)
        {
            SelectedIndex = LocaleSettings.SupportedLocales.ToList().IndexOf(settings.SelectedLocale);
            ApplyCommand = new AsyncCommand(_ => RunAsync(() =>
            {
                if (SelectedIndex < 0 || SelectedIndex >= LocaleSettings.SupportedLocales.Count)
                    throw new ArgumentException("Choose a supported locale.");
                settings.Select(LocaleSettings.SupportedLocales[SelectedIndex]);
                applyLocale();
                return Task.CompletedTask;
            }));
        }
    }
}
