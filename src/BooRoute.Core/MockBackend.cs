using System.Text.Json;
using BooRoute.Core.Localization;

namespace BooRoute.Core;

public sealed record DemoState(List<House> Houses, List<PassportEntry> Passport);
public sealed record NeighborhoodSnapshot(IReadOnlyList<House> Houses, IReadOnlyList<PassportEntry> Passport);

public interface IDemoStateStore
{
    Task<DemoState?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(DemoState state, CancellationToken cancellationToken = default);
}

public interface INeighborhoodBackend
{
    event EventHandler? Changed;
    Task<NeighborhoodSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task SaveHouseAsync(House house, CancellationToken cancellationToken = default);
    Task SetStampCollectedAsync(Guid houseId, bool collected, CancellationToken cancellationToken = default);
}

public sealed class JsonDemoStateStore : IDemoStateStore
{
    private readonly string path;

    public JsonDemoStateStore(string path)
    {
        this.path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
    }

    public async Task<DemoState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        FileStream stream;
        try
        {
            stream = File.OpenRead(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        await using (stream)
        {
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                throw InvalidState();
            var document = json.RootElement.Deserialize(DemoJsonContext.Default.SavedDemoDocument)!;
            List<PassportEntry>? passport;
            if (json.RootElement.TryGetProperty("Version", out _))
            {
                if (document.Version != 1)
                    throw InvalidState();
                passport = document.Passport;
            }
            else
            {
                passport = document.Visited?.Select(id => new PassportEntry(id, null)).ToList();
            }
            var state = new DemoState(document.Houses!, passport!);
            Validate(state);
            return state;
        }
    }

    private static InvalidDataException InvalidState() =>
        new(AppText.Default.Strings.SavedDataInvalid);

    private static void Validate(DemoState state)
    {
        if (state.Houses is null || state.Passport is null ||
            state.Houses.Any(house => house is null || house.Validate().Count != 0) ||
            state.Houses.Select(house => house.Id).Distinct().Count() != state.Houses.Count ||
            state.Passport.Any(entry => entry is null) ||
            state.Passport.Select(entry => entry.HouseId).Distinct().Count() != state.Passport.Count ||
            state.Passport.Any(entry => !state.Houses.Any(house => house.Id == entry.HouseId)))
            throw InvalidState();
    }

    public async Task SaveAsync(DemoState state, CancellationToken cancellationToken = default)
    {
        Validate(state);
        var temporaryPath = path + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream,
                    new CurrentDemoDocument(1, state.Houses, state.Passport),
                    DemoJsonContext.Default.CurrentDemoDocument, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}

public sealed class MockNeighborhoodBackend(IDemoStateStore store, TimeProvider clock) : INeighborhoodBackend
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DemoState? state;
    public event EventHandler? Changed;

    private async Task<DemoState> GetStateAsync(CancellationToken cancellationToken)
    {
        if (state is not null)
            return state;

        var saved = await store.LoadAsync(cancellationToken);
        if (saved is not null)
            return state = saved;

        var seeded = DemoSeed.Create(clock.GetLocalNow());
        await store.SaveAsync(seeded, cancellationToken);
        return state = seeded;
    }

    public async Task<NeighborhoodSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = await GetStateAsync(cancellationToken);
            return new(current.Houses.ToArray(), current.Passport.ToArray());
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveHouseAsync(House house, CancellationToken cancellationToken = default)
    {
        var errors = house.Validate();
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(house));

        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = await GetStateAsync(cancellationToken);
            var houses = current.Houses.ToList();
            var index = houses.FindIndex(existing => existing.Id == house.Id);
            if (index >= 0 && houses[index] == house)
                return;
            if (index < 0)
                houses.Add(house);
            else
                houses[index] = house;
            var updated = new DemoState(houses, new(current.Passport));
            await store.SaveAsync(updated, cancellationToken);
            state = updated;
        }
        finally
        {
            gate.Release();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetStampCollectedAsync(Guid houseId, bool collected, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = await GetStateAsync(cancellationToken);
            if (!current.Houses.Any(house => house.Id == houseId))
                throw new ArgumentException(AppText.Default.Strings.HouseMissing, nameof(houseId));
            var passport = current.Passport.ToList();
            var existing = passport.FindIndex(entry => entry.HouseId == houseId);
            if (collected == (existing >= 0))
                return;
            if (collected)
                passport.Add(new(houseId, clock.GetUtcNow()));
            else
                passport.RemoveAt(existing);
            var updated = new DemoState(current.Houses.ToList(), passport);
            await store.SaveAsync(updated, cancellationToken);
            state = updated;
        }
        finally
        {
            gate.Release();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
