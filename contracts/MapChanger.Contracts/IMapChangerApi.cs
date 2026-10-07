using HudKit.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Contracts;

/// <summary>
/// Public shared interface exposed by the MapChanger plugin (id <c>MapChanger</c>).
///
/// Consumer plugins obtain it from <c>UseSharedInterface</c>:
/// <code>
/// if (interfaceManager.HasSharedInterface(IMapChangerApi.Identity))
///     var api = interfaceManager.GetSharedInterface&lt;IMapChangerApi&gt;(IMapChangerApi.Identity);
/// </code>
///
/// Threading: like most SwiftlyS2 APIs, all members must be called from the main thread
/// (game-event handlers, command handlers, or <c>Scheduler.NextWorldUpdate</c> callbacks).
/// Events are raised on the main thread. If MapChanger's runtime hasn't finished initialising
/// (it defers startup until its own HudKit dependency is injected), every member degrades
/// gracefully — see <see cref="IsReady" />.
/// </summary>
public interface IMapChangerApi
{
    /// <summary>Shared-interface registration key (versioned — bump on breaking changes).</summary>
    public const string Identity = "MapChanger.Api.v1";

    // ---------------------------------------------------------------- state

    /// <summary>True once MapChanger finished its deferred startup and all managers are live.</summary>
    bool IsReady { get; }

    /// <summary>Current map identity (map name or workshop id), empty before the first map load.</summary>
    string CurrentMap { get; }

    /// <summary>Workshop id of the current map, or empty for plain (non-workshop) maps.</summary>
    string CurrentWorkshopId { get; }

    /// <summary>The scheduled next map's display name, or null when no change is pending.</summary>
    string? NextMap { get; }

    /// <summary>True while a map change has been decided but not yet executed.</summary>
    bool MapChangeScheduled { get; }

    /// <summary>True while an end-of-map / RTV / admin vote is collecting ballots.</summary>
    bool VoteInProgress { get; }

    /// <summary>How many map extends remain on the current map.</summary>
    int ExtendsLeft { get; }

    /// <summary>
    /// Seconds remaining under <c>mp_timelimit</c>, or null when the server is round-based,
    /// the engine snapshot is unavailable, or the runtime isn't ready.
    /// </summary>
    int? TimeLeftSeconds { get; }

    /// <summary>
    /// Rounds remaining under <c>mp_maxrounds</c>, or null when the server is time-based or
    /// the game rules aren't readable right now.
    /// </summary>
    int? RoundsLeft { get; }

    /// <summary>Snapshot of the configured map list with per-map cooldown state.</summary>
    IReadOnlyList<MapChangerMapInfo> GetMaps();

    /// <summary>Current nominations (player slot → map display name).</summary>
    IReadOnlyDictionary<int, string> GetNominations();

    /// <summary>
    /// The HudKit shared interface MapChanger renders its vote menus with (HudKit.Core's public
    /// API). Exposed so dependent plugins can compose matching HUD elements without resolving
    /// <see cref="IHudKit" /> themselves. Null while HudKit isn't injected.
    /// </summary>
    IHudKit? HudKit { get; }

    // -------------------------------------------------------------- actions

    /// <summary>
    /// Schedule a map change by display name or id. Equivalent to an admin setting the next map:
    /// the change applies immediately when <paramref name="changeImmediately" /> is set or the
    /// match has already ended, otherwise at the next round end / match end.
    /// </summary>
    /// <returns>False when the map isn't in the configured list or the runtime isn't ready.</returns>
    bool ScheduleMapChange(string mapNameOrId, bool changeImmediately = false);

    /// <summary>Force-start an end-of-map style vote. No-op while another vote is active.</summary>
    /// <returns>False when a vote is already running or the runtime isn't ready.</returns>
    bool StartVote(int voteDurationSeconds, int mapsToShow);

    /// <summary>Cancel the currently running vote, if any.</summary>
    void CancelVote();

    /// <summary>Reopen the active vote menu for one player (same as <c>!revote</c>).</summary>
    /// <returns>False when no vote is active or the player isn't eligible.</returns>
    bool OpenVoteMenu(IPlayer player);

    /// <summary>
    /// Extend the current map by the configured steps, consuming one extend.
    /// </summary>
    /// <returns>False when no extends remain or the runtime isn't ready.</returns>
    bool ExtendCurrentMap();

    // --------------------------------------------------------------- events

    /// <summary>Raised when a next-map decision is made (scheduled or immediate).</summary>
    event Action<string>? NextMapDecided;

    /// <summary>Raised when a vote starts collecting ballots.</summary>
    event Action? VoteStarted;

    /// <summary>
    /// Raised when a vote concludes. First argument is the outcome; second is the winning map's
    /// display name, or null for extend / no-votes / cancelled outcomes.
    /// </summary>
    event Action<MapChangerVoteResult, string?>? VoteEnded;

    /// <summary>Raised right before MapChanger issues the actual map-change commands.</summary>
    event Action<string>? MapChanging;
}
