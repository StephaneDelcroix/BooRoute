using System.Collections.ObjectModel;
using System.Windows.Input;
using BooRoute.Core;
using BooRoute.Core.Localization;
using BooRoute.Pages;
using Microsoft.Extensions.Logging;

namespace BooRoute.ViewModels;

public sealed class PassportStamp(House house, PassportEntry entry, TimeZoneInfo timeZone, AppText text)
{
    public House House { get; } = house;
    public string Nickname => House.Nickname;
    public StampPresentation Stamp { get; } = new(StampGenerator.Generate(house.Id), text);
    public string CollectedAt => entry.CollectedAt is { } date
        ? text.Strings.CollectedAtFormat(TimeZoneInfo.ConvertTime(date, timeZone).ToString("g", AppText.FormattingCulture))
        : text.Strings.PreviouslyCollected;
    public string Features => text.ScareName(House.ScareLevel) +
        (House.QuietVisits ? $" · {text.Strings.QuietVisits}" : "") +
        (House.StepFreeAccess ? $" · {text.Strings.StepFree}" : "");
    public string ViewDescription => text.Strings.ViewStampHouseFormat(Nickname);
}

public sealed class AchievementCard(PassportAchievement achievement, AppText text)
{
    public string Title => $"{(achievement.IsUnlocked ? "✓" : "🔒")} {achievement.Title}";
    public string Description => achievement.Description;
    public string Status => achievement.IsUnlocked ? text.Strings.Unlocked : text.Strings.Locked;
    public Color Color => achievement.IsUnlocked ? Ui.Mint : Ui.Muted;
}

public sealed class PassportViewModel : ViewModelBase
{
    private readonly INeighborhoodBackend backend;
    private readonly TimeProvider clock;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private string progressText;
    private double progress;
    public ObservableCollection<PassportStamp> Stamps { get; } = [];
    public ObservableCollection<AchievementCard> Achievements { get; } = [];
    public string ProgressText { get => progressText; private set => Set(ref progressText, value); }
    public double Progress { get => progress; private set => Set(ref progress, value); }
    public ICommand RefreshCommand { get; }
    public ICommand OpenHouseCommand { get; }

    public PassportViewModel(INeighborhoodBackend backend, TimeProvider clock, ILoggerFactory logging, AppText text) : base(logging, text)
    {
        this.backend = backend;
        this.clock = clock;
        progressText = Text.Strings.PassportFirstStamp;
        RefreshCommand = new AsyncCommand(_ => RefreshAsync());
        OpenHouseCommand = new AsyncCommand(parameter => RunAsync(async () =>
        {
            if (parameter is not PassportStamp stamp)
                throw new ArgumentException(Text.Strings.SelectStamp);
            await Shell.Current.Navigation.PushAsync(new HouseDetailsPage(new(backend, clock, logging, Text, stamp.House)));
        }));
        backend.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        await refreshGate.WaitAsync();
        try
        {
            await RunAsync(async () =>
            {
                var snapshot = await backend.GetSnapshotAsync();
                var houses = snapshot.Houses.ToDictionary(house => house.Id);
                Stamps.Clear();
                foreach (var entry in snapshot.Passport.OrderByDescending(entry => entry.CollectedAt))
                    Stamps.Add(new(houses[entry.HouseId], entry, clock.LocalTimeZone, Text));
                Achievements.Clear();
                foreach (var achievement in PassportAchievements.Evaluate(snapshot.Houses, snapshot.Passport))
                    Achievements.Add(new(achievement, Text));
                ProgressText = Text.Strings.PassportProgressFormat(Stamps.Count, houses.Count);
                Progress = houses.Count == 0 ? 0 : (double)Stamps.Count / houses.Count;
            });
        }
        finally
        {
            refreshGate.Release();
        }
    }
}
