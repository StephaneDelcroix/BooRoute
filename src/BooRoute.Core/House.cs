using BooRoute.Core.Localization;

namespace BooRoute.Core;

public enum HouseStatus { LightsOn, OutOfTreats, Closed }
public enum ScareLevel { Friendly, Spooky, Scary }

public sealed record House(
    Guid Id,
    string Nickname,
    string Address,
    string Description,
    TimeOnly OpensAt,
    TimeOnly ClosesAt,
    ScareLevel ScareLevel,
    HouseStatus Status,
    bool QuietVisits,
    bool StepFreeAccess)
{
    public bool IsWelcoming(DateTimeOffset localNow)
    {
        if (Status != HouseStatus.LightsOn || OpensAt == ClosesAt)
            return false;

        var time = TimeOnly.FromDateTime(localNow.DateTime);
        return OpensAt < ClosesAt
            ? time >= OpensAt && time < ClosesAt
            : time >= OpensAt || time < ClosesAt;
    }

    public IReadOnlyList<string> Validate()
    {
        var text = AppText.Default.Strings;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Nickname))
            errors.Add(text.NicknameRequired);
        else if (Nickname.Length > 60)
            errors.Add(text.NicknameTooLong);
        if (string.IsNullOrWhiteSpace(Address))
            errors.Add(text.AddressRequired);
        else if (Address.Length > 120)
            errors.Add(text.AddressTooLong);
        if (string.IsNullOrWhiteSpace(Description))
            errors.Add(text.DescriptionRequired);
        else if (Description.Length > 500)
            errors.Add(text.DescriptionTooLong);
        if (OpensAt == ClosesAt)
            errors.Add(text.HoursMustDiffer);
        if (!Enum.IsDefined(ScareLevel) || !Enum.IsDefined(Status))
            errors.Add(text.InvalidHouseOptions);
        if (Id == Guid.Empty)
            errors.Add(text.InvalidHouseId);
        return errors;
    }
}

public static class HouseFilters
{
    public static IEnumerable<House> Apply(
        IEnumerable<House> houses,
        DateTimeOffset localNow,
        bool welcomingOnly = false,
        ScareLevel? scareLevel = null,
        bool quietOnly = false,
        IReadOnlySet<Guid>? collectedOnly = null) =>
        houses.Where(house =>
            (!welcomingOnly || house.IsWelcoming(localNow)) &&
            (scareLevel is null || house.ScareLevel == scareLevel) &&
            (!quietOnly || house.QuietVisits) &&
            (collectedOnly is null || collectedOnly.Contains(house.Id)));
}
