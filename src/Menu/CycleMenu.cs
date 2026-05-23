using MapChanger.Helpers;
using MapChanger.Models;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Menu;

public class CycleMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;
    private readonly MapCycleManager _cycleManager;
    private readonly MapChangerConfig _config;

    public CycleMenu(ISwiftlyCore core, MapLister mapLister, MapCycleManager cycleManager, MapChangerConfig config)
    {
        _core = core;
        _mapLister = mapLister;
        _cycleManager = cycleManager;
        _config = config;
    }

    public void Show(IPlayer player)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var maps = _mapLister.Maps;

        var currentMapName = _core.Engine?.GlobalVars.MapName.ToString() ?? "";
        var currentWorkshopId = _core.Engine?.WorkshopId ?? "";

        string modeKey = _config.Cycle.RandomOrder
            ? "map_chooser.cycle.mode_random"
            : "map_chooser.cycle.mode_sequential";

        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["map_chooser.cycle.menu_title"] + " — " + localizer[modeKey]);

        if (maps.Count == 0)
        {
            var emptyOption = new ButtonMenuOption(localizer["map_chooser.cycle.no_maps"]);
            builder.AddOption(emptyOption);
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
                if (isCurrent) label = $"<font color='yellow'>{label} ◄</font>";
                else if (isNext) label = $"<font color='lightgreen'>{label} →</font>";
                else label = $"<font color='white'>{label}</font>";

                var mapCapture = map;
                var option = new ButtonMenuOption(label);
                option.Click += (sender, args) =>
                {
                    _core.Scheduler.NextTick(() =>
                    {
                        var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                        if (currentMenu != null)
                            _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                        ShowMapActions(args.Player, mapCapture);
                    });
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                };
                builder.AddOption(option);
            }
        }

        var menu = builder.Build();
        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    private void ShowMapActions(IPlayer player, Map map)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var maps = _mapLister.Maps;
        int idx = maps.ToList().FindIndex(m => m.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase));

        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["map_chooser.cycle.actions_title", map.Name]);

        if (idx > 0)
        {
            var upOption = new ButtonMenuOption($"<font color='lightblue'>{localizer["map_chooser.cycle.action_move_up"]}</font>");
            upOption.Click += (sender, args) =>
            {
                _core.Scheduler.NextTick(() =>
                {
                    _cycleManager.MoveMapUp(map.Name);
                    var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                    if (currentMenu != null)
                        _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                    Show(args.Player);
                });
                return System.Threading.Tasks.ValueTask.CompletedTask;
            };
            builder.AddOption(upOption);
        }

        if (idx >= 0 && idx < maps.Count - 1)
        {
            var downOption = new ButtonMenuOption($"<font color='lightblue'>{localizer["map_chooser.cycle.action_move_down"]}</font>");
            downOption.Click += (sender, args) =>
            {
                _core.Scheduler.NextTick(() =>
                {
                    _cycleManager.MoveMapDown(map.Name);
                    var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                    if (currentMenu != null)
                        _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                    Show(args.Player);
                });
                return System.Threading.Tasks.ValueTask.CompletedTask;
            };
            builder.AddOption(downOption);
        }

        var removeOption = new ButtonMenuOption($"<font color='red'>{localizer["map_chooser.cycle.action_remove"]}</font>");
        removeOption.Click += (sender, args) =>
        {
            _core.Scheduler.NextTick(() =>
            {
                var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                if (currentMenu != null)
                    _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                ShowRemoveConfirm(args.Player, map);
            });
            return System.Threading.Tasks.ValueTask.CompletedTask;
        };
        builder.AddOption(removeOption);

        var backOption = new ButtonMenuOption($"<font color='grey'>{localizer["map_chooser.cycle.action_back"]}</font>");
        backOption.Click += (sender, args) =>
        {
            _core.Scheduler.NextTick(() =>
            {
                var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                if (currentMenu != null)
                    _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                Show(args.Player);
            });
            return System.Threading.Tasks.ValueTask.CompletedTask;
        };
        builder.AddOption(backOption);

        var menu = builder.Build();
        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    private void ShowRemoveConfirm(IPlayer player, Map map)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);

        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["map_chooser.cycle.remove_confirm_title", map.Name]);

        var yesOption = new ButtonMenuOption($"<font color='red'>{localizer["map_chooser.cycle.remove_confirm_yes"]}</font>");
        yesOption.Click += (sender, args) =>
        {
            _core.Scheduler.NextTick(() =>
            {
                var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                if (currentMenu != null)
                    _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);

                bool removed = _cycleManager.RemoveMap(map.Name);
                if (removed)
                    _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.cycle.map_removed", map.Name]);
            });
            return System.Threading.Tasks.ValueTask.CompletedTask;
        };

        var cancelOption = new ButtonMenuOption($"<font color='lightgreen'>{localizer["map_chooser.cycle.remove_cancel"]}</font>");
        cancelOption.Click += (sender, args) =>
        {
            _core.Scheduler.NextTick(() =>
            {
                var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                if (currentMenu != null)
                    _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                Show(args.Player);
            });
            return System.Threading.Tasks.ValueTask.CompletedTask;
        };

        builder.AddOption(yesOption);
        builder.AddOption(cancelOption);

        var menu = builder.Build();
        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }
}
