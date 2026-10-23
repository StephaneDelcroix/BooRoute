#if !IOS && !MACCATALYST && !ANDROID && !WINDOWS
using System.Globalization;
using System.Text.Json;

namespace BooRoute.ViewModels;

// Desktop and browser (Avalonia) have no platform geocoder, so they look addresses up with
// OpenStreetMap's Nominatim. Its usage policy allows at most one request per second and asks
// for an identifying User-Agent (browsers send their Referer instead). HouseLocator caches results.
public static class NominatimGeocoder
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset nextRequest = DateTimeOffset.MinValue;

    public static async Task<(double Latitude, double Longitude)?> LocateAsync(string address)
    {
        await Gate.WaitAsync();
        try
        {
            var wait = nextRequest - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait);

            using var stream = await Client.GetStreamAsync($"search?format=jsonv2&limit=1&q={Uri.EscapeDataString(address)}");
            using var results = await JsonDocument.ParseAsync(stream);
            if (results.RootElement.ValueKind != JsonValueKind.Array || results.RootElement.GetArrayLength() == 0)
                return null;

            var first = results.RootElement[0];
            return TryGetCoordinate(first, "lat", out var latitude) && TryGetCoordinate(first, "lon", out var longitude)
                ? (latitude, longitude)
                : null;
        }
        finally
        {
            nextRequest = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
            Gate.Release();
        }
    }

    private static bool TryGetCoordinate(JsonElement result, string name, out double value)
    {
        value = 0;
        return result.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String &&
               double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { BaseAddress = new Uri("https://nominatim.openstreetmap.org/") };
        if (!OperatingSystem.IsBrowser())
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BooRoute/1.0 (+https://github.com/StephaneDelcroix/BooRoute)");
        return client;
    }
}
#endif
