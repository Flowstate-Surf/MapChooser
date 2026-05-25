using System.Linq;
using System.Threading;
using MapChanger.Dependencies;
using MapChanger.Models;
using SwiftlyS2.Shared;

namespace MapChanger.Helpers;

/// <summary>
/// Reloads the current map on a periodic timer when the server is empty.
/// Fixes CS2 movement desync on long-running 24/7 servers
/// (sv_hibernate_when_empty 0).
/// Ported from ModSharp WSMaps by yappershq/ms-wsmaps.
/// </summary>
public class EmptyMapSwitcher
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly MapChangerConfig _config;
    private CancellationTokenSource? _timer;

    public EmptyMapSwitcher(ISwiftlyCore core, PluginState state, MapChangerConfig config)
    {
        _core = core;
        _state = state;
        _config = config;
    }

    public void Start(int intervalSeconds)
    {
        _timer?.Cancel();
        _timer = _core.Scheduler.DelayAndRepeatBySeconds(
            intervalSeconds,
            intervalSeconds,
            CheckAndReload);
        _core.Scheduler.StopOnMapChange(_timer);
    }

    public void Stop()
    {
        _timer?.Cancel();
        _timer = null;
    }

    private void CheckAndReload()
    {
        // Don't interfere with an in-progress map change.
        if (_state.MapChangeScheduled || _state.ChangeMapImmediately || _state.MatchEnded)
            return;

        try
        {
            var playerCount = _core.PlayerManager.GetAllPlayers()
                .Count(p => p.IsValid && !p.IsFakeClient);

            if (playerCount > 0)
                return;

            if (_core.Engine == null)
                return;

            // Mark immediately so the next timer tick doesn't double-fire.
            _state.ChangeMapImmediately = true;

            var workshopId = _state.CurrentWorkshopId;
            var mapId = _state.CurrentMapId;

            if (!string.IsNullOrEmpty(workshopId))
            {
                if (!string.IsNullOrEmpty(_config.MapGroup))
                    _core.Engine.ExecuteCommand($"sv_mapgroup {_config.MapGroup}");
                _core.Engine.ExecuteCommand($"host_workshop_map {workshopId}");
            }
            else if (!string.IsNullOrEmpty(mapId))
                _core.Engine.ExecuteCommand($"changelevel {mapId}");
            else
                _state.ChangeMapImmediately = false; // nothing to reload, reset flag
        }
        catch
        {
            _state.ChangeMapImmediately = false;
        }
    }
}
