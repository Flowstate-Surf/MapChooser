using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using MapChanger.Menu;

namespace MapChanger.Commands;

public class AdminMapsVoteCommand
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly MapLister _mapLister;
    private readonly EndOfMapVoteManager _eofManager;
    private readonly MapChangerConfig _config;

    public AdminMapsVoteCommand(ISwiftlyCore core, PluginState state, MapLister mapLister, EndOfMapVoteManager eofManager, MapChangerConfig config)
    {
        _core = core;
        _state = state;
        _mapLister = mapLister;
        _eofManager = eofManager;
        _config = config;
    }

    public void Execute(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("This command can only be used by players.");
            return;
        }

        var player = context.Sender!;
        var menu = new AdminMapsVoteMenu(_core, _mapLister, _config.Nomination);
        menu.Show(player, (p, maps) =>
        {
            _eofManager.StartCustomVote(maps, _config.EndOfMap.VoteDuration, false);
        });
    }
}
