namespace MapChanger.Contracts;

/// <summary>
/// Read-only snapshot of one map entry from MapChanger's configured map list.
/// </summary>
/// <param name="Name">Display name shown in menus and chat.</param>
/// <param name="Id">
/// Map identifier — either the plain map name (e.g. <c>de_dust2</c>) or a workshop id
/// (<c>3124567099</c> / <c>ws:3124567099</c>).
/// </param>
/// <param name="Tier">Skill tier used by the tiered nomination menu (0 = untiered).</param>
/// <param name="MinPlayers">Minimum real players required for this map to be eligible (0 = no minimum).</param>
/// <param name="MaxPlayers">Maximum real players allowed for this map to be eligible (0 = no maximum).</param>
/// <param name="InCooldown">True while the map is inside the recently-played cooldown window.</param>
public sealed record MapChangerMapInfo(
    string Name,
    string? Id,
    int Tier,
    int MinPlayers,
    int MaxPlayers,
    bool InCooldown);
