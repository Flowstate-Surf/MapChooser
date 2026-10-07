using System.Threading;
using MapChanger.Api;
using MapChanger.Commands;
using MapChanger.Contracts;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using MapChanger.Menu;
using MapChanger.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using HudKit.Shared;

namespace MapChanger;

[PluginMetadata(Id = "MapChanger", Version = "1.3.4", Name = "Map Chooser", Author = "aga", Description = "Map chooser plugin for SwiftlyS2")]
public sealed class MapChanger : BasePlugin
{
    private MapChangerConfig _config = new();
    private MapsConfig _mapsConfig = new();
    private PluginState _state = new();
    private MapLister _mapLister = new();
    private MapCooldown _mapCooldown = null!;
    private ChangeMapManager _changeMapManager = null!;
    private VoteManager _rtvVoteManager = null!;
    private VoteManager _extVoteManager = null!;
    private EndOfMapVoteManager _eofManager = null!;
    private ExtendManager _extendManager = null!;
    private MapChooserHudMenuService _hudMenu = null!;
    private IHudKit? _hudKit;
    private IInterfaceManager? _battleInterfaces;
    private OptionalBattleInterface? BattleRotation => GetBattleInterface("Flowtimer.AutoBattleMapRotation.v1", "FlowtimerS2.Contract.IAutoBattleMapRotation");
    private OptionalBattleInterface? BattleGate => GetBattleInterface("Flowtimer.AutoBattleMapGate.v1", "FlowtimerS2.Contract.IAutoBattleMapGate");
    private OptionalBattleInterface? GetBattleInterface(string identity, string typeName)
        => _battleInterfaces?.HasSharedInterface(identity) == true
            ? new OptionalBattleInterface(_battleInterfaces.GetSharedInterface<object>(identity), typeName) : null;
    private readonly MapChangerApi _api;

    private MapCycleManager _cycleManager = null!;

    private RtvCommand _rtvCmd = null!;
    private UnRtvCommand _unRtvCmd = null!;
    private StuckCommand _stuckCmd = null!;
    private NominateCommand _nominateCmd = null!;
    private TimeleftCommand _timeleftCmd = null!;
    private NextmapCommand _nextmapCmd = null!;
    private VotemapCommand _votemapCmd = null!;
    private RevoteCommand _revoteCmd = null!;
    private SetNextMapCommand _setNextMapCmd = null!;
    private ExtendCommand _extendCmd = null!;
    private AdminMapsVoteCommand _adminMapsVoteCmd = null!;
    private AdminChangeMapCommand _adminChangeMapCmd = null!;
    private MapListCommand _mapListCmd = null!;
    private AddMapCommand _addMapCmd = null!;
    private RemoveMapCommand _removeMapCmd = null!;

    private CancellationTokenSource? _checkVoteTimer;
    private CancellationTokenSource? _convarGuard;
    private bool _runtimeInitialized;

    public MapChanger(ISwiftlyCore core) : base(core)
    {
        _api = new MapChangerApi(core);
        _api.VoteStarted += () => BattleRotation?.SetMapVoteActive(true);
        _api.VoteEnded += (_, _) => BattleRotation?.SetMapVoteActive(false);
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        // Public cross-plugin API. Contract assembly: contracts/MapChanger.Contracts
        // (references HudKit.Core's public API — HudKit.Shared), shipped in resources/exports/.
        interfaceManager.AddSharedInterface<IMapChangerApi, MapChangerApi>(IMapChangerApi.Identity, _api);
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        _battleInterfaces = interfaceManager;
        if (interfaceManager.HasSharedInterface(IHudKit.Identity))
        {
            var current = interfaceManager.GetSharedInterface<IHudKit>(IHudKit.Identity);
            if (_runtimeInitialized && !ReferenceEquals(current, _hudKit)) _hudMenu.SetHudKit(current);
            _hudKit = current;
        }
    }

    public override void OnSharedInterfaceInjected(IInterfaceManager interfaceManager)
    {
        UseSharedInterface(interfaceManager);
        if (!_runtimeInitialized && _hudKit is not null)
            Load(hotReload: false);
    }

