using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Shared;

namespace MapChanger.Helpers;

public class ChangeMapManager
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly MapLister _mapLister;
    private readonly MapChangerConfig _config;

    public ChangeMapManager(ISwiftlyCore core, PluginState state, MapLister mapLister, MapChangerConfig config)
    {
        _core = core;
        _state = state;
        _mapLister = mapLister;
        _config = config;
    }

    public void ScheduleMapChange(string mapName, bool changeImmediately = false, bool isRtv = false)
    {
        _state.NextMap = mapName;
        _state.MapChangeScheduled = true;
        _state.ChangeMapImmediately = changeImmediately;
        _state.IsRtv = isRtv;

        if (changeImmediately || _state.MatchEnded)
        {
            ChangeMap();
        }
        else
        {
            _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.next_map_announced", mapName]);
        }
    }

    public void ChangeMap()
    {
        if (string.IsNullOrEmpty(_state.NextMap)) return;

        var mapName = _state.NextMap;
        string? previousMapName = _state.CurrentMapId;
        _state.NextMap = null;
        _state.MapChangeScheduled = false;
        _state.ChangeMapImmediately = true;

        var map = _mapLister.Maps.FirstOrDefault(m => m.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase));
        if (map == null)
        {
            var fallbackMap = _mapLister.Maps.Where(m => m.IsValidForPlayerCount(_core.PlayerManager.GetAllPlayers().Count(p => p.IsValid && !p.IsFakeClient)))
                .OrderBy(_ => Guid.NewGuid())
                .FirstOrDefault();
            if (fallbackMap == null) return;
            map = fallbackMap;
            _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.change_map.fallback", map.Name]);
        }

        bool wasRtv = _state.IsRtv;
        int delay = wasRtv ? _config.Rtv.ChangeMapDelay : _config.EndOfMap.ChangeMapDelay;
        _state.IsRtv = false;
        _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.changing_map", map.Name, delay]);

        // Mark match ended regardless of trigger source — prevents WinPanelMatch/
        // GamePhaseChanged from firing a second ChangeMap (cycle double-changelevel crash)
        if (!_state.MatchEnded)
            _state.MatchEnded = true;

        _core.Scheduler.DelayBySeconds(delay, () => {
            if (_core.Engine == null) return;
            // Final defence: prevent CS2 from issuing a competing changelevel
            _core.Engine.ExecuteCommand("mp_match_end_changelevel 0");
            _core.Engine.ExecuteCommand("mp_endmatch_votenextmap 0");
            _core.Engine.ExecuteCommand("mp_endmatch_votenextleveltime 0");
            if (!string.IsNullOrEmpty(map.Id) && (map.Id.StartsWith("ws:") || long.TryParse(map.Id, out _)))
            {
                string workshopId = map.Id.StartsWith("ws:") ? map.Id.Substring(3) : map.Id;
                _core.Engine.ExecuteCommand($"host_workshop_map {workshopId}");
            }
            else
            {
                _core.Engine.ExecuteCommand($"changelevel {map.Id}");
            }
        });

        _core.Scheduler.DelayBySeconds(delay + 15, () =>
        {
            if (_state.ChangeMapFallbackInProgress) return;
            if (string.IsNullOrEmpty(_state.CurrentMapId)) return;

            bool currentMapMatchesAttempt = string.Equals(_state.CurrentMapId, map.Name, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(map.Id) && string.Equals(_state.CurrentMapId, map.Id, StringComparison.OrdinalIgnoreCase));

            if (currentMapMatchesAttempt && !string.IsNullOrEmpty(previousMapName) &&
                (string.Equals(previousMapName, map.Name, StringComparison.OrdinalIgnoreCase) ||
                 (!string.IsNullOrEmpty(map.Id) && string.Equals(previousMapName, map.Id, StringComparison.OrdinalIgnoreCase))))
            {
                TriggerFallbackMapChange(map.Name);
            }
        });
    }

    private void TriggerFallbackMapChange(string attemptedMapName)
    {
        if (_state.ChangeMapFallbackInProgress) return;
        _state.ChangeMapFallbackInProgress = true;

        var playerCount = _core.PlayerManager.GetAllPlayers().Count(p => p.IsValid && !p.IsFakeClient);
        var fallbackMap = _mapLister.Maps
            .Where(m => m.IsValidForPlayerCount(playerCount) && !m.Name.Equals(attemptedMapName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefault();

        if (fallbackMap == null)
        {
            _state.ChangeMapFallbackInProgress = false;
            return;
        }

        _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.change_map.fallback", fallbackMap.Name]);
        _state.NextMap = fallbackMap.Name;
        _state.MapChangeScheduled = true;
        _state.ChangeMapImmediately = true;
        _state.IsRtv = false;
        ChangeMap();
    }
}
