using MapChanger.Models;
using MapChanger.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Menu;

/// <summary>
/// HudKit-based admin map picker for the !map / !setmap command. Ported off the native MenusAPI.
/// The full map list is paged by <see cref="MapChooserHudMenuService"/>.
/// </summary>
public class AdminChangeMapMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;
    private readonly MapChooserHudMenuService _hudMenu;

    public AdminChangeMapMenu(ISwiftlyCore core, MapLister mapLister, MapChooserHudMenuService hudMenu)
    {
        _core = core;
        _mapLister = mapLister;
        _hudMenu = hudMenu;
    }

    public void Show(IPlayer player, Action<IPlayer, string> onChangeMap)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var options = _mapLister.Maps
            .Select(m => new MapChooserHudOption(m.Name, Enabled: true, p => onChangeMap(p, m.Name)))
            .ToList();

        _hudMenu.Show(player, localizer["map_chooser.change_map.title"] ?? "Change map to:", options);
    }
}