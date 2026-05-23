namespace MapChanger.Models;

public class Map
{
    public string Name { get; set; } = string.Empty;
    public string? Id { get; set; }
    public int MinPlayers { get; set; } = 0;
    public int MaxPlayers { get; set; } = 0;
    public int Tier { get; set; } = 0;

    public Map() { }

    public Map(string name, string? id = null)
    {
        Name = name.Trim();
        Id = id?.Trim();
    }

    public bool IsValidForPlayerCount(int playerCount)
    {
        if (MinPlayers > 0 && playerCount < MinPlayers) return false;
        if (MaxPlayers > 0 && playerCount > MaxPlayers) return false;
        return true;
    }
}
