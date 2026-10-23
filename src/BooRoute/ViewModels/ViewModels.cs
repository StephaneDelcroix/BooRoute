using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using BooRoute.Core;
using BooRoute.Core.Localization;
using BooRoute.Pages;
using Microsoft.Extensions.Logging;

namespace BooRoute.ViewModels;

public sealed class AsyncCommand(Func<object?, Task> execute) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !running;

    public async void Execute(object? parameter)
    {
        if (running)
            return;
        running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await execute(parameter);
        }
        finally
        {
            running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public abstract class ViewModelBase(ILoggerFactory logging, AppText text) : INotifyPropertyChanged
{
    private Func<string>? errorMessage;
    private bool isBusy;
    public string ErrorMessage => errorMessage?.Invoke() ?? "";
    public bool IsBusy { get => isBusy; private set => Set(ref isBusy, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public AppText Text { get; } = text;
    public virtual void RefreshLocalization() => Changed(nameof(ErrorMessage));

    protected void SetError(Func<string>? message)
    {
        errorMessage = message;
        Changed(nameof(ErrorMessage));
    }

    protected void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Changed(name);
        return true;
    }

    protected async Task RunAsync(Func<Task> action)
    {
        SetError(null);
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                      JsonException or ArgumentException or InvalidOperationException)
        {
            logging.CreateLogger(GetType()).LogError(error, "BooRoute action failed");
            SetError(() => error switch
            {
                JsonException or InvalidDataException => Text.Strings.SavedDataInvalid,
                IOException or UnauthorizedAccessException => Text.Strings.StorageFailed,
                _ => Text.Strings.ActionFailed
            });
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public enum NeighborhoodMode { Explore, Visited, Host }

public sealed class NeighborhoodViewModel : ViewModelBase
{
    private readonly INeighborhoodBackend backend;
    private readonly TimeProvider clock;
    private readonly ILoggerFactory logging;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private NeighborhoodSnapshot? snapshot;
    private bool welcomingOnly;
    private bool quietOnly;
    private int scareIndex;
    private string summary;

    public NeighborhoodMode Mode { get; }
    public HouseCardCollection Houses { get; } = [];
    public ICommand OpenHouseCommand { get; }
    public ICommand AddHouseCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public string[] ScareOptions =>
        [Text.Strings.EveryScareLevel, .. Enum.GetValues<ScareLevel>().Select(Text.ScareName)];
    public string Summary { get => summary; private set => Set(ref summary, value); }
    public bool WelcomingOnly
    {
        get => welcomingOnly;
        set { if (Set(ref welcomingOnly, value)) ApplyFilters(); }
    }
    public bool QuietOnly
    {
        get => quietOnly;
        set { if (Set(ref quietOnly, value)) ApplyFilters(); }
    }
    public int ScareIndex
    {
        get => scareIndex;
        set { if (Set(ref scareIndex, value)) ApplyFilters(); }
    }

    public NeighborhoodViewModel(INeighborhoodBackend backend, TimeProvider clock,
        ILoggerFactory logging, AppText text, NeighborhoodMode mode) : base(logging, text)
    {
        this.backend = backend;
        this.clock = clock;
        this.logging = logging;
        Mode = mode;
        summary = Text.Strings.InitialSummary;
        RefreshCommand = new AsyncCommand(_ => RefreshAsync());
        OpenSettingsCommand = new AsyncCommand(_ => RunAsync(() => Shell.Current.GoToAsync("//settings")));
        OpenHouseCommand = new AsyncCommand(parameter => RunAsync(async () =>
        {
            if (parameter is not HouseCard card)
                throw new ArgumentException(Text.Strings.SelectHouse);
            Page page = Mode == NeighborhoodMode.Host
                ? new HostEditorPage(new(backend, clock, logging, Text, card.House))
                : new HouseDetailsPage(new(backend, clock, logging, Text, card.House));
            await Shell.Current.Navigation.PushAsync(page);
        }));
        AddHouseCommand = new AsyncCommand(_ => RunAsync(() =>
            Shell.Current.Navigation.PushAsync(new HostEditorPage(new(backend, clock, logging, Text)))));
        backend.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        await refreshGate.WaitAsync();
        try
        {
            await RunAsync(async () =>
            {
                snapshot = await backend.GetSnapshotAsync();
                ApplyFilters();
            });
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public override void RefreshLocalization()
    {
        base.RefreshLocalization();
        foreach (var card in Houses)
            card.RefreshLocalization();
        Summary = snapshot is null ? Text.Strings.InitialSummary
            : Text.Summary(Houses.Count, Houses.Count(card => card.IsWelcoming));
    }

    private void ApplyFilters()
    {
        if (snapshot is null)
            return;
        var now = clock.GetLocalNow();
        ScareLevel? scare = scareIndex is >= 1 and <= 3 ? (ScareLevel)(scareIndex - 1) : null;
        var filtered = HouseFilters.Apply(snapshot.Houses, now,
            Mode == NeighborhoodMode.Explore && WelcomingOnly,
            Mode == NeighborhoodMode.Explore ? scare : null,
            Mode == NeighborhoodMode.Explore && QuietOnly,
            Mode == NeighborhoodMode.Visited ? snapshot.Visited : null);
        Houses.Update(filtered.OrderByDescending(house => house.IsWelcoming(now)).ThenBy(house => house.Nickname),
            snapshot.Visited, now, Text);
        Summary = Text.Summary(Houses.Count, Houses.Count(card => card.IsWelcoming));
    }
}

public sealed class HouseDetailsViewModel : ViewModelBase
{
    private bool visited;
    public House House { get; }
    public HouseCard Card { get; }
    public string VisitButtonText => visited ? Text.Strings.UndoVisited : Text.Strings.MarkVisited;
    public ICommand ToggleVisitedCommand { get; }
    public event EventHandler? VisitMarked;

    public HouseDetailsViewModel(INeighborhoodBackend backend, TimeProvider clock,
        ILoggerFactory logging, AppText text, House house) : base(logging, text)
    {
        House = house;
        Card = new(house, false, clock.GetLocalNow(), Text);
        ToggleVisitedCommand = new AsyncCommand(_ => RunAsync(async () =>
        {
            await backend.SetVisitedAsync(House.Id, !visited);
            visited = !visited;
            Changed(nameof(VisitButtonText));
            if (visited)
                VisitMarked?.Invoke(this, EventArgs.Empty);
        }));
        Backend = backend;
    }

    private INeighborhoodBackend Backend { get; }
    public override void RefreshLocalization()
    {
        base.RefreshLocalization();
        Card.RefreshLocalization();
        Changed(nameof(VisitButtonText));
    }
    public Task LoadAsync() => RunAsync(async () =>
    {
        visited = (await Backend.GetSnapshotAsync()).Visited.Contains(House.Id);
        Changed(nameof(VisitButtonText));
    });
}

public sealed class HostEditorViewModel : ViewModelBase
{
    private readonly Guid id;
    private string nickname = "";
    private string address = "";
    private string description = "";
    private TimeSpan opensAt;
    private TimeSpan closesAt;
    private int scareIndex;
    private int statusIndex;
    private bool quietVisits;
    private bool stepFreeAccess;

    private readonly bool isNew;
    public string Heading => isNew ? Text.Strings.NewHouseHeading : Text.Strings.EditHouseHeading;
    public string Nickname { get => nickname; set => Set(ref nickname, value); }
    public string Address { get => address; set => Set(ref address, value); }
    public string Description { get => description; set => Set(ref description, value); }
    public TimeSpan OpensAt { get => opensAt; set => Set(ref opensAt, value); }
    public TimeSpan ClosesAt { get => closesAt; set => Set(ref closesAt, value); }
    public int ScareIndex { get => scareIndex; set => Set(ref scareIndex, value); }
    public int StatusIndex { get => statusIndex; set => Set(ref statusIndex, value); }
    public bool QuietVisits { get => quietVisits; set => Set(ref quietVisits, value); }
    public bool StepFreeAccess { get => stepFreeAccess; set => Set(ref stepFreeAccess, value); }
    public string[] ScareOptions => Enum.GetValues<ScareLevel>().Select(Text.ScareName).ToArray();
    public string[] StatusOptions => Enum.GetValues<HouseStatus>().Select(Text.StatusName).ToArray();
    public ICommand SaveCommand { get; }

    public HostEditorViewModel(INeighborhoodBackend backend, TimeProvider clock,
        ILoggerFactory logging, AppText text, House? house = null) : base(logging, text)
    {
        id = house?.Id ?? Guid.NewGuid();
        isNew = house is null;
        Nickname = house?.Nickname ?? "";
        Address = house?.Address ?? "";
        Description = house?.Description ?? "";
        OpensAt = house?.OpensAt.ToTimeSpan() ?? TimeOnly.FromDateTime(clock.GetLocalNow().AddHours(-1).DateTime).ToTimeSpan();
        ClosesAt = house?.ClosesAt.ToTimeSpan() ?? TimeOnly.FromDateTime(clock.GetLocalNow().AddHours(3).DateTime).ToTimeSpan();
        ScareIndex = (int)(house?.ScareLevel ?? ScareLevel.Friendly);
        StatusIndex = (int)(house?.Status ?? HouseStatus.LightsOn);
        QuietVisits = house?.QuietVisits ?? false;
        StepFreeAccess = house?.StepFreeAccess ?? false;
        SaveCommand = new AsyncCommand(_ => RunAsync(async () =>
        {
            var updated = EditedHouse();
            var errors = updated.Validate();
            if (errors.Count != 0)
            {
                SetError(() => string.Join(Environment.NewLine, EditedHouse().Validate()));
                return;
            }
            await backend.SaveHouseAsync(updated);
            await Shell.Current.Navigation.PopAsync();
        }));
    }

    private House EditedHouse() => new(id, Nickname.Trim(), Address.Trim(), Description.Trim(),
        TimeOnly.FromTimeSpan(OpensAt), TimeOnly.FromTimeSpan(ClosesAt),
        (ScareLevel)ScareIndex, (HouseStatus)StatusIndex, QuietVisits, StepFreeAccess);
}

public sealed record MapHouse(HouseCard Card, double Latitude, double Longitude);

/// <summary>Turns house addresses into map positions with the platform geocoder and caches the results.</summary>
public sealed class HouseLocator(ILogger<HouseLocator> logger)
{
    private readonly Dictionary<string, (double Latitude, double Longitude)?> cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<(double Latitude, double Longitude)?> LocateAsync(string address)
    {
        var key = address.Trim();
        if (cache.TryGetValue(key, out var cached))
            return cached;

        (double Latitude, double Longitude)? result = null;
        try
        {
            var location = (await Geocoding.Default.GetLocationsAsync(key))?.FirstOrDefault();
            if (location is not null)
                result = (location.Latitude, location.Longitude);
        }
        catch (Exception error) when (error is FeatureNotSupportedException or FeatureNotEnabledException or
                                      PermissionException or InvalidOperationException or IOException or
                                      HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(error, "Could not geocode a house address");
            return null; // Don't cache failures; the network may be back on the next refresh.
        }
        cache[key] = result;
        return result;
    }
}

public sealed class MapViewModel : ViewModelBase
{
    private readonly INeighborhoodBackend backend;
    private readonly TimeProvider clock;
    private readonly ILoggerFactory logging;
    private readonly HouseLocator locator;
    private string summary;
    private int missing;
    private bool loaded;

    public IReadOnlyList<MapHouse> Houses { get; private set; } = [];
    public string Summary { get => summary; private set => Set(ref summary, value); }
    public event EventHandler? HousesChanged;

    public MapViewModel(INeighborhoodBackend backend, TimeProvider clock, ILoggerFactory logging, AppText text) : base(logging, text)
    {
        this.backend = backend;
        this.clock = clock;
        this.logging = logging;
        summary = Text.Strings.MapLoading;
        locator = new HouseLocator(logging.CreateLogger<HouseLocator>());
        backend.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAsync());
    }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        var snapshot = await backend.GetSnapshotAsync();
        var now = clock.GetLocalNow();
        var cards = snapshot.Houses
            .OrderBy(house => house.Nickname)
            .Select(house => new HouseCard(house, snapshot.Visited.Contains(house.Id), now, Text))
            .ToList();

        var located = new List<(HouseCard Card, (double Latitude, double Longitude) Position)>();
        foreach (var card in cards)
        {
            if (await locator.LocateAsync(card.Address) is { } position)
                located.Add((card, position));
        }
        var positions = DemoMap.SeparateDuplicates(located.Select(item => item.Position).ToList());
        Houses = located.Select((item, index) => new MapHouse(item.Card, positions[index].Latitude, positions[index].Longitude)).ToList();

        missing = cards.Count - Houses.Count;
        loaded = true;
        RefreshLocalization();
        HousesChanged?.Invoke(this, EventArgs.Empty);
    });

    public override void RefreshLocalization()
    {
        base.RefreshLocalization();
        foreach (var house in Houses)
            house.Card.RefreshLocalization();
        Summary = !loaded ? Text.Strings.MapLoading
            : Text.Strings.MapSummaryFormat(Houses.Count, Houses.Count(house => house.Card.IsWelcoming)) +
              (missing > 0 ? Text.Strings.MapMissingFormat(missing) : "");
    }

    public Task OpenHouseAsync(House house) => RunAsync(() =>
        Shell.Current.Navigation.PushAsync(new HouseDetailsPage(new(backend, clock, logging, Text, house))));
}
