namespace MapChanger.Contracts;

/// <summary>
/// Reason an end-of-map / RTV vote concluded. Surfaced through
/// <see cref="IMapChangerApi.VoteEnded" />.
/// </summary>
public enum MapChangerVoteResult
{
    /// <summary>A map won the vote and is now the scheduled next map.</summary>
    MapWon,

    /// <summary>The "extend current map" option won.</summary>
    ExtendWon,

    /// <summary>The vote ended without a winner (e.g. an RTV vote with zero ballots).</summary>
    NoVotes,

    /// <summary>The vote was cancelled before completing (admin cancel / map shutdown).</summary>
    Cancelled,
}
