using MapChanger.Helpers;
using MapChanger.Models;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Menu;

/// <summary>
/// HudKit-based map-cycle management menu (view order, move maps up/down, remove). Ported off the
/// native MenusAPI. The main list is paged; per-map actions and the remove confirmation are
/// single-page sub-menus with a "◄ Back" row that re-opens the parent.
/// </summary>
public class CycleMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;
    private readonly MapCycleManager _cycleManager;
    private readonly MapChangerConfig _config;
    private readonly MapChooserHudMenuService _hudMenu;

    public CycleMenu(ISwiftlyCore core, MapLister mapLister, MapCycleManager cycleManager, MapChangerConfig config, MapChooserHudMenuService hudMenu)
    {
        _core = core;
        _mapLister = mapLister;
        _cycleManager = cycleManager;
        _config = config;
        _hudMenu = hudMenu;
    }

    public void Show(IPlayer player)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var maps = _mapLister.Maps;

        string currentMapName = _core.TryGetCurrentMapName();
        string currentWorkshopId = _core.TryGetWorkshopId();

        string modeKey = _config.Cycle.RandomOrder
            ? "map_chooser.cycle.mode_random"
            : "map_chooser.cycle.mode_sequential";
        string title = localizer["map_chooser.cycle.menu_title"] + " — " + localizer[modeKey];

        var options = new List<MapChooserHudOption>();
        if (maps.Count == 0)
        {
            options.Add(new MapChooserHudOption(localizer["map_chooser.cycle.no_maps"], Enabled: false, _ => { }));
        }
        else
        {
            for (int i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                bool isCurrent = map.Name.Equals(currentMapName, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(map.Id) && map.Id.Equals(currentMapName, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(map.Id) && !string.IsNullOrEmpty(currentWorkshopId) &&
                     map.Id.Equals(currentWorkshopId, StringComparison.OrdinalIgnoreCase));

                bool isNext = !_config.Cycle.RandomOrder && i == (_cycleManager.CycleIndex + 1) % maps.Count;

                string label = $"{i + 1}. {map.Name}";
                if (isCurrent) label += " ◄";
                else if (isNext) label += " →";

                var mapCapture = map;
                options.Add(new MapChooserHudOption(label, Enabled: true, p => ShowMapActions(p, mapCapture)));
            }
        }

        _hudMenu.Show(player, title, options);
    }

    private void ShowMapActions(IPlayer player, Map map)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var maps = _mapLister.Maps;
        int idx = maps.ToList().FindIndex(m => m.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase));

        var options = new List<MapChooserHudOption>();
        if (idx > 0)
        {
            options.Add(new MapChooserHudOption(localizer["map_chooser.cycle.action_move_up"], Enabled: true, p =>
            {
                _cycleManager.MoveMapUp(map.Name);
                Show(p);
            }));
        }
        if (idx >= 0 && idx < maps.Count - 1)
        {
            options.Add(new MapChooserHudOption(localizer["map_chooser.cycle.action_move_down"], Enabled: true, p =>
            {
                _cycleManager.MoveMapDown(map.Name);
                Show(p);
            }));
        }
        options.Add(new MapChooserHudOption(localizer["map_chooser.cycle.action_remove"], Enabled: true, p => ShowRemoveConfirm(p, map)));

        _hudMenu.Show(player, localizer["map_chooser.cycle.actions_title", map.Name], options, onBack: p => Show(p));
    }

    private void ShowRemoveConfirm(IPlayer player, Map map)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);

        var options = new List<MapChooserHudOption>
        {
            new MapChooserHudOption(localizer["map_chooser.cycle.remove_confirm_yes"], Enabled: true, p =>
            {
                _hudMenu.Close(p);
                bool removed = _cycleManager.RemoveMap(map.Name);
                if (removed)
                    _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.cycle.map_removed", map.Name]);
            }),
            new MapChooserHudOption(localizer["map_chooser.cycle.remove_cancel"], Enabled: true, p => ShowMapActions(p, map))
        };

        _hudMenu.Show(player, localizer["map_chooser.cycle.remove_confirm_title", map.Name], options, onBack: p => ShowMapActions(p, map));
    }
}