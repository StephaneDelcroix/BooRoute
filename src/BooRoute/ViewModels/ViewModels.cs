using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using BooRoute.Core;
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

public abstract class ViewModelBase(ILoggerFactory logging) : INotifyPropertyChanged
{
    private string errorMessage = "";
    private bool isBusy;
    public string ErrorMessage { get => errorMessage; protected set => Set(ref errorMessage, value); }
    public bool IsBusy { get => isBusy; private set => Set(ref isBusy, value); }
    public event PropertyChangedEventHandler? PropertyChanged;

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
        ErrorMessage = "";
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                      JsonException or ArgumentException or InvalidOperationException)
        {
            logging.CreateLogger(GetType()).LogError(error, "BooRoute action failed");
            ErrorMessage = $"That didn't work: {error.Message} Please try again.";
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
    private string summary = "Meet your demo neighborhood";

    public NeighborhoodMode Mode { get; }
    public HouseCardCollection Houses { get; } = [];
    public ICommand OpenHouseCommand { get; }
    public ICommand AddHouseCommand { get; }
    public ICommand RefreshCommand { get; }
    public string[] ScareOptions { get; } = ["Every scare level", "Friendly", "Spooky", "Scary"];
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
        ILoggerFactory logging, NeighborhoodMode mode) : base(logging)
    {
        this.backend = backend;
        this.clock = clock;
        this.logging = logging;
        Mode = mode;
        RefreshCommand = new AsyncCommand(_ => RefreshAsync());
        OpenHouseCommand = new AsyncCommand(parameter => RunAsync(async () =>
        {
            if (parameter is not HouseCard card)
                throw new ArgumentException("Select a house first.");
            Page page = Mode == NeighborhoodMode.Host
                ? new HostEditorPage(new(backend, clock, logging, card.House))
                : new HouseDetailsPage(new(backend, clock, logging, card.House));
            await Shell.Current.Navigation.PushAsync(page);
        }));
        AddHouseCommand = new AsyncCommand(_ => RunAsync(() =>
            Shell.Current.Navigation.PushAsync(new HostEditorPage(new(backend, clock, logging)))));
        backend.Changed += (_, _) => Application.Current?.Dispatcher.Dispatch(() => _ = RefreshAsync());
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
            snapshot.Visited, now);
        Summary = $"{Houses.Count} {(Houses.Count == 1 ? "house" : "houses")} · " +
                  $"{Houses.Count(card => card.IsWelcoming)} welcoming now";
    }
}

public sealed class HouseDetailsViewModel : ViewModelBase
{
    private bool visited;
    public House House { get; }
    public HouseCard Card { get; }
    public string VisitButtonText => visited ? "✓ Visited — tap to undo" : "Mark as visited";
    public ICommand ToggleVisitedCommand { get; }

    public HouseDetailsViewModel(INeighborhoodBackend backend, TimeProvider clock,
        ILoggerFactory logging, House house) : base(logging)
    {
        House = house;
        Card = new(house, false, clock.GetLocalNow());
        ToggleVisitedCommand = new AsyncCommand(_ => RunAsync(async () =>
        {
            await backend.SetVisitedAsync(House.Id, !visited);
            visited = !visited;
            Changed(nameof(VisitButtonText));
        }));
        Backend = backend;
    }

    private INeighborhoodBackend Backend { get; }
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

    public string Heading { get; }
    public string Nickname { get => nickname; set => Set(ref nickname, value); }
    public string Address { get => address; set => Set(ref address, value); }
    public string Description { get => description; set => Set(ref description, value); }
    public TimeSpan OpensAt { get => opensAt; set => Set(ref opensAt, value); }
    public TimeSpan ClosesAt { get => closesAt; set => Set(ref closesAt, value); }
    public int ScareIndex { get => scareIndex; set => Set(ref scareIndex, value); }
    public int StatusIndex { get => statusIndex; set => Set(ref statusIndex, value); }
    public bool QuietVisits { get => quietVisits; set => Set(ref quietVisits, value); }
    public bool StepFreeAccess { get => stepFreeAccess; set => Set(ref stepFreeAccess, value); }
    public string[] ScareOptions { get; } = ["Friendly", "Spooky", "Scary"];
    public string[] StatusOptions { get; } = ["Lights on", "Out of treats", "Closed"];
    public ICommand SaveCommand { get; }

    public HostEditorViewModel(INeighborhoodBackend backend, TimeProvider clock,
        ILoggerFactory logging, House? house = null) : base(logging)
    {
        id = house?.Id ?? Guid.NewGuid();
        Heading = house is null ? "Welcome the neighborhood." : "Keep your porch up to date.";
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
            var updated = new House(id, Nickname.Trim(), Address.Trim(), Description.Trim(),
                TimeOnly.FromTimeSpan(OpensAt), TimeOnly.FromTimeSpan(ClosesAt),
                (ScareLevel)ScareIndex, (HouseStatus)StatusIndex, QuietVisits, StepFreeAccess);
            var errors = updated.Validate();
            if (errors.Count != 0)
            {
                ErrorMessage = string.Join(Environment.NewLine, errors);
                return;
            }
            await backend.SaveHouseAsync(updated);
            await Shell.Current.Navigation.PopAsync();
        }));
    }
}
