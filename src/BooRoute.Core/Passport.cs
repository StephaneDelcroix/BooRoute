using BooRoute.Core.Localization;

namespace BooRoute.Core;

public sealed record PassportEntry(Guid HouseId, DateTimeOffset? CollectedAt);

public enum StampSymbol { Pumpkin, Ghost, Bat, Cat, Moon, Spider }
public enum StampPalette { Pumpkin, Lavender, Mint, Rose }
public enum StampAccent { Stars, Sparkles, Dots }
public sealed record StampDesign(StampSymbol Symbol, StampPalette Palette, StampAccent Accent);

public static class StampGenerator
{
    public static StampDesign Generate(Guid houseId)
    {
        // Network byte order makes the design stable across platforms and runtimes.
        Span<byte> bytes = stackalloc byte[16];
        houseId.TryWriteBytes(bytes, bigEndian: true, out _);
        var symbol = 0;
        var palette = 0;
        var accent = 0;
        for (var index = 0; index < bytes.Length; index++)
        {
            symbol = (symbol + bytes[index]) % 6;
            palette = (palette + bytes[index] * (index + 1)) % 4;
            accent = (accent + bytes[index] * (index + 1)) % 3;
        }
        return new((StampSymbol)symbol, (StampPalette)palette, (StampAccent)accent);
    }
}

public enum PassportAchievementKind { FirstKnock, SpookyExplorer, QuietNight }
public sealed record PassportAchievement(
    PassportAchievementKind Kind, string Title, string Description, bool IsUnlocked);

public static class PassportAchievements
{
    public static IReadOnlyList<PassportAchievement> Evaluate(
        IEnumerable<House> houses, IEnumerable<PassportEntry> passport)
    {
        var ids = passport.Select(entry => entry.HouseId).ToHashSet();
        var collected = houses.Where(house => ids.Contains(house.Id)).DistinctBy(house => house.Id).ToArray();
        var text = AppText.Default.Strings;
        return
        [
            new(PassportAchievementKind.FirstKnock, text.FirstKnock,
                text.FirstKnockDescription, collected.Length >= 1),
            new(PassportAchievementKind.SpookyExplorer, text.SpookyExplorer,
                text.SpookyExplorerDescription,
                Enum.GetValues<ScareLevel>().All(level => collected.Any(house => house.ScareLevel == level))),
            new(PassportAchievementKind.QuietNight, text.QuietNight,
                text.QuietNightDescription, collected.Count(house => house.QuietVisits) >= 2)
        ];
    }
}
