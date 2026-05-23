using MapChanger.Models;

namespace MapChanger.Helpers;

public static class MapFilter
{
    public static List<Map> Apply(IEnumerable<Map> maps, NominationConfig config)
    {
        var result = maps.AsEnumerable();

        if (!string.IsNullOrEmpty(config.MapType))
        {
            var prefix = config.MapType.TrimEnd('_') + "_";
            result = result.Where(m =>
                (m.Id?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                || m.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        var includeTiers = ParseIncludeTiers(config.IncludeTiers);
        if (includeTiers.Count > 0)
            result = result.Where(m => includeTiers.Contains(m.Tier));

        return result.ToList();
    }

    private static HashSet<int> ParseIncludeTiers(string? includeTiers)
    {
        if (string.IsNullOrWhiteSpace(includeTiers)) return new HashSet<int>();
        return includeTiers
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : -1)
            .Where(n => n >= 0)
            .ToHashSet();
    }
}
