using MapChanger.Api;
using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace MapChanger.Helpers;

public class ChangeMapManager
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly MapLister _mapLister;
    private readonly MapChangerConfig _config;
    private readonly MapChangerApi? _api;
    private CancellationTokenSource? _pendingChange;
    private CancellationTokenSource? _battleWait;
    private int _battleWaitGeneration;
    private bool _battleWaitAnnounced;
    public Func<bool?>? RtvBattleDecision { get; set; }
    public Action? CancelBattleWait { get; set; }
    private void ClearBattleWait()
    {
        ++_battleWaitGeneration;
        _battleWait?.Cancel();
        _battleWait = null;
        _battleWaitAnnounced = false;
    }

    internal ChangeMapManager(ISwiftlyCore core, PluginState state, MapLister mapLister, MapChangerConfig config, MapChangerApi? api = null)
    {
        _core = core;
        _state = state;
        _mapLister = mapLister;
        _config = config;
        _api = api;
    }

    public void ScheduleMapChange(string mapName, bool changeImmediately = false, bool isRtv = false)
    {
        ClearBattleWait();
        if (!isRtv) CancelBattleWait?.Invoke();
        _state.NextMap = mapName;
        _state.MapChangeScheduled = true;
        _state.MapChangeScheduledAt = DateTime.Now;
        _state.ChangeMapImmediately = changeImmediately;
        _state.IsRtv = isRtv;

        bool fireNow = changeImmediately || _state.MatchEnded;

        if (_config.DetailedLogging)
            _core.Logger.LogInformation(
                "MapChanger: ScheduleMapChange map={Map} immediate={Immediate} isRtv={IsRtv} matchEnded={MatchEnded} -> {Action}",
                mapName, changeImmediately, isRtv, _state.MatchEnded, fireNow ? "ChangeMap" : "WaitForRound/MatchEnd");

        _api?.RaiseNextMapDecided(mapName);

        if (fireNow || (isRtv && RtvBattleDecision?.Invoke() is not null))
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
        // Debounce concurrent triggers (WinPanelMatch + GamePhaseChanged + cycle + stuck timer, etc.)
        if (_state.MapSwitchInFlight) return;
        if (string.IsNullOrEmpty(_state.NextMap))
        {
            if (_config.DetailedLogging)
                _core.Logger.LogInformation("MapChanger: ChangeMap aborted — NextMap is empty.");
            return;
        }

        if (_state.IsRtv && RtvBattleDecision?.Invoke() == true)
        {
            if (!_battleWaitAnnounced)
            {
                _battleWaitAnnounced = true;
                _core.PlayerManager.SendChat($"[RTV] Next map: {_state.NextMap}. Waiting for the current battle to finish.");
            }
            if (_battleWait is null)
            {
                var generation = _battleWaitGeneration;
                _battleWait = _core.Scheduler.DelayBySeconds(1, () =>
                {
                    if (generation != _battleWaitGeneration) return;
                    _battleWait = null;
                    ChangeMap();
                });
                _core.Scheduler.StopOnMapChange(_battleWait);
            }
            return;
        }
        ClearBattleWait();
        var mapName = _state.NextMap;
        _state.NextMap = null;
        _state.MapChangeScheduled = false;
        _state.MapChangeScheduledAt = null;
        _state.ChangeMapImmediately = true;

        var map = _mapLister.Maps.FirstOrDefault(m => m.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase));
        if (map == null)
        {
            var fallbackMap = _mapLister.Maps.Where(m => m.IsValidForPlayerCount(_core.PlayerManager.GetAllPlayers().Count(p => p.IsValid && !p.IsFakeClient)))
                .OrderBy(_ => Guid.NewGuid())
                .FirstOrDefault();
            if (fallbackMap == null)
            {
                _core.Logger.LogWarning("MapChanger: ChangeMap aborted — map '{Map}' not found in map list and no fallback available.", mapName);
                CancelBattleWait?.Invoke();
                return;
            }
            map = fallbackMap;
            _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.change_map.fallback", map.Name]);
        }

        bool wasRtv = _state.IsRtv;
        int delay = wasRtv ? _config.Rtv.ChangeMapDelay : _config.EndOfMap.ChangeMapDelay;
        _state.IsRtv = false;
        _state.MapSwitchInFlight = true;
        _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.changing_map", map.Name, delay]);

        // Mark match ended regardless of trigger source — prevents WinPanelMatch/
        // GamePhaseChanged from firing a second ChangeMap (cycle double-changelevel crash)
        if (!_state.MatchEnded)
            _state.MatchEnded = true;

        _api?.RaiseMapChanging(map.Name);

        // Cancel any previously scheduled (but not yet fired) change to prevent stacking.
        _pendingChange?.Cancel();

        _pendingChange = _core.Scheduler.DelayBySeconds(delay, () =>
        {
            try
            {
                if (_core.Engine == null)
                {
                    _state.MapSwitchInFlight = false;
                    _core.Logger.LogWarning("MapChanger: engine unavailable while changing to {Map}.", map.Name);
                    return;
                }
                // Final defence: prevent CS2 from issuing a competing changelevel
                _core.Engine.ExecuteCommand("mp_match_end_changelevel 0");
                _core.Engine.ExecuteCommand("mp_endmatch_votenextmap 0");
                _core.Engine.ExecuteCommand("mp_endmatch_votenextleveltime 0");
                // Direct ExecuteCommand (not WithBuffer) — the buffered variant dropped
                // host_workshop_map during testing; direct issue is the reliable path here.
                if (!string.IsNullOrEmpty(map.Id) && (map.Id.StartsWith("ws:") || long.TryParse(map.Id, out _)))
                {
                    string workshopId = map.Id.StartsWith("ws:") ? map.Id.Substring(3) : map.Id;
                    _core.Engine.ExecuteCommand($"host_workshop_map {workshopId}");
                }
                else
                {
                    _core.Engine.ExecuteCommand($"changelevel {(!string.IsNullOrWhiteSpace(map.Id) ? map.Id : map.Name)}");
                }
            }
            catch (Exception ex)
            {
                _core.Logger.LogError(ex, "MapChanger: failed to issue map change to {Map}", map.Name);
                // Clear in-flight so a retry/fallback path can run.
                _state.MapSwitchInFlight = false;
            }
            finally
            {
                _pendingChange = null;
            }
        });

        // Belt & braces: if the map actually changes for any other reason before the delay
        // fires, the scheduler auto-cancels this token.
        _core.Scheduler.StopOnMapChange(_pendingChange);

        // Do not issue a second map command from a fixed timeout. Workshop loads can
        // outlast it, and the old callback also survived successful same-map reloads.
    }

    /// <summary>Cancel any pending map-change callback (used during plugin unload).</summary>
    public void CancelPending()
    {
        ClearBattleWait();
        CancelBattleWait?.Invoke();
        _pendingChange?.Cancel();
        _pendingChange = null;
    }
}
