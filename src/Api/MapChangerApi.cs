using HudKit.Shared;
using MapChanger.Contracts;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using MapChanger.Menu;
using MapChanger.Models;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Api;

/// <summary>
/// MapChanger-side implementation of <see cref="IMapChangerApi"/>. Created before
/// <c>ConfigureSharedInterface</c> runs and registered immediately; the runtime managers only
/// exist after the plugin's deferred <c>Load</c> completes (MapChanger waits for HudKit), so
/// they're wired later via <see cref="Attach"/> and every member degrades gracefully until then.
///
/// All members are expected to be called on the main thread (same rule as the rest of SwiftlyS2);
/// events are raised on the main thread with per-subscriber isolation so a throwing consumer
/// can't break the vote flow.
/// </summary>
internal sealed class MapChangerApi : IMapChangerApi
{
    private readonly ISwiftlyCore _core;

    private PluginState? _state;
    private MapChangerConfig? _config;
    private MapLister? _mapLister;
    private MapCooldown? _mapCooldown;
    private ChangeMapManager? _changeMapManager;
    private EndOfMapVoteManager? _eofManager;
    private ExtendManager? _extendManager;
    private MapChooserHudMenuService? _hudMenu;
    private IHudKit? _hudKit;

    public MapChangerApi(ISwiftlyCore core)
    {
        _core = core;
    }

    /// <summary>Called from <c>MapChanger.Load</c> once the runtime managers exist.</summary>
    internal void Attach(
        PluginState state,
        MapChangerConfig config,
        MapLister mapLister,
        MapCooldown mapCooldown,
        ChangeMapManager changeMapManager,
        EndOfMapVoteManager eofManager,
        ExtendManager extendManager,
        MapChooserHudMenuService hudMenu,
        IHudKit? hudKit)
    {
        _state = state;
        _config = config;
        _mapLister = mapLister;
        _mapCooldown = mapCooldown;
        _changeMapManager = changeMapManager;
        _eofManager = eofManager;
        _extendManager = extendManager;
        _hudMenu = hudMenu;
        _hudKit = hudKit;
    }

    // ---------------------------------------------------------------- state

    public bool IsReady => _state is not null;

    public string CurrentMap => _state?.CurrentMapId ?? string.Empty;

    public string CurrentWorkshopId => _state?.CurrentWorkshopId ?? string.Empty;

    public string? NextMap => _state?.NextMap;

    public bool MapChangeScheduled => _state?.MapChangeScheduled ?? false;

    public bool VoteInProgress => _state?.EofVoteHappening ?? false;

    public int ExtendsLeft => _state?.ExtendsLeft ?? 0;

    public int? TimeLeftSeconds
    {
        get
        {
            if (_state is null) return null;
            var timelimit = _core.ConVar.Find<float>("mp_timelimit")?.Value ?? 0;
            if (timelimit <= 0) return null;

            float currentTime = _core.TryGetCurrentTime();
            if (currentTime <= 0 || _state.MapStartTime <= 0) return null;

            return Math.Max(0, (int)(timelimit * 60 - (currentTime - _state.MapStartTime)));
        }
    }

    public int? RoundsLeft
    {
        get
        {
            if (_state is null) return null;
            var maxrounds = _core.ConVar.Find<int>("mp_maxrounds")?.Value ?? 0;
            if (maxrounds <= 0) return null;

            try
            {
                int played = _core.Game.MatchData.TerroristScoreTotal + _core.Game.MatchData.CTScoreTotal;
                return Math.Max(0, maxrounds - played);
            }
            catch (Exception ex)
            {
                _core.Logger.LogDebug(ex, "MapChanger.Api: game rules unavailable for RoundsLeft");
                return null;
            }
        }
    }

    public IReadOnlyList<MapChangerMapInfo> GetMaps()
    {
        if (_mapLister is null || _mapCooldown is null) return Array.Empty<MapChangerMapInfo>();

        return _mapLister.Maps
            .Select(m => new MapChangerMapInfo(
                m.Name,
                m.Id,
                m.Tier,
                m.MinPlayers,
                m.MaxPlayers,
                _mapCooldown.IsMapInCooldown(m)))
            .ToList();
    }

    public IReadOnlyDictionary<int, string> GetNominations()
        => _state is null
            ? new Dictionary<int, string>()
            : new Dictionary<int, string>(_state.Nominations);

    public IHudKit? HudKit => _hudKit;

    // -------------------------------------------------------------- actions

    public bool ScheduleMapChange(string mapNameOrId, bool changeImmediately = false)
    {
        if (_changeMapManager is null || _mapLister is null) return false;

        var map = _mapLister.Maps.FirstOrDefault(m =>
            m.Name.Equals(mapNameOrId, StringComparison.OrdinalIgnoreCase) ||
            (m.Id is not null && m.Id.Equals(mapNameOrId, StringComparison.OrdinalIgnoreCase)));
        if (map is null) return false;

        _changeMapManager.ScheduleMapChange(map.Name, changeImmediately, isRtv: false);
        return true;
    }

    public bool StartVote(int voteDurationSeconds, int mapsToShow)
    {
        if (_eofManager is null || _state is null) return false;
        if (_state.EofVoteHappening) return false;

        _eofManager.StartVote(voteDurationSeconds, mapsToShow);
        return true;
    }

    public void CancelVote() => _eofManager?.CancelVote();

    public bool OpenVoteMenu(IPlayer player)
    {
        if (_eofManager is null || _state is null || _config is null) return false;
        if (!_state.EofVoteHappening) return false;
        if (!player.IsValid) return false;
        if (!_config.AllowSpectatorsToVote && player.Controller?.TeamNum == 1) return false;

        _eofManager.OpenVoteMenu(player);
        return true;
    }

    public bool ExtendCurrentMap()
    {
        if (_extendManager is null || _state is null || _config is null) return false;
        if (_state.ExtendsLeft <= 0) return false;

        _extendManager.ExtendMap(_config.EndOfMap.ExtendTimeStep, _config.EndOfMap.ExtendRoundStep);
        return true;
    }

    // --------------------------------------------------------------- events

    public event Action<string>? NextMapDecided;
    public event Action? VoteStarted;
    public event Action<MapChangerVoteResult, string?>? VoteEnded;
    public event Action<string>? MapChanging;

    internal void RaiseNextMapDecided(string mapName)
    {
        foreach (var handler in SafeInvocationList(NextMapDecided))
        {
            try { handler(mapName); }
            catch (Exception ex) { _core.Logger.LogWarning(ex, "MapChanger.Api: NextMapDecided subscriber threw"); }
        }
    }

    internal void RaiseVoteStarted()
    {
        var handlers = VoteStarted;
        if (handlers is null) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception ex) { _core.Logger.LogWarning(ex, "MapChanger.Api: VoteStarted subscriber threw"); }
        }
    }

    internal void RaiseVoteEnded(MapChangerVoteResult result, string? winner)
    {
        var handlers = VoteEnded;
        if (handlers is null) return;
        foreach (Action<MapChangerVoteResult, string?> handler in handlers.GetInvocationList())
        {
            try { handler(result, winner); }
            catch (Exception ex) { _core.Logger.LogWarning(ex, "MapChanger.Api: VoteEnded subscriber threw"); }
        }
    }

    internal void RaiseMapChanging(string mapName)
    {
        foreach (var handler in SafeInvocationList(MapChanging))
        {
            try { handler(mapName); }
            catch (Exception ex) { _core.Logger.LogWarning(ex, "MapChanger.Api: MapChanging subscriber threw"); }
        }
    }

    /// <summary>Null-safe invocation list snapshot for <c>Action&lt;T&gt;</c> events.</summary>
    private static IEnumerable<Action<T>> SafeInvocationList<T>(Action<T>? handlers)
    {
        if (handlers is null) yield break;
        foreach (var handler in handlers.GetInvocationList())
            yield return (Action<T>)handler;
    }
}
