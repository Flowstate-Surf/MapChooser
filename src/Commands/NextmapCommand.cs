using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Commands;

public class NextmapCommand
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly MapCycleManager? _cycleManager;
    private readonly MapChangerConfig? _config;

    public NextmapCommand(ISwiftlyCore core, PluginState state, MapCycleManager? cycleManager = null, MapChangerConfig? config = null)
    {
        _core = core;
        _state = state;
        _cycleManager = cycleManager;
        _config = config;
    }

    public void Execute(ICommandContext context)
    {
        if (!context.IsSentByPlayer) return;
        var player = context.Sender!;
        var localizer = _core.Translation.GetPlayerLocalizer(player);

        if (!string.IsNullOrEmpty(_state.NextMap))
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.next_map_decided", _state.NextMap]);
            return;
        }

        if (_cycleManager != null && _config?.Cycle.Enabled == true)
        {
            if (_config.Cycle.RandomOrder)
            {
                player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.cycle.next_map_random"]);
                return;
            }

            var nextMap = _cycleManager.PreviewNextMap();
            if (nextMap != null)
            {
                player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.cycle.next_map", nextMap.Name]);
                return;
            }
        }

        player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.next_map_not_decided"]);
    }
}
