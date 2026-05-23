using MapChanger.Models;
using MapChanger.Dependencies;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace MapChanger.Helpers;

public class ExtendManager
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly MapChangerConfig _config;
    private readonly VoteManager _extVoteManager;

    public ExtendManager(ISwiftlyCore core, PluginState state, MapChangerConfig config, VoteManager extVoteManager)
    {
        _core = core;
        _state = state;
        _config = config;
        _extVoteManager = extVoteManager;
    }

    public void ExtendMap(int minutes, int rounds)
    {
        if (_state.ExtendsLeft <= 0) return;

        bool extendedTime = false;
        bool extendedRounds = false;

        if (minutes > 0)
        {
            var timelimitConVar = _core.ConVar.Find<float>("mp_timelimit");
            if (timelimitConVar != null && timelimitConVar.Value > 0)
            {
                timelimitConVar.Value += minutes;
                extendedTime = true;

                // Schema-based round-time bump: immediately advances the HUD timer
                // by writing to CCSGameRules.m_iRoundTime via the SetSchemaHammerUniqueId
                // patch applied by the SwiftlyS2 host.
                try
                {
                    var proxy = _core.EntitySystem
                        .GetAllEntitiesByClass<CCSGameRulesProxy>()
                        .FirstOrDefault();

                    var gameRules = proxy?.GameRules;
                    if (gameRules != null)
                    {
                        gameRules.RoundTime += minutes * 60;
                        gameRules.RoundTimeUpdated();
                    }
                }
                catch (Exception ex)
                {
                    _core.Logger.LogWarning(ex, "MapChanger: schema round-time bump failed (patch not applied?)");
                }
            }
        }

        if (rounds > 0)
        {
            var maxroundsConVar = _core.ConVar.Find<int>("mp_maxrounds");
            if (maxroundsConVar != null && maxroundsConVar.Value > 0)
            {
                maxroundsConVar.Value += rounds;
                extendedRounds = true;
            }

            var winlimitConVar = _core.ConVar.Find<int>("mp_winlimit");
            if (winlimitConVar != null && winlimitConVar.Value > 0)
            {
                winlimitConVar.Value += (int)Math.Ceiling(rounds / 2.0);
                extendedRounds = true;
            }
        }

        _state.MapChangeScheduled = false;
        _state.EofVoteCompleted = false;
        try
        {
            _state.NextEofVotePossibleRound = _core.Game.MatchData.TerroristScoreTotal + _core.Game.MatchData.CTScoreTotal + 1;
        }
        catch (InvalidOperationException ex)
        {
            _core.Logger.LogWarning(ex, "GameRules not available in ExtendManager - leaving NextEofVotePossibleRound unchanged");
        }
        if (_core.Engine?.GlobalVars != null)
            _state.NextEofVotePossibleTime = _core.Engine.GlobalVars.CurrentTime + 60.0f;

        if (extendedTime || extendedRounds)
        {
            _state.ExtendsLeft--;

            // Any successful extend (vote, EOF menu pick, etc.) clears stale
            // extend-vote ballots and starts the configured cooldown so the
            // next !ext / !ve attempt has fresh state.
            _extVoteManager.Clear();
            if (_config.ExtendMap.CooldownDuration > 0)
                _state.ExtendVoteCooldownEndTime = DateTime.Now.AddSeconds(_config.ExtendMap.CooldownDuration);

            if (extendedTime && extendedRounds)
                _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.vote.map_extended_both", minutes, rounds, _state.ExtendsLeft]);
            else if (extendedTime)
                _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.vote.map_extended_time", minutes, _state.ExtendsLeft]);
            else if (extendedRounds)
                _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.vote.map_extended_rounds", rounds, _state.ExtendsLeft]);
        }
    }
}
