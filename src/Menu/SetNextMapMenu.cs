using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Menu;

/// <summary>
/// HudKit-based "set next map" picker. Ported off the native SwiftlyS2 MenusAPI so every menu in
/// the plugin shares one input path (the panorama HUD) instead of fighting the built-in menu.
/// The full map list is paged by <see cref="MapChooserHudMenuService"/>.
/// </summary>
public class SetNextMapMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;
    private readonly MapChooserHudMenuService _hudMenu;

    public SetNextMapMenu(ISwiftlyCore core, MapLister mapLister, MapChooserHudMenuService hudMenu)
    {
        _core = core;
        _mapLister = mapLister;
        _hudMenu = hudMenu;
    }

    public void Show(IPlayer player, Action<IPlayer, string> onSelect)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        string title = "Set Next Map:";
        try { title = localizer["map_chooser.setnextmap.title"] ?? "Set Next Map:"; } catch { /* missing key */ }

        var currentMapName = _core.ConVar.FindAsString("mapname")?.ValueAsString;
        var options = _mapLister.Maps
            .Where(m => string.IsNullOrEmpty(currentMapName) || !m.Name.Equals(currentMapName, StringComparison.OrdinalIgnoreCase))
            .Select(m => new MapChooserHudOption(m.Name, Enabled: true, p => onSelect(p, m.Name)))
            .ToList();

        _hudMenu.Show(player, title, options);
    }
}