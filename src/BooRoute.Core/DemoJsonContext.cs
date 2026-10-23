using System.Text.Json.Serialization;

namespace BooRoute.Core;

internal sealed record SavedDemoDocument(int? Version, List<House>? Houses,
    List<PassportEntry>? Passport, List<Guid>? Visited);
internal sealed record CurrentDemoDocument(int Version, List<House> Houses, List<PassportEntry> Passport);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DemoState))]
[JsonSerializable(typeof(SavedDemoDocument))]
[JsonSerializable(typeof(CurrentDemoDocument))]
internal partial class DemoJsonContext : JsonSerializerContext
{
}
