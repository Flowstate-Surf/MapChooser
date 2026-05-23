using MapChanger.Models;

namespace MapChanger.Helpers;

public class MapLister
{
    private List<Map> _maps = new();

    public IReadOnlyList<Map> Maps => _maps;

    public void UpdateMaps(List<Map> maps)
    {
        _maps = maps ?? new List<Map>();
    }

    public void AddMap(Map map)
    {
        _maps.Add(map);
    }

    public bool RemoveMap(string name)
    {
        int idx = _maps.FindIndex(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;
        _maps.RemoveAt(idx);
        return true;
    }

    public bool MoveUp(string name)
    {
        int idx = _maps.FindIndex(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (idx <= 0) return false;
        (_maps[idx], _maps[idx - 1]) = (_maps[idx - 1], _maps[idx]);
        return true;
    }

    public bool MoveDown(string name)
    {
        int idx = _maps.FindIndex(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx >= _maps.Count - 1) return false;
        (_maps[idx], _maps[idx + 1]) = (_maps[idx + 1], _maps[idx]);
        return true;
    }
}
