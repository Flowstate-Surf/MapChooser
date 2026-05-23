using MapChanger.Models;
using MapChanger.Helpers;
using MapChanger.Dependencies;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Commands;

public class RevoteCommand
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly EndOfMapVoteManager _eofManager;
    private readonly MapChangerConfig _config;

    public RevoteCommand(ISwiftlyCore core, PluginState state, EndOfMapVoteManager eofManager, MapChangerConfig config)
    {
        _core = core;
        _state = state;
        _eofManager = eofManager;
        _config = config;
    }

    public void Execute(ICommandContext context)
    {
        if (!context.IsSentByPlayer) return;

        var player = context.Sender!;
        var localizer = _core.Translation.GetPlayerLocalizer(player);

        if (!_config.AllowSpectatorsToVote && player.Controller?.TeamNum == 1)
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.general.validation.spectator"]);
            return;
        }

        if (!_state.EofVoteHappening)
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.revote.no_vote_active"]);
            return;
        }

        _eofManager.OpenVoteMenu(player);
    }
}
