using System.Collections.ObjectModel;
using BooRoute.Core;
using BooRoute.Core.Localization;

namespace BooRoute.ViewModels;

public sealed class HouseCard
{
    public House House { get; }
    public bool IsCollected { get; }
    public StampDesign Stamp { get; }
    public bool IsWelcoming { get; }
    public string Nickname => House.Nickname;
    public string Address => House.Address;
    public string ScareArt { get; }
    public string Hours { get; }
    public string Status { get; }
    public string Features { get; }
    public string PassportStatus { get; }
    public string EditDescription { get; }
    public string ViewDescription { get; }
    internal string CultureKey { get; }

    public HouseCard(House house, bool collected, DateTimeOffset localNow, AppText? text = null)
    {
        text ??= AppText.Default;
        CultureKey = CurrentCultureKey;
        House = house;
        IsCollected = collected;
        Stamp = StampGenerator.Generate(house.Id);
        IsWelcoming = house.IsWelcoming(localNow);
        ScareArt = house.ScareLevel switch
        {
            ScareLevel.Friendly => "scare_friendly.png",
            ScareLevel.Spooky => "scare_spooky.png",
            _ => "scare_scary.png"
        };
        Hours = text.Hours(house.OpensAt, house.ClosesAt);
        Status = house.Status switch
        {
            HouseStatus.OutOfTreats => text.Strings.OutOfTreats,
            HouseStatus.Closed => text.Strings.Closed,
            _ => IsWelcoming ? text.Strings.LightsOnWelcome : text.Strings.LightsOnOutsideHours
        };
        Features = string.Join(" · ", new[]
        {
            text.ScareName(house.ScareLevel),
            house.QuietVisits ? text.Strings.QuietVisits : null,
            house.StepFreeAccess ? text.Strings.StepFree : null
        }.Where(value => value is not null));
        PassportStatus = collected ? text.Strings.PassportStampCollected : "";
        EditDescription = text.Strings.EditHouseDescriptionFormat(house.Nickname);
        ViewDescription = text.Strings.ViewHouseDescriptionFormat(house.Nickname);
    }

    internal static string CurrentCultureKey =>
        $"{AppText.ResourceCulture.Name}|{AppText.FormattingCulture.Name}|{AppText.FormattingCulture.DateTimeFormat.ShortTimePattern}";
}

public sealed class HouseCardCollection : ObservableCollection<HouseCard>
{
    public void Update(IEnumerable<House> orderedHouses, IReadOnlySet<Guid> collected, DateTimeOffset localNow, AppText? text = null)
    {
        var cultureKey = HouseCard.CurrentCultureKey;
        var houses = orderedHouses.ToArray();
        var incoming = houses.ToDictionary(house => house.Id);
        var existing = this.ToDictionary(card => card.House.Id);

        for (var index = Count - 1; index >= 0; index--)
        {
            if (!incoming.ContainsKey(this[index].House.Id))
                RemoveAt(index);
        }

        for (var index = 0; index < houses.Length; index++)
        {
            var house = houses[index];
            var isCollected = collected.Contains(house.Id);
            existing.TryGetValue(house.Id, out var previous);
            var card = previous is not null && previous.House == house &&
                       previous.IsCollected == isCollected && previous.IsWelcoming == house.IsWelcoming(localNow) &&
                       previous.CultureKey == cultureKey
                ? previous
                : new HouseCard(house, isCollected, localNow, text);

            if (previous is null)
            {
                Insert(index, card);
                continue;
            }

            if (this[index].House.Id != house.Id)
                Move(IndexOf(previous), index);
            if (!ReferenceEquals(this[index], card))
                this[index] = card;
        }
    }
}