    public override void Load(bool hotReload)
    {
        if (_runtimeInitialized)
            return;

        Core.Configuration
            .InitializeJsonWithModel<MapChangerConfig>("config.jsonc", "MapChanger")
            .Configure(builder =>
            {
                builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true);
            });

        Core.Configuration
            .InitializeJsonWithModel<MapsConfig>("maps.jsonc", "MapChangerMaps")
            .Configure(builder =>
            {
                builder.AddJsonFile("maps.jsonc", optional: false, reloadOnChange: true);
            });

        _config = Core.Configuration.Manager.GetSection("MapChanger").Get<MapChangerConfig>() ?? new MapChangerConfig();
        _mapsConfig = Core.Configuration.Manager.GetSection("MapChangerMaps").Get<MapsConfig>() ?? new MapsConfig();
        _mapLister.UpdateMaps(_mapsConfig.Maps);

        // Swiftly can call Load before shared interfaces have finished injecting. Defer
        // runtime registration until OnSharedInterfaceInjected instead of failing the plugin.
        if (_hudKit is null)
            return;

        _hudMenu = new MapChooserHudMenuService(Core, _hudKit);
        _hudMenu.BattleVoteEnabled = () => BattleGate != null;

        _mapCooldown = new MapCooldown(Core, _config);
        _changeMapManager = new ChangeMapManager(Core, _state, _mapLister, _config, _api);
        _changeMapManager.RtvBattleDecision = () => BattleGate?.DeferRtvChange();
        _changeMapManager.CancelBattleWait = () => BattleGate?.CancelRtvChange();
        _rtvVoteManager = new VoteManager();
        _extVoteManager = new VoteManager();
        _extendManager = new ExtendManager(Core, _state, _config, _extVoteManager);
        _eofManager = new EndOfMapVoteManager(Core, _state, _rtvVoteManager, _mapLister, _mapCooldown, _changeMapManager, _extendManager, _config, _hudMenu, _api);
        _api.Attach(_state, _config, _mapLister, _mapCooldown, _changeMapManager, _eofManager, _extendManager, _hudMenu, _hudKit);

        var mapsFilePath = Core.Configuration.GetConfigPath("maps.jsonc");
        _cycleManager = new MapCycleManager(Core, _state, _mapLister, _changeMapManager, _config, mapsFilePath);

        _state.ExtendsLeft = _config.EndOfMap.ExtendLimit;
        _state.NextEofVotePossibleRound = 0;
        _state.NextEofVotePossibleTime = 0;
        _state.RoundsPlayed = 0;
        var warmupConVar = Core.ConVar.Find<int>("mp_warmup_period");
        _state.WarmupRunning = warmupConVar?.Value == 1;

        _rtvCmd = new RtvCommand(Core, _state, _rtvVoteManager, _eofManager, _config);
        _unRtvCmd = new UnRtvCommand(Core, _state, _rtvVoteManager, _eofManager, _config);
        _stuckCmd = new StuckCommand(Core, _state, _rtvVoteManager, _eofManager, _mapLister, _config);
        _nominateCmd = new NominateCommand(Core, _state, _mapLister, _mapCooldown, _config, _hudMenu);
        _timeleftCmd = new TimeleftCommand(Core, _state, _config);
        _nextmapCmd = new NextmapCommand(Core, _state, _cycleManager, _config);
        _votemapCmd = new VotemapCommand(Core, _state, _mapLister, _mapCooldown, _changeMapManager, _config, _hudMenu);
        _revoteCmd = new RevoteCommand(Core, _state, _eofManager, _config);
        _setNextMapCmd = new SetNextMapCommand(Core, _state, _mapLister, _changeMapManager, _hudMenu);
        _extendCmd = new ExtendCommand(Core, _state, _extVoteManager, _extendManager, _config);
        _adminMapsVoteCmd = new AdminMapsVoteCommand(Core, _state, _mapLister, _eofManager, _config, _hudMenu);
        _adminChangeMapCmd = new AdminChangeMapCommand(Core, _state, _mapLister, _changeMapManager, _hudMenu);
        _mapListCmd = new MapListCommand(Core, _mapLister, _mapCooldown);
        _addMapCmd = new AddMapCommand(Core, _cycleManager);
        _removeMapCmd = new RemoveMapCommand(Core, _cycleManager);

