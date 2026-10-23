using System.Text.Json;
using System.Text.Json.Serialization;

namespace BooRoute.Core;

public sealed record DemoState(List<House> Houses, HashSet<Guid> Visited);
public sealed record NeighborhoodSnapshot(IReadOnlyList<House> Houses, IReadOnlySet<Guid> Visited);

// Source-generated so saving works where reflection-based serialization is disabled (WebAssembly, trimming).
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DemoState))]
internal sealed partial class DemoStateJsonContext : JsonSerializerContext;

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
    Task SetVisitedAsync(Guid houseId, bool visited, CancellationToken cancellationToken = default);
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
            return EnsureValid(await JsonSerializer.DeserializeAsync(stream, DemoStateJsonContext.Default.DemoState, cancellationToken));
        }
    }

    internal static DemoState EnsureValid(DemoState? state)
    {
        if (state is null || state.Houses is null || state.Visited is null ||
            state.Houses.Any(house => house is null || house.Validate().Count != 0) ||
            state.Houses.Select(house => house.Id).Distinct().Count() != state.Houses.Count ||
            state.Visited.Any(id => !state.Houses.Any(house => house.Id == id)))
            throw new InvalidDataException("The saved demo data is invalid. It has not been overwritten.");
        return state;
    }

    public async Task SaveAsync(DemoState state, CancellationToken cancellationToken = default)
    {
        var temporaryPath = path + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, state, DemoStateJsonContext.Default.DemoState, cancellationToken);
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

public sealed class TextDemoStateStore(Func<string?> read, Action<string> write) : IDemoStateStore
{
    public Task<DemoState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = read();
        if (json is null)
            return Task.FromResult<DemoState?>(null);

        return Task.FromResult<DemoState?>(JsonDemoStateStore.EnsureValid(JsonSerializer.Deserialize(json, DemoStateJsonContext.Default.DemoState)));
    }

    public Task SaveAsync(DemoState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        write(JsonSerializer.Serialize(state, DemoStateJsonContext.Default.DemoState));
        return Task.CompletedTask;
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
            return new(current.Houses.ToArray(), new HashSet<Guid>(current.Visited));
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
            if (index < 0)
                houses.Add(house);
            else
                houses[index] = house;
            var updated = new DemoState(houses, new(current.Visited));
            await store.SaveAsync(updated, cancellationToken);
            state = updated;
        }
        finally
        {
            gate.Release();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetVisitedAsync(Guid houseId, bool visited, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = await GetStateAsync(cancellationToken);
            if (!current.Houses.Any(house => house.Id == houseId))
                throw new ArgumentException("That house is not in this demo neighborhood.", nameof(houseId));
            var visits = new HashSet<Guid>(current.Visited);
            if (visited)
                visits.Add(houseId);
            else
                visits.Remove(houseId);
            var updated = new DemoState(current.Houses.ToList(), visits);
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
