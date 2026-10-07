using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Menu;

public class EndOfMapVoteMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapCooldown _mapCooldown;
    private readonly MapChooserHudMenuService _hudMenu;

    public EndOfMapVoteMenu(ISwiftlyCore core, MapCooldown mapCooldown, MapChooserHudMenuService hudMenu)
    {
        _core = core;
        _mapCooldown = mapCooldown;
        _hudMenu = hudMenu;
    }

    public void Show(IPlayer player, List<string> mapsInVote, Action<IPlayer, string> onVote, bool disableExit = false)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var options = mapsInVote.Select(map =>
        {
            string displayName = map;
            bool isExtend = map == "map_chooser.extend_option";
            if (isExtend)
            {
                displayName = localizer["map_chooser.extend_option"];
            }

            return new MapChooserHudOption(
                displayName,
                isExtend || !_mapCooldown.IsMapInCooldown(map),
                p => onVote(p, map));
        }).ToList();

        // Port of upstream MapChooser v1.3.0's DisableVoteMenuExit: native menus always ship an
        // Exit entry unless disabled — mirror that on the HUD menu so players can dismiss the
        // vote without picking a map (the row costs one of the 8 option slots).
        if (!disableExit)
        {
            options.Add(new MapChooserHudOption(
                localizer["map_chooser.vote.exit"] ?? "Exit",
                Enabled: true,
                p => _hudMenu.Close(p)));
        }

        _hudMenu.Show(player, localizer["map_chooser.vote.title"] ?? "Vote for the next map", options, allowClose: !disableExit, capturesMouse: false);
    }
}
