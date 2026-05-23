using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Commands;

public class UnRtvCommand
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly VoteManager _voteManager;
    private readonly EndOfMapVoteManager _eofManager;
    private readonly MapChangerConfig _config;

    public UnRtvCommand(ISwiftlyCore core, PluginState state, VoteManager voteManager, EndOfMapVoteManager eofManager, MapChangerConfig config)
    {
        _core = core;
        _state = state;
        _voteManager = voteManager;
        _eofManager = eofManager;
        _config = config;
    }

    public void Execute(ICommandContext context)
    {
        if (!_config.Rtv.Enabled) return;
        if (!context.IsSentByPlayer) return;

        var player = context.Sender!;
        var localizer = _core.Translation.GetPlayerLocalizer(player);

        if (_state.WarmupRunning && !_config.Rtv.EnabledInWarmup)
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.general.validation.warmup"]);
            return;
        }

        if (!_config.AllowSpectatorsToVote && player.Controller?.TeamNum == 1)
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.general.validation.spectator"]);
            return;
        }

        // If vote IS happening...
        // Just remove the vote. The result will be validated when the vote ends.
        
        if (_voteManager.RemoveVote(player.Slot))
        {
             player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.rtv.vote_removed"]);
             
             if (_state.EofVoteHappening)
             {
                 _eofManager.RemoveMapVote(player);
             }
        }
        else
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.rtv.not_voted"]);
        }
    }
}
