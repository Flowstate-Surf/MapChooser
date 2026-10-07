using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Menu;

/// <summary>
/// HudKit-based two-level nomination menu: a tier picker (T1..T6 that have eligible maps, plus
/// "All Maps") that drills into a paged map list. Ported off the native SwiftlyS2 MenusAPI so the
/// whole plugin shares one menu system.
/// </summary>
public class NominateMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;
    private readonly MapCooldown _mapCooldown;
    private readonly NominationConfig _nominationConfig;
    private readonly MapChooserHudMenuService _hudMenu;

    public NominateMenu(ISwiftlyCore core, MapLister mapLister, MapCooldown mapCooldown, NominationConfig nominationConfig, MapChooserHudMenuService hudMenu)
    {
        _core = core;
        _mapLister = mapLister;
        _mapCooldown = mapCooldown;
        _nominationConfig = nominationConfig;
        _hudMenu = hudMenu;
    }

    public void Show(IPlayer player, Action<IPlayer, string> onNominate)
    {
        _core.TryGetEngineSnapshot(out var currentMapId, out var currentWorkshopId, out _);
        var playerCount = _core.PlayerManager.GetAllPlayers()
            .Count(p => p.IsValid && !p.IsFakeClient);

        var eligible = _mapLister.Maps
            .Where(m => !IsCurrentMap(m, currentMapId, currentWorkshopId)
                     && !_mapCooldown.IsMapInCooldown(m)
                     && m.IsValidForPlayerCount(playerCount))
            .ToList();

        eligible = MapFilter.Apply(eligible, _nominationConfig);

        ShowTierMenu(player, eligible, onNominate);
    }

    private void ShowTierMenu(IPlayer player, List<Map> eligible, Action<IPlayer, string> onNominate)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var options = new List<MapChooserHudOption>();

        for (int tier = 1; tier <= 6; tier++)
        {
            var mapsInTier = eligible.Where(m => m.Tier == tier).ToList();
            if (mapsInTier.Count == 0) continue;

            int t = tier;
            options.Add(new MapChooserHudOption(
                $"T{t}  ({mapsInTier.Count})",
                Enabled: true,
                p => ShowMapMenu(p, eligible.Where(m => m.Tier == t).ToList(), onNominate)));
        }

        options.Add(new MapChooserHudOption(
            localizer["map_chooser.nominate.all_maps"] ?? "All Maps",
            Enabled: true,
            p => ShowMapMenu(p, eligible, onNominate)));

        _hudMenu.Show(player, localizer["map_chooser.nominate.tier_title"] ?? "Nominate — Select Tier:", options);
    }

    private void ShowMapMenu(IPlayer player, List<Map> maps, Action<IPlayer, string> onNominate)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var options = maps
            .Select(m => new MapChooserHudOption(m.Name, Enabled: true, p => onNominate(p, m.Name)))
            .ToList();

        _hudMenu.Show(
            player,
            localizer["map_chooser.nominate.title"] ?? "Nominate a map:",
            options,
            onBack: p => Show(p, onNominate));
    }

    private static bool IsCurrentMap(Map map, string? currentMapId, string? currentWorkshopId)
    {
        if (string.IsNullOrEmpty(currentMapId) && string.IsNullOrEmpty(currentWorkshopId)) return false;
        if (map.Id != null)
        {
            if (!string.IsNullOrEmpty(currentMapId) && map.Id.Equals(currentMapId, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(currentWorkshopId) && map.Id.Equals(currentWorkshopId, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}