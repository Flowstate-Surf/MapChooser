using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using System.Threading.Tasks;

namespace MapChanger.Menu;

public class VotemapMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;
    private readonly MapCooldown _mapCooldown;
    private readonly NominationConfig _nominationConfig;

    public VotemapMenu(ISwiftlyCore core, MapLister mapLister, MapCooldown mapCooldown, NominationConfig nominationConfig)
    {
        _core = core;
        _mapLister = mapLister;
        _mapCooldown = mapCooldown;
        _nominationConfig = nominationConfig;
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
                && !_mapCooldown.IsMapInCooldown(m)
                && m.IsValidForPlayerCount(playerCount)),
            _nominationConfig);

        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["map_chooser.votemap.title"] ?? "Vote for the next map:");
        foreach (var map in eligible)
        {
            var option = new ButtonMenuOption($"<font color='lightgreen'>{map.Name}</font>");
            option.Click += (sender, args) =>
            {
                _core.Scheduler.NextTick(() => {
                    onVote(args.Player, map.Name);
                    var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                    if (currentMenu != null)
                    {
                        _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                    }
                });
                return ValueTask.CompletedTask;
            };

            builder.AddOption(option);
        }

        var menu = builder.Build();
        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }
}
