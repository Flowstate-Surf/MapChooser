using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Menu;

/// <summary>
/// HudKit-based !votemap picker. Ported off the native MenusAPI. Eligible maps (excluding the
/// current map, cooldowns, and player-count-gated entries) are paged by the HUD menu service.
/// </summary>
public class VotemapMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;
    private readonly MapCooldown _mapCooldown;
    private readonly NominationConfig _nominationConfig;
    private readonly MapChooserHudMenuService _hudMenu;

    public VotemapMenu(ISwiftlyCore core, MapLister mapLister, MapCooldown mapCooldown, NominationConfig nominationConfig, MapChooserHudMenuService hudMenu)
    {
        _core = core;
        _mapLister = mapLister;
        _mapCooldown = mapCooldown;
        _nominationConfig = nominationConfig;
        _hudMenu = hudMenu;
    }

    public void Show(IPlayer player, Action<IPlayer, string> onVote)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var currentMapName = _core.ConVar.FindAsString("mapname")?.ValueAsString;
        var playerCount = _core.PlayerManager.GetAllPlayers()
            .Count(p => p.IsValid && !p.IsFakeClient);

        var eligible = MapFilter.Apply(
            _mapLister.Maps.Where(m =>
                !(m.Id != null && !string.IsNullOrEmpty(currentMapName) && m.Id.Equals(currentMapName, StringComparison.OrdinalIgnoreCase))
                && m.IsValidForPlayerCount(playerCount)),
            _nominationConfig);

        // Cooldown maps stay visible but greyed out so players can see they exist.
        var options = eligible
            .Select(m => new MapChooserHudOption(
                m.Name,
                Enabled: !_mapCooldown.IsMapInCooldown(m),
                p => onVote(p, m.Name)))
            .ToList();

        _hudMenu.Show(player, localizer["map_chooser.votemap.title"] ?? "Vote for the next map:", options);
    }
}