        // Swiftly exposes registered numeric commands as !1 / /1 etc. in chat.
        // No movement hooks, mouse capture, or client key bindings are used for votes.
        for (int number = 0; number <= MapChooserMenuTemplate.MaxOptions; number++)
        {
            int choice = number;
            Core.Command.RegisterCommand(choice.ToString(), context =>
            {
                if (context.Sender is { IsValid: true } player) _hudMenu.SelectNumber(player, choice);
            });
        }

        RegisterCommands(_config.Commands.Rtv, _rtvCmd.Execute);
        RegisterCommands(_config.Commands.UnRtv, _unRtvCmd.Execute);
        RegisterCommands(_config.Commands.Stuck, _stuckCmd.Execute, permission: _config.ExtendMap.Permission);
        RegisterCommands(_config.Commands.Nominate, _nominateCmd.Execute);
        RegisterCommands(_config.Commands.Timeleft, _timeleftCmd.Execute);
        RegisterCommands(_config.Commands.Nextmap, _nextmapCmd.Execute);
        RegisterCommands(_config.Commands.Votemap, _votemapCmd.Execute);
        RegisterCommands(_config.Commands.Revote, _revoteCmd.Execute);
        RegisterCommands(_config.Commands.SetNextMap, _setNextMapCmd.Execute, permission: _config.SetNextMapPermission);
        RegisterCommands(_config.Commands.Extend, _extendCmd.Execute, permission: _config.ExtendMap.Permission);
        RegisterCommands(_config.Commands.MapsVote, _adminMapsVoteCmd.Execute, permission: _config.MapsVotePermission);
        RegisterCommands(_config.Commands.ChangeMap, _adminChangeMapCmd.Execute, permission: _config.ChangeMapPermission);
        RegisterCommands(_config.Commands.MapList, _mapListCmd.Execute);
        RegisterCommands(_config.Commands.AddMap, _addMapCmd.Execute, permission: _config.Cycle.AddMapPermission);
        RegisterCommands(_config.Commands.RemoveMap, _removeMapCmd.Execute, permission: _config.Cycle.RemoveMapPermission);
        RegisterCommands(_config.Commands.CycleMenu, ExecuteCycleMenu, permission: _config.Cycle.CycleMenuPermission);

