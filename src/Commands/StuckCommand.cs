using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Commands;

public class StuckCommand
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly VoteManager _voteManager;
    private readonly EndOfMapVoteManager _eofManager;
    private readonly MapLister _mapLister;
    private readonly MapChangerConfig _config;

    private readonly Dictionary<string, int> _chatVotes = new();
    private readonly Dictionary<int, string> _chatPlayerVotes = new();
    private List<string> _chatVoteMaps = new();
    private bool _chatVoteActive = false;
    private int _chatVoteSessionId = 0;
    private DateTime _chatVoteEndTime;

    public StuckCommand(ISwiftlyCore core, PluginState state, VoteManager voteManager, EndOfMapVoteManager eofManager, MapLister mapLister, MapChangerConfig config)
    {
        _core = core;
        _state = state;
        _voteManager = voteManager;
        _eofManager = eofManager;
        _mapLister = mapLister;
        _config = config;
    }

    public void Execute(ICommandContext context)
    {
        if (!_config.Rtv.Enabled) return;
        if (!context.IsSentByPlayer) return;

        if (_chatVoteActive && context.Args.Length > 0)
        {
            HandleChatVote(context);
            return;
        }

        var player = context.Sender!;
        var localizer = _core.Translation.GetPlayerLocalizer(player);

        if (_state.WarmupRunning && !_config.Rtv.EnabledInWarmup)
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.general.validation.warmup"]);
            return;
        }

        if (_config.Rtv.MinPlayers > 0)
        {
            int playerCount = _core.PlayerManager.GetAllPlayers().Count(p => p.IsValid && !p.IsFakeClient);
            if (playerCount < _config.Rtv.MinPlayers)
            {
                player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.general.validation.min_players", _config.Rtv.MinPlayers]);
                return;
            }
        }

        if (_config.Rtv.MinRounds > 0)
        {
            int totalRoundsPlayed;
            try
            {
                totalRoundsPlayed = _core.Game.MatchData.TerroristScoreTotal + _core.Game.MatchData.CTScoreTotal;
            }
            catch (InvalidOperationException ex)
            {
                _core.Logger.LogWarning(ex, "GameRules not available in StuckCommand - skipping MinRounds check");
                totalRoundsPlayed = _config.Rtv.MinRounds;
            }
            if (totalRoundsPlayed < _config.Rtv.MinRounds)
            {
                player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.general.validation.min_rounds", _config.Rtv.MinRounds - totalRoundsPlayed]);
                return;
            }
        }

        if (!_config.AllowSpectatorsToVote && player.Controller?.TeamNum == 1)
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.general.validation.spectator"]);
            return;
        }

        if (_state.EofVoteHappening)
        {
            if (!_eofManager.HasPlayerVoted(player.Slot))
            {
                _eofManager.OpenVoteMenu(player);
            }
            else
            {
                player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.rtv.already_voted"]);
            }
            return;
        }

        if (_state.RtvCooldownEndTime.HasValue)
        {
            var remaining = (_state.RtvCooldownEndTime.Value - DateTime.Now).TotalSeconds;
            if (remaining > 0)
            {
                player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.rtv.cooldown", (int)remaining]);
                return;
            }
            else
            {
                _state.RtvCooldownEndTime = null;
            }
        }

        if (_chatVoteActive)
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.stuck.vote_active"]);
            return;
        }

        if (_voteManager.AddVote(player.Slot))
        {
            var allPlayers = _core.PlayerManager.GetAllPlayers()
                .Where(p => p.IsValid && !p.IsFakeClient && (_config.AllowSpectatorsToVote || p.Controller?.TeamNum > 1))
                .ToList();
            int totalPlayers = allPlayers.Count;
            int needed = _voteManager.GetRequiredVotes(totalPlayers, _config.Rtv.VotePercentage);

            if (_config.AnnounceVotes)
                _core.PlayerManager.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.rtv.voted", player.Controller?.PlayerName ?? "Unknown", _voteManager.VoteCount, needed]);

            if (_voteManager.HasReached(totalPlayers, _config.Rtv.VotePercentage))
            {
                if (!string.IsNullOrWhiteSpace(_config.Rtv.VoteStyle) && _config.Rtv.VoteStyle.Equals("chat", StringComparison.OrdinalIgnoreCase))
                {
                    StartChatVote(_config.Rtv.VoteDuration, _config.Rtv.MapsToShow);
                }
                else
                {
                    _eofManager.StartVote(_config.Rtv.VoteDuration, _config.Rtv.MapsToShow, _config.Rtv.ChangeMapImmediately, isRtv: true);
                }
            }
        }
        else
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.rtv.already_voted"]);
        }
    }

    private void HandleChatVote(ICommandContext context)
    {
        if (!_chatVoteActive) return;
        if (!context.IsSentByPlayer) return;

        var player = context.Sender!;
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var mapName = context.Args[0].Trim();

        if (string.IsNullOrWhiteSpace(mapName))
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.stuck.usage"]);
            return;
        }

        if (!_chatVoteMaps.Any(m => m.Equals(mapName, StringComparison.OrdinalIgnoreCase)))
        {
            player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.stuck.invalid_map", mapName]);
            return;
        }

        int slot = player.Slot;
        if (_chatPlayerVotes.TryGetValue(slot, out var previousMap))
        {
            if (_chatVotes.TryGetValue(previousMap, out var previousCount))
                _chatVotes[previousMap] = Math.Max(0, previousCount - 1);
            _chatPlayerVotes.Remove(slot);
        }

        _chatPlayerVotes[slot] = mapName;
        _chatVotes[mapName] = _chatVotes.GetValueOrDefault(mapName) + 1;

        player.SendChat(localizer["map_chooser.prefix"] + " " + localizer["map_chooser.vote.you_voted", mapName]);
    }

    private void StartChatVote(int voteDuration, int mapsToShow)
    {
        if (_chatVoteActive) return;

        _chatVoteSessionId++;
        _chatVoteActive = true;
        _chatVotes.Clear();
        _chatPlayerVotes.Clear();
        _chatVoteMaps.Clear();

        var playerCount = _core.PlayerManager.GetAllPlayers().Count(p => p.IsValid && !p.IsFakeClient);
        var availableMaps = _mapLister.Maps
            .Where(m => m.IsValidForPlayerCount(playerCount))
            .ToList();

        if (!string.IsNullOrWhiteSpace(_state.CurrentMapId))
        {
            string currentMapId = _state.CurrentMapId;
            availableMaps = availableMaps
                .Where(m => !m.Name.Equals(currentMapId, StringComparison.OrdinalIgnoreCase) &&
                            (m.Id == null || !m.Id.Equals(currentMapId, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        availableMaps = MapFilter.Apply(availableMaps, _config.Nomination).ToList();
        _chatVoteMaps = availableMaps
            .Select(m => m.Name)
            .OrderBy(_ => Guid.NewGuid())
            .Take(Math.Max(1, mapsToShow))
            .ToList();

        foreach (var map in _chatVoteMaps)
            _chatVotes[map] = 0;

        _chatVoteEndTime = DateTime.Now.AddSeconds(voteDuration);
        _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.stuck.vote_started", string.Join(", ", _chatVoteMaps)]);
        _core.Scheduler.DelayBySeconds(voteDuration, () => EndChatVote(_chatVoteSessionId));
    }

    private void EndChatVote(int sessionId)
    {
        if (!_chatVoteActive || sessionId != _chatVoteSessionId) return;

        _chatVoteActive = false;

        string winner = _chatVotes
            .OrderByDescending(x => x.Value)
            .ThenBy(x => Guid.NewGuid())
            .Select(x => x.Key)
            .FirstOrDefault() ?? _chatVoteMaps.OrderBy(_ => Guid.NewGuid()).FirstOrDefault() ?? "";

        if (!string.IsNullOrWhiteSpace(winner))
        {
            _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.vote.ended", winner, _chatVotes.GetValueOrDefault(winner, 0)]);
            _eofManager.ResetVote();
            _eofManager.StartVote(_config.Rtv.VoteDuration, _config.Rtv.MapsToShow, _config.Rtv.ChangeMapImmediately, isRtv: true);
            _eofManager.ForceEnd();
        }
    }
}
