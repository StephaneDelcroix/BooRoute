using System.Text;
using System.Text.Json;

namespace BooRoute.Core.Tests;

public sealed class SavedStateValidationTests
{
    private static House Sample() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Pumpkin Porch", "13 Make-Believe Lane", "Friendly pumpkins.",
        new(17, 0), new(21, 0), ScareLevel.Friendly, HouseStatus.LightsOn, true, true);

    public static IEnumerable<object[]> InvalidSavedStates()
    {
        var house = Sample();
        yield return ["null"];
        yield return ["""{"Houses":[],"Visited":null}"""];
        yield return ["""{"Houses":[null],"Visited":[]}"""];
        yield return ["""{"Version":1,"Houses":null,"Passport":[]}"""];
        yield return ["""{"Version":1,"Houses":[],"Passport":null}"""];
        yield return ["""{"Version":1,"Houses":[],"Passport":[null]}"""];
        yield return ["""{"Version":2,"Houses":[],"Passport":[]}"""];
        yield return ["""{"Version":null,"Houses":[],"Visited":[]}"""];
        yield return ["""{"Houses":[],"Passport":[]}"""];
        yield return [Current([house with { Nickname = "" }], [])];
        yield return [Current([house with { Id = Guid.Empty }], [])];
        yield return [Current([house, house with { Nickname = "Another porch" }], [])];
        yield return [Current([house], [new(Guid.NewGuid(), null)])];
        yield return [Current([house], [new(house.Id, null), new(house.Id, DateTimeOffset.UtcNow)])];
        yield return [JsonSerializer.Serialize(new { Houses = new[] { house }, Visited = new[] { house.Id, house.Id } })];
        yield return [JsonSerializer.Serialize(new { Houses = new[] { house }, Visited = new[] { Guid.NewGuid() } })];
        yield return [JsonSerializer.Serialize(new { Houses = new[] { house, house }, Visited = Array.Empty<Guid>() })];
    }

    private static string Current(List<House> houses, List<PassportEntry> passport) =>
        JsonSerializer.Serialize(new { Version = 1, Houses = houses, Passport = passport });

    [Theory]
    [MemberData(nameof(InvalidSavedStates))]
    public async Task InvalidSavedStateIsRejectedWithoutChangingSavedBytes(string json)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"booroute-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");
        try
        {
            var store = new JsonDemoStateStore(path);
            var bytes = Encoding.UTF8.GetBytes(json);
            await File.WriteAllBytesAsync(path, bytes);
            await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LegacyMigrationDoesNotRewriteUntilSuccessfulMutation()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"booroute-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");
        try
        {
            var store = new JsonDemoStateStore(path);
            var house = Sample();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Houses = new[] { house }, Visited = new[] { house.Id } });
            await File.WriteAllBytesAsync(path, bytes);
            var backend = new MockNeighborhoodBackend(store, TimeProvider.System);
            var loaded = await backend.GetSnapshotAsync();
            Assert.Equal(house, Assert.Single(loaded.Houses));
            Assert.Equal(new PassportEntry(house.Id, null), Assert.Single(loaded.Passport));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            await backend.SetStampCollectedAsync(house.Id, true);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            await Assert.ThrowsAsync<ArgumentException>(() => backend.SetStampCollectedAsync(Guid.NewGuid(), true));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));

            await backend.SaveHouseAsync(house with { Nickname = "Updated porch" });
            using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(path));
            Assert.Equal(1, json.RootElement.GetProperty("Version").GetInt32());
            Assert.False(json.RootElement.TryGetProperty("Visited", out _));
            Assert.Equal(new PassportEntry(house.Id, null), Assert.Single((await store.LoadAsync())!.Passport));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CurrentSchemaRoundTripsWithExactTimestampAndNoReadRewrite()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"booroute-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");
        try
        {
            var store = new JsonDemoStateStore(path);
            var house = Sample();
            var second = house with { Id = Guid.NewGuid() };
            var entry = new PassportEntry(house.Id, new(2026, 10, 31, 19, 23, 45, TimeSpan.FromHours(2)));
            var state = new DemoState([house, second], [entry, new(second.Id, null)]);
            await store.SaveAsync(state);
            var bytes = await File.ReadAllBytesAsync(path);
            using var json = JsonDocument.Parse(bytes);
            Assert.Equal(1, json.RootElement.GetProperty("Version").GetInt32());
            Assert.False(json.RootElement.TryGetProperty("Visited", out _));
            var loaded = await store.LoadAsync();
            Assert.NotNull(loaded);
            Assert.Equal(state.Houses, loaded.Houses);
            Assert.Equal(state.Passport, loaded.Passport);
            Assert.Equal(entry.CollectedAt!.Value.Offset, loaded.Passport[0].CollectedAt!.Value.Offset);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CancelledSavePreservesOriginalBytesAndCleansStagingFile()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"booroute-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");
        try
        {
            var store = new JsonDemoStateStore(path);
            var house = Sample();
            await store.SaveAsync(new([house], []));
            var bytes = await File.ReadAllBytesAsync(path);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                store.SaveAsync(new([house], [new(house.Id, null)]), cancellation.Token));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SavedStateUsesIndentedVersionedJsonAndLoadsLegacyFormat()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"booroute-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");
        const string json = """
            {
              "Houses": [
                {
                  "Id": "11111111-1111-1111-1111-111111111111",
                  "Nickname": "Pumpkin Porch",
                  "Address": "13 Make-Believe Lane",
                  "Description": "Friendly pumpkins.",
                  "OpensAt": "17:00:00",
                  "ClosesAt": "21:00:00",
                  "ScareLevel": 0,
                  "Status": 0,
                  "QuietVisits": true,
                  "StepFreeAccess": true
                }
              ],
              "Visited": [
                "11111111-1111-1111-1111-111111111111"
              ]
            }
            """;
        try
        {
            var house = Sample();
            var store = new JsonDemoStateStore(path);
            await store.SaveAsync(new DemoState([house], [new(house.Id, null)]));

            var currentJson = json.Replace("{\n  \"Houses\"", "{\n  \"Version\": 1,\n  \"Houses\"")
                .Replace("\"Visited\": [\n    \"11111111-1111-1111-1111-111111111111\"\n  ]",
                    "\"Passport\": [\n    {\n      \"HouseId\": \"11111111-1111-1111-1111-111111111111\",\n      \"CollectedAt\": null\n    }\n  ]");
            Assert.Equal(currentJson.ReplaceLineEndings("\n"),
                (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n"));
            Assert.False(File.Exists(path + ".tmp"));

            await File.WriteAllTextAsync(path, json);
            var loaded = await new JsonDemoStateStore(path).LoadAsync();

            Assert.NotNull(loaded);
            Assert.Equal(house, Assert.Single(loaded.Houses));
            Assert.Equal(new PassportEntry(house.Id, null), Assert.Single(loaded.Passport));
            Assert.Equal(json, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
