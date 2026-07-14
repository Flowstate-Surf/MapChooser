using MapChanger.Models;

namespace MapChanger.Dependencies;

public class PluginState
{
    public bool MapChangeScheduled { get; set; }
    public bool EofVoteHappening { get; set; }
    public bool CommandsDisabled { get; set; }
    public string? NextMap { get; set; }
    public bool ChangeMapImmediately { get; set; }
    public bool IsRtv { get; set; }
    public float MapStartTime { get; set; }
    public int RoundsPlayed { get; set; }
    public bool WarmupRunning { get; set; }
    public int ExtendsLeft { get; set; }
    public int NextEofVotePossibleRound { get; set; }
    public float NextEofVotePossibleTime { get; set; }
    public DateTime? RtvCooldownEndTime { get; set; }
    public DateTime? ExtendVoteCooldownEndTime { get; set; }
    public bool MatchEnded { get; set; }
    public bool EofVoteCompleted { get; set; }
    public bool ChangeMapFallbackInProgress { get; set; }
    public Dictionary<int, string> Nominations { get; set; } = new();
    public string CurrentMapId { get; set; } = "";
    public string CurrentWorkshopId { get; set; } = "";
}