        Core.GameEvent.HookPost<EventRoundEnd>(OnRoundEnd);
        Core.GameEvent.HookPost<EventRoundStart>(OnRoundStart);
        Core.GameEvent.HookPost<EventRoundAnnounceWarmup>(OnAnnounceWarmup);
        Core.GameEvent.HookPost<EventWarmupEnd>(OnWarmupEnd);
        Core.GameEvent.HookPost<EventCsWinPanelMatch>(OnWinPanelMatch);
        Core.GameEvent.HookPost<EventCsIntermission>(OnCsIntermission);
        Core.GameEvent.HookPost<EventMapShutdown>(OnMapShutdown);
        Core.GameEvent.HookPost<EventGamePhaseChanged>(OnGamePhaseChanged);
        Core.GameEvent.HookPost<EventRoundAnnounceMatchStart>(OnMatchStart);
        Core.GameEvent.HookPost<EventRoundAnnounceMatchPoint>(OnMatchPoint);
        Core.Event.OnMapLoad += OnMapLoad;
        Core.Event.OnMapUnload += OnMapUnload;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        _checkVoteTimer = Core.Scheduler.DelayAndRepeatBySeconds(1f, 1f, () =>
        {
            CheckStuckMapChange();
            CheckAutomatedVote();
        });
        Core.Scheduler.StopOnMapChange(_checkVoteTimer);
        _runtimeInitialized = true;
    }

    /// <summary>
    /// Safety net for a fully empty/hibernating server: if a map change was decided
    /// but nothing (no round end, no match-start/end event) has applied it after a
    /// grace period, force it through directly. Round end normally handles this, but
    /// on an empty server rounds may never complete at all, so this can't rely on
    /// any game event firing.
    /// </summary>
    private void CheckStuckMapChange()
    {
        if (!_state.MapChangeScheduled || _state.EofVoteHappening || _state.ChangeMapImmediately) return;
        if (_state.MapChangeScheduledAt is null) return;
        // A completed advance vote is a next-map decision, not a stuck change.
        // Never let the watchdog cut short an active round on a populated server.
        if (!_state.MatchEnded)
        {
            if (Core.PlayerManager.GetAllPlayers().Any(p => p.IsValid && !p.IsFakeClient)) return;
            float limit = Core.ConVar.Find<float>("mp_timelimit")?.Value ?? 0;
            if (limit <= 0 || Core.TryGetCurrentTime() - _state.MapStartTime < limit * 60) return;
        }


        if ((DateTime.Now - _state.MapChangeScheduledAt.Value).TotalSeconds >= _config.EndOfMap.ChangeMapDelay + 10)
        {
            _changeMapManager.ChangeMap();
        }
    }

    private void OnMapUnload(IOnMapUnloadEvent _)
    {
        _changeMapManager.CancelPending();
        _hudMenu.ForgetAll();
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        if (string.IsNullOrEmpty(@event.MapName)) return;

        _eofManager?.ResetVote();
        _state.MapChangeScheduled = false;
        _state.MapChangeScheduledAt = null;
        _state.EofVoteHappening = false;
        _state.NextMap = null;
        _state.RoundsPlayed = 0;
        // GetGameRules() calls ThrowIfEntitySystemInvalid() internally and throws
        // InvalidOperationException when the entity system isn't ready yet (early OnMapLoad).
        // Also, GlobalVars dereferences a native pointer via Unsafe.AsRef — if the pointer is
        // zero it causes a native AV that try/catch cannot catch. Gate all access on gamerules
        // existence, which confirms the engine is fully initialised.
        CCSGameRules? gameRules = null;
        try { gameRules = Core.EntitySystem.GetGameRules(); } catch (InvalidOperationException) { }
        bool engineReady = gameRules?.IsValid == true && Core.Engine != null;

        _state.MapStartTime = engineReady ? Core.Engine!.GlobalVars.CurrentTime : 0;

        _state.RtvCooldownEndTime = null;
        _state.ExtendVoteCooldownEndTime = null;
        _state.IsRtv = false;
        _state.ChangeMapImmediately = false;
        // Clear the changelevel debounce from the previous map (upstream v1.3.0 missed this —
        // without it a successful switch would leave ChangeMap() debounced forever).
        _state.MapSwitchInFlight = false;

        _rtvVoteManager?.Clear();
        _extVoteManager?.Clear();
        _nominateCmd?.Clear();
        _state.ExtendsLeft = _config.EndOfMap.ExtendLimit;
        _state.NextEofVotePossibleRound = 0;
        _state.NextEofVotePossibleTime = 0;
        _state.MatchEnded = false;
        _state.EofVoteCompleted = false;
        _state.ChangeMapFallbackInProgress = false;

        string workshopId = engineReady ? Core.Engine!.WorkshopId : "";
        _state.CurrentMapId = engineReady
            ? (Core.Engine!.GlobalVars.MapName.Value ?? @event.MapName ?? "")
            : (@event.MapName ?? "");
        _state.CurrentWorkshopId = workshopId;
        _mapCooldown.OnMapStart(@event.MapName ?? "", workshopId);
        _cycleManager.OnMapStart(@event.MapName ?? "", workshopId);

        _checkVoteTimer = Core.Scheduler.DelayAndRepeatBySeconds(1f, 1f, () =>
        {
            CheckStuckMapChange();
            CheckAutomatedVote();
        });
        Core.Scheduler.StopOnMapChange(_checkVoteTimer);

        // Disable CS2's native map-change flow — the plugin owns all map changes.
        // Apply immediately, then periodically (fires at 5 s, repeats every 30 s) to
        // beat exec_after_map_start cfgs (e.g. map_voting.cfg) regardless of their delay.
        if (Core.Engine != null)
        {
            Core.Engine.ExecuteCommand("mp_match_end_changelevel 0");
            Core.Engine.ExecuteCommand("mp_endmatch_votenextmap 0");
            Core.Engine.ExecuteCommand("mp_endmatch_votenextleveltime 0");
            if (!string.IsNullOrEmpty(_config.MapGroup))
                Core.Engine.ExecuteCommand($"sv_mapgroup {_config.MapGroup}");
        }
        _convarGuard?.Cancel();
        _convarGuard = Core.Scheduler.DelayAndRepeatBySeconds(5f, 30f, () =>
        {
            if (Core.Engine != null)
            {
                Core.Engine.ExecuteCommand("mp_match_end_changelevel 0");
                Core.Engine.ExecuteCommand("mp_endmatch_votenextmap 0");
                Core.Engine.ExecuteCommand("mp_endmatch_votenextleveltime 0");
            }
        });
        Core.Scheduler.StopOnMapChange(_convarGuard);
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        // Re-assert convars every round — mapsettings.cfg (exec'd by Flowtimer on RoundStart)
        // can re-enable mp_match_end_changelevel 1. Immediate reset handles the case where
        // mapsettings runs before us; NextWorldUpdate handles the case where it runs after.
        if (Core.Engine != null)
        {
            Core.Engine.ExecuteCommand("mp_match_end_changelevel 0");
            Core.Engine.ExecuteCommand("mp_endmatch_votenextmap 0");
            Core.Engine.ExecuteCommand("mp_endmatch_votenextleveltime 0");
        }
        Core.Scheduler.NextWorldUpdate(() =>
        {
            if (Core.Engine != null)
            {
                Core.Engine.ExecuteCommand("mp_match_end_changelevel 0");
                Core.Engine.ExecuteCommand("mp_endmatch_votenextmap 0");
                Core.Engine.ExecuteCommand("mp_endmatch_votenextleveltime 0");
            }
        });
        CheckAutomatedVote();
        return HookResult.Continue;
    }

    private HookResult OnAnnounceWarmup(EventRoundAnnounceWarmup @event)
    {
        _state.WarmupRunning = true;
        return HookResult.Continue;
    }

    private HookResult OnWarmupEnd(EventWarmupEnd @event)
    {
        _state.WarmupRunning = false;
        CCSGameRules? gameRules = null;
        try { gameRules = Core.EntitySystem.GetGameRules(); } catch (InvalidOperationException) { }
        if (gameRules?.IsValid == true && Core.Engine != null)
            _state.MapStartTime = Core.Engine.GlobalVars.CurrentTime;
        return HookResult.Continue;
    }

    private HookResult OnMatchStart(EventRoundAnnounceMatchStart @event)
    {
        // If a map change was already decided (e.g. an EOF/extend vote finished while the
        // server was empty and never got a chance to apply), don't silently wipe it here —
        // that would strand the server on the current map with the decision lost and no
        // revote pending. Apply it now instead.
        if (_state.MapChangeScheduled && !_state.EofVoteHappening && !_state.ChangeMapImmediately)
        {
            _changeMapManager.ChangeMap();
            return HookResult.Continue;
        }

        _eofManager?.ResetVote();
        _state.RoundsPlayed = 0;
        CCSGameRules? gameRules = null;
        try { gameRules = Core.EntitySystem.GetGameRules(); } catch (InvalidOperationException) { }
        if (gameRules?.IsValid == true && Core.Engine != null)
            _state.MapStartTime = Core.Engine.GlobalVars.CurrentTime;
        _state.WarmupRunning = false;
        _state.NextEofVotePossibleRound = 0;
        _state.NextEofVotePossibleTime = 0;
        _state.MapChangeScheduled = false;
        _state.MapChangeScheduledAt = null;
        _state.EofVoteHappening = false;
        _state.EofVoteCompleted = false;
        _state.IsRtv = false;
        _state.ChangeMapImmediately = false;
        _state.NextMap = null;
        _state.ExtendsLeft = _config.EndOfMap.ExtendLimit;
        _state.ExtendVoteCooldownEndTime = null;

        _rtvVoteManager?.Clear();
        _extVoteManager?.Clear();
        return HookResult.Continue;
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        // Drop the disconnecting player from any active vote tallies so the
        // required-vote threshold reflects actual remaining participants.
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player is null) return;
        _rtvVoteManager?.RemoveVote(player.Slot);
        _extVoteManager?.RemoveVote(player.Slot);
    }

    private HookResult OnMatchPoint(EventRoundAnnounceMatchPoint @event)
    {
        CheckAutomatedVote(true);
        return HookResult.Continue;
    }

    private HookResult OnWinPanelMatch(EventCsWinPanelMatch @event)
    {
        try
        {
            if (Core.Game.MatchData.Phase == GamePhase.GAMEPHASE_HALFTIME) return HookResult.Continue;
        }
        catch (Exception ex)
        {
            Core.Logger.LogDebug(ex, "GameRules not available in OnWinPanelMatch - proceeding without halftime check");
        }

        // Ensure CS2 won't issue its own changelevel (map_voting.cfg may have re-enabled it)
        if (Core.Engine != null)
        {
            Core.Engine.ExecuteCommand("mp_match_end_changelevel 0");
            Core.Engine.ExecuteCommand("mp_endmatch_votenextmap 0");
        }
        if (BattleGate != null || _state.MatchEnded || _state.ChangeMapImmediately) return HookResult.Continue;
        _state.MatchEnded = true;
        if (_state.EofVoteHappening)
            _eofManager.ForceEnd();
        else if (_state.MapChangeScheduled)
            _changeMapManager.ChangeMap();
        else if (_config.Cycle.Enabled)
            _cycleManager.TriggerCycleChange();
        return HookResult.Continue;
    }

    private HookResult OnCsIntermission(EventCsIntermission @event)
    {
        if (Core.Engine != null)
        {
            Core.Engine.ExecuteCommand("mp_match_end_changelevel 0");
            Core.Engine.ExecuteCommand("mp_endmatch_votenextmap 0");
        }
        if (BattleGate != null || _state.MatchEnded || _state.ChangeMapImmediately || _state.WarmupRunning) return HookResult.Continue;
        _state.MatchEnded = true;
        if (_state.EofVoteHappening)
            _eofManager.ForceEnd();
        else if (_state.MapChangeScheduled)
            _changeMapManager.ChangeMap();
        else if (_config.Cycle.Enabled)
            _cycleManager.TriggerCycleChange();
        return HookResult.Continue;
    }

    private HookResult OnMapShutdown(EventMapShutdown @event)
    {
        _checkVoteTimer?.Cancel();
        _checkVoteTimer = null;
        _convarGuard?.Cancel();
        _convarGuard = null;
        _eofManager?.Shutdown();
        return HookResult.Continue;
    }

    private HookResult OnGamePhaseChanged(EventGamePhaseChanged @event)
    {
        if (@event.NewPhase != (short)GamePhase.GAMEPHASE_MATCH_ENDED) return HookResult.Continue;
        if (Core.Engine != null)
        {
            Core.Engine.ExecuteCommand("mp_match_end_changelevel 0");
            Core.Engine.ExecuteCommand("mp_endmatch_votenextmap 0");
        }
        if (BattleGate != null || _state.MatchEnded || _state.ChangeMapImmediately) return HookResult.Continue;

        _state.MatchEnded = true;
        if (_state.EofVoteHappening)
            _eofManager.ForceEnd();
        else if (_state.MapChangeScheduled)
            _changeMapManager.ChangeMap();
        else if (_config.Cycle.Enabled)
            _cycleManager.TriggerCycleChange();
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        _state.RoundsPlayed++;

        if (_config.DetailedLogging)
            Core.Logger.LogInformation(
                "MapChanger: OnRoundEnd scheduled={Scheduled} eofVote={Eof} immediate={Immediate} isRtv={IsRtv} inFlight={InFlight} nextMap={NextMap}",
                _state.MapChangeScheduled, _state.EofVoteHappening,
                _state.ChangeMapImmediately, _state.IsRtv, _state.MapSwitchInFlight, _state.NextMap ?? "<null>");

        // Apply ANY pending map change here, not just RTV ones. Non-RTV changes
        // (automated EOF vote, !votemap, !setnextmap, post-extend vote) used to only
        // apply via the native match-end events (WinPanelMatch/CsIntermission/
        // GamePhaseChanged). Those events depend on the match actually progressing —
        // if the server was empty (hibernating) when the vote finished, they may never
        // fire, leaving the map "stuck" even after players reconnect. Round end is a
        // reliable, always-fired hook, so use it as the general trigger.
        if (_state.MapChangeScheduled && !_state.EofVoteHappening && !_state.ChangeMapImmediately
            && (_state.IsRtv || _state.MatchEnded || MapTimeExpired()))
        {
            _changeMapManager.ChangeMap();
        }
        else if (!_state.MapChangeScheduled)
        {
            CheckAutomatedVote();
        }

        return HookResult.Continue;
    }

    private bool MapTimeExpired()
    {
        float limit = Core.ConVar.Find<float>("mp_timelimit")?.Value ?? 0;
        return limit > 0 && Core.TryGetCurrentTime() - _state.MapStartTime >= limit * 60;
    }

    private void CheckAutomatedVote(bool force = false)
    {
        try
        {
            CheckAutomatedVoteCore(force);
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "MapChanger: CheckAutomatedVote threw");
        }
    }

    private DateTime _battleVoteRetry;
    private void CheckAutomatedVoteCore(bool force)
    {
        if (BattleGate != null) {
            if (_state.EofVoteHappening || _state.MapChangeScheduled || _state.MapSwitchInFlight || DateTime.Now < _battleVoteRetry) return;
            if (BattleRotation?.ReadyForMapVote == true) {
                _battleVoteRetry = DateTime.Now.AddSeconds(30);
                _eofManager.StartVote(_config.EndOfMap.VoteDuration, 6, changeImmediately:true);
            }
            return;
        }

        if (!_config.EndOfMap.Enabled || _state.EofVoteHappening || _state.MapChangeScheduled || _state.ChangeMapImmediately || _state.WarmupRunning) return;
        if (_state.MapSwitchInFlight) return;

        int totalRoundsPlayed;
        try
        {
            if (Core.Game.MatchData.Phase == GamePhase.GAMEPHASE_HALFTIME) return;
            totalRoundsPlayed = Core.Game.MatchData.TerroristScoreTotal + Core.Game.MatchData.CTScoreTotal;
        }
        catch (Exception ex)
        {
            Core.Logger.LogDebug(ex, "GameRules not available in CheckAutomatedVote - skipping this tick");
            return;
        }

        bool pastDueNoMap = _state.EofVoteCompleted && string.IsNullOrEmpty(_state.NextMap);

        if (!force && !pastDueNoMap)
        {
            if (_state.EofVoteCompleted) return;
            if (totalRoundsPlayed < _state.NextEofVotePossibleRound) return;
            if (Core.Engine != null && Core.Engine.GlobalVars.CurrentTime < _state.NextEofVotePossibleTime) return;
        }

        var timelimitConVar = Core.ConVar.Find<float>("mp_timelimit");
        var maxroundsConVar = Core.ConVar.Find<int>("mp_maxrounds");
        var winlimitConVar = Core.ConVar.Find<int>("mp_winlimit");

        float timelimit = timelimitConVar?.Value ?? 0;
        int maxrounds = maxroundsConVar?.Value ?? 0;
        int winlimit = winlimitConVar?.Value ?? 0;

        bool trigger = false;

        if (timelimit > 0 && Core.Engine != null)
        {
            if (_state.MapStartTime <= 0)
            {
                _state.MapStartTime = Core.Engine.GlobalVars.CurrentTime;
            }
            float timePlayed = Core.Engine.GlobalVars.CurrentTime - _state.MapStartTime;
            float timeRemaining = (timelimit * 60) - timePlayed;
            if (timeRemaining <= _config.EndOfMap.TriggerSecondsBeforeEnd)
            {
                trigger = true;
            }
        }

        if (!trigger && maxrounds > 0)
        {
            int roundsRemaining = maxrounds - totalRoundsPlayed;
            if (roundsRemaining <= _config.EndOfMap.TriggerRoundsBeforeEnd)
            {
                trigger = true;
            }
        }

        if (!trigger && winlimit > 0)
        {
            int maxTeamScore = TryGetMaxTeamScore();
            if (winlimit - maxTeamScore <= _config.EndOfMap.TriggerRoundsBeforeEnd)
            {
                trigger = true;
            }
        }

        // When mp_winlimit is not set, CS2 still ends the match when a team wins (maxrounds/2)+1 rounds
        if (!trigger && winlimit == 0 && maxrounds > 0)
        {
            int effectiveWinlimit = maxrounds / 2 + 1;
            int maxTeamScore = TryGetMaxTeamScore();
            if (effectiveWinlimit - maxTeamScore <= _config.EndOfMap.TriggerRoundsBeforeEnd)
            {
                trigger = true;
            }
        }

        if (trigger)
        {
            _state.EofVoteCompleted = false;
            _state.NextEofVotePossibleRound = totalRoundsPlayed + 1;
            if (Core.Engine != null)
                _state.NextEofVotePossibleTime = Core.Engine.GlobalVars.CurrentTime + _config.EndOfMap.VoteDuration + 1;
            _eofManager.StartVote(_config.EndOfMap.VoteDuration, _config.EndOfMap.MapsToShow);
        }
    }

    /// <summary>
    /// Safely read the highest team score. The entity system can throw if it
    /// isn't fully initialised yet (e.g. during early map load), so swallow
    /// and return 0 in that case. (Ported from upstream MapChooser v1.3.0.)
    /// </summary>
    private int TryGetMaxTeamScore()
    {
        try
        {
            var teams = Core.EntitySystem.GetAllEntitiesByClass<CCSTeam>();
            int maxTeamScore = 0;
            foreach (var team in teams)
            {
                int score = team.ScoreFirstHalf + team.ScoreSecondHalf + team.ScoreOvertime;
                if (score > maxTeamScore) maxTeamScore = score;
            }
            return maxTeamScore;
        }
        catch (Exception ex)
        {
            Core.Logger.LogDebug(ex, "MapChanger: failed to read team scores");
            return 0;
        }
    }

    private void ExecuteCycleMenu(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("This command can only be used by players.");
            return;
        }
        var menu = new CycleMenu(Core, _mapLister, _cycleManager, _config, _hudMenu);
        menu.Show(context.Sender!);
    }

    private void RegisterCommands(string commandNames, ICommandService.CommandListener handler, string? permission = null)
    {
        var names = commandNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (!string.IsNullOrWhiteSpace(permission))
                    Core.Command.RegisterCommand(name, handler, permission: permission);
                else
                    Core.Command.RegisterCommand(name, handler);
            }
        }
    }

    public override void Unload()
    {
        try
        {
            _checkVoteTimer?.Cancel();
            _checkVoteTimer = null;

            _convarGuard?.Cancel();
            _convarGuard = null;

            _changeMapManager?.CancelPending();
            _hudMenu?.CloseAll();

            Core.Event.OnMapLoad -= OnMapLoad;
            Core.Event.OnMapUnload -= OnMapUnload;
            Core.Event.OnClientDisconnected -= OnClientDisconnected;

            Core.GameEvent.UnhookPost<EventRoundEnd>();
            Core.GameEvent.UnhookPost<EventRoundStart>();
            Core.GameEvent.UnhookPost<EventRoundAnnounceWarmup>();
            Core.GameEvent.UnhookPost<EventWarmupEnd>();
            Core.GameEvent.UnhookPost<EventCsWinPanelMatch>();
            Core.GameEvent.UnhookPost<EventCsIntermission>();
            Core.GameEvent.UnhookPost<EventMapShutdown>();
            Core.GameEvent.UnhookPost<EventGamePhaseChanged>();
            Core.GameEvent.UnhookPost<EventRoundAnnounceMatchStart>();
            Core.GameEvent.UnhookPost<EventRoundAnnounceMatchPoint>();
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "MapChanger: Unload cleanup failed");
        }
    }
}
