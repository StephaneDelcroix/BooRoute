using System.Collections.ObjectModel;
using System.ComponentModel;
using BooRoute.Core;
using BooRoute.Core.Localization;

namespace BooRoute.ViewModels;

public sealed class HouseCard : INotifyPropertyChanged
{
    private readonly AppText text;
    public event PropertyChangedEventHandler? PropertyChanged;
    public House House { get; }
    public bool Visited { get; }
    public bool IsWelcoming { get; }
    public string Nickname => House.Nickname;
    public string Address => House.Address;
    public string ScareArt { get; }
    public string Hours { get; private set; } = "";
    public string Status { get; private set; } = "";
    public string Features { get; private set; } = "";
    public string VisitStatus { get; private set; } = "";
    public string EditDescription { get; private set; } = "";
    public string ViewDescription { get; private set; } = "";
    public string ViewButtonText => text.Strings.VisitDetails;
    public string EditButtonText => text.Strings.EditHouse;
    internal string CultureKey { get; private set; } = "";

    public HouseCard(House house, bool visited, DateTimeOffset localNow, AppText? text = null)
    {
        this.text = text ?? AppText.Default;
        House = house;
        Visited = visited;
        IsWelcoming = house.IsWelcoming(localNow);
        ScareArt = house.ScareLevel switch
        {
            ScareLevel.Friendly => "scare_friendly.png",
            ScareLevel.Spooky => "scare_spooky.png",
            _ => "scare_scary.png"
        };
        RefreshLocalization();
    }

    public void RefreshLocalization()
    {
        CultureKey = CurrentCultureKey;
        Hours = text.Hours(House.OpensAt, House.ClosesAt);
        Status = House.Status switch
        {
            HouseStatus.OutOfTreats => text.Strings.OutOfTreats,
            HouseStatus.Closed => text.Strings.Closed,
            _ => IsWelcoming ? text.Strings.LightsOnWelcome : text.Strings.LightsOnOutsideHours
        };
        Features = string.Join(" · ", new[]
        {
            text.ScareName(House.ScareLevel),
            House.QuietVisits ? text.Strings.QuietVisits : null,
            House.StepFreeAccess ? text.Strings.StepFree : null
        }.Where(value => value is not null));
        VisitStatus = Visited ? text.Strings.Visited : "";
        EditDescription = text.Strings.EditHouseDescriptionFormat(House.Nickname);
        ViewDescription = text.Strings.ViewHouseDescriptionFormat(House.Nickname);
        PropertyChanged?.Invoke(this, new(null));
    }

    internal static string CurrentCultureKey =>
        $"{AppText.ResourceCulture.Name}|{AppText.FormattingCulture.Name}|{AppText.FormattingCulture.DateTimeFormat.ShortTimePattern}";
}

public sealed class HouseCardCollection : ObservableCollection<HouseCard>
{
    public void Update(IEnumerable<House> orderedHouses, IReadOnlySet<Guid> visited, DateTimeOffset localNow, AppText? text = null)
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
            var isVisited = visited.Contains(house.Id);
            existing.TryGetValue(house.Id, out var previous);
            var card = previous is not null && previous.House == house &&
                       previous.Visited == isVisited && previous.IsWelcoming == house.IsWelcoming(localNow) &&
                       previous.CultureKey == cultureKey
                ? previous
                : new HouseCard(house, isVisited, localNow, text);

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
