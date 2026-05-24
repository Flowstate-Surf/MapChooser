using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using System.Threading.Tasks;

namespace MapChanger.Menu;

public class NominateMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;
    private readonly MapCooldown _mapCooldown;
    private readonly NominationConfig _nominationConfig;

    public NominateMenu(ISwiftlyCore core, MapLister mapLister, MapCooldown mapCooldown, NominationConfig nominationConfig)
    {
        _core = core;
        _mapLister = mapLister;
        _mapCooldown = mapCooldown;
        _nominationConfig = nominationConfig;
    }

    public void Show(IPlayer player, Action<IPlayer, string> onNominate)
    {
        string currentMapId;
        string? currentWorkshopId;
        try
        {
            currentMapId = _core.Engine?.GlobalVars.MapName.ToString() ?? "";
            currentWorkshopId = _core.Engine?.WorkshopId;
        }
        catch
        {
            currentMapId = "";
            currentWorkshopId = null;
        }
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
        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["map_chooser.nominate.tier_title"] ?? "Nominate — Select Tier:");

        for (int tier = 1; tier <= 6; tier++)
        {
            var mapsInTier = eligible.Where(m => m.Tier == tier).ToList();
            if (mapsInTier.Count == 0) continue;

            var tierOption = new ButtonMenuOption(
                $"<font color='yellow'>T{tier}</font>  <font color='gray'>({mapsInTier.Count})</font>")
            {
                Tag = mapsInTier
            };
            tierOption.Click += (sender, args) =>
            {
                if (sender is not IMenuOption opt || opt.Tag is not List<Map> tierMaps)
                    return ValueTask.CompletedTask;
                _core.Scheduler.NextTick(() =>
                {
                    _core.MenusAPI.CloseActiveMenu(args.Player);
                    ShowMapMenu(args.Player, tierMaps, onNominate);
                });
                return ValueTask.CompletedTask;
            };
            builder.AddOption(tierOption);
        }

        var allOption = new ButtonMenuOption(
            localizer["map_chooser.nominate.all_maps"] ?? "All Maps")
        {
            Tag = eligible
        };
        allOption.Click += (sender, args) =>
        {
            if (sender is not IMenuOption opt || opt.Tag is not List<Map> allMaps)
                return ValueTask.CompletedTask;
            _core.Scheduler.NextTick(() =>
            {
                _core.MenusAPI.CloseActiveMenu(args.Player);
                ShowMapMenu(args.Player, allMaps, onNominate);
            });
            return ValueTask.CompletedTask;
        };
        builder.AddOption(allOption);

        var menu = builder.Build();
        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    private void ShowMapMenu(IPlayer player, List<Map> maps, Action<IPlayer, string> onNominate)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["map_chooser.nominate.title"] ?? "Nominate a map:");

        foreach (var map in maps)
        {
            var option = new ButtonMenuOption($"<font color='lightgreen'>{map.Name}</font>");
            option.Click += (sender, args) =>
            {
                _core.Scheduler.NextTick(() => {
                    onNominate(args.Player, map.Name);
                    var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                    if (currentMenu != null)
                        _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                });
                return ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        var menu = builder.Build();
        _core.MenusAPI.OpenMenuForPlayer(player, menu);
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
