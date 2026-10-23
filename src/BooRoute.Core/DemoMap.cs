namespace BooRoute.Core;

/// <summary>
/// Map settings for the demo neighborhood. Pins are placed by geocoding each house's
/// address in the app; this class only holds the platform-independent parts.
/// </summary>
public static class DemoMap
{
    // Microsoft Cologne, Holzmarkt 2a, where the seeded venues are clustered.
    public const double CenterLatitude = 50.9289;
    public const double CenterLongitude = 6.9636;
    public const double InitialRadiusMeters = 1400;

    // About 15 m, enough to tell two pins at the same entrance apart.
    public const double DuplicateOffsetDegrees = 0.00014;

    /// <summary>
    /// Nudges positions that share the same coordinates (for example two museums
    /// with one entrance address) into a small circle so every pin stays tappable.
    /// The first occurrence keeps its exact position.
    /// </summary>
    public static IReadOnlyList<(double Latitude, double Longitude)> SeparateDuplicates(
        IReadOnlyList<(double Latitude, double Longitude)> positions)
    {
        var result = new List<(double Latitude, double Longitude)>(positions.Count);
        var seen = new Dictionary<(double, double), int>();
        foreach (var position in positions)
        {
            var key = (Math.Round(position.Latitude, 6), Math.Round(position.Longitude, 6));
            seen.TryGetValue(key, out var count);
            seen[key] = count + 1;
            if (count == 0)
            {
                result.Add(position);
                continue;
            }
            var angle = count * Math.PI / 3;
            result.Add((position.Latitude + Math.Sin(angle) * DuplicateOffsetDegrees,
                        position.Longitude + Math.Cos(angle) * DuplicateOffsetDegrees * 1.6));
        }
        return result;
    }
}
