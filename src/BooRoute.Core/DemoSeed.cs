namespace BooRoute.Core;

public static class DemoSeed
{
    public static DemoState Create(DateTimeOffset localNow)
    {
        (string Name, string Address)[] locations =
        [
            ("Microsoft Cologne", "Holzmarkt 2a, 50676 Köln"),
            ("art'otel Cologne", "Holzmarkt 4, 50676 Köln"),
            ("Schokoladenmuseum", "Am Schokoladenmuseum 1a, 50678 Köln"),
            ("Deutsches Sport & Olympia Museum", "Im Zollhafen 1, 50678 Köln"),
            ("Trinitatiskirche", "Filzengraben 4, 50676 Köln"),
            ("NH Köln Altstadt", "Holzmarkt 47, 50676 Köln"),
            ("Motel One Köln-Altstadt", "Tel-Aviv-Straße 6, 50676 Köln"),
            ("Wasserturm Hotel Cologne", "Kaygasse 2, 50676 Köln"),
            ("Museum Schnütgen", "Cäcilienstraße 29-33, 50667 Köln"),
            ("Rautenstrauch-Joest-Museum", "Cäcilienstraße 29-33, 50667 Köln")
        ];
        var opens = TimeOnly.FromDateTime(localNow.AddHours(-2).DateTime);
        var closes = TimeOnly.FromDateTime(localNow.AddHours(4).DateTime);
        var houses = locations.Select((location, index) => new House(
            Guid.NewGuid(), location.Name, location.Address,
            $"Demo stop at {location.Name}. This is a real public venue, not a registered Halloween host. " +
            "Treats, welcoming hours, scare levels, quiet visits and accessibility are simulated. Do not visit for treats.",
            opens, closes, (ScareLevel)(index % 3),
            index is 3 or 7 ? HouseStatus.OutOfTreats : index == 9 ? HouseStatus.Closed : HouseStatus.LightsOn,
            index is 0 or 1 or 2 or 4 or 6, index % 3 != 1)).ToList();
        return new(houses, []);
    }
}
