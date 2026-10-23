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

    private const double IdentityCenterLatitude = 52.5140;
    private const double IdentityCenterLongitude = 13.3500;
    private const double IdentityLatitudeSpread = 0.0036;
    private const double IdentityLongitudeSpread = 0.0066;

    /// <summary>
    /// Returns a stable synthetic position based only on house identity.
    /// The app's map continues to use geocoded addresses.
    /// </summary>
    public static (double Latitude, double Longitude) LocationFor(House house) => LocationFor(house.Id);

    public static (double Latitude, double Longitude) LocationFor(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        var x = BitConverter.ToUInt32(bytes[..4]) / (double)uint.MaxValue;
        var y = BitConverter.ToUInt32(bytes[4..8]) / (double)uint.MaxValue;
        return (IdentityCenterLatitude + (y * 2 - 1) * IdentityLatitudeSpread,
                IdentityCenterLongitude + (x * 2 - 1) * IdentityLongitudeSpread);
    }

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
