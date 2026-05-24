using MapChanger.Models;
using SwiftlyS2.Shared;

namespace MapChanger.Helpers;

public class MapCooldown
{
    private List<string> _mapsOnCooldown = new();
    private readonly MapChangerConfig _config;
    private readonly ISwiftlyCore _core;

    public MapCooldown(ISwiftlyCore core, MapChangerConfig config)
    {
        _core = core;
        _config = config;
    }

    public void OnMapStart(string mapName, string? workshopId = null)
    {
        if (_config.MapsInCooldown <= 0)
        {
            _mapsOnCooldown.Clear();
            return;
        }

        string identity = (!string.IsNullOrEmpty(workshopId) ? workshopId : mapName).Trim().ToLower();
        
        if (_mapsOnCooldown.Contains(identity))
            _mapsOnCooldown.Remove(identity);

        _mapsOnCooldown.Add(identity);

        int limit = _config.MapsInCooldown + 1;
        while (_mapsOnCooldown.Count > limit)
        {
            _mapsOnCooldown.RemoveAt(0);
        }
    }

    public bool IsMapInCooldown(string mapIdentity)
    {
        string identity = mapIdentity.Trim().ToLower();
        
        // Find if this identity exists in history
        if (!_mapsOnCooldown.Contains(identity)) return false;

        // Verify it's not the current map
        if (_core.Engine == null) return false;

        string currentMapName;
        string currentWorkshopId;
        try
        {
            currentMapName = _core.Engine.GlobalVars.MapName.ToString()?.ToLower() ?? "";
            currentWorkshopId = _core.Engine.WorkshopId?.ToLower() ?? "";
        }
        catch
        {
            return false;
        }

        if (identity == currentMapName || (!string.IsNullOrEmpty(currentWorkshopId) && identity == currentWorkshopId))
            return false;

        return true;
    }

    public bool IsMapInCooldown(Map map)
    {
        if (map.Id != null && IsMapInCooldown(map.Id)) return true;
        if (IsMapInCooldown(map.Name)) return true;
        return false;
    }

    public void AddMapToCooldown(string mapIdentity)
    {
        string identity = mapIdentity.Trim().ToLower();
        if (!_mapsOnCooldown.Contains(identity))
        {
            _mapsOnCooldown.Add(identity);
            int limit = _config.MapsInCooldown + 1;
            while (_mapsOnCooldown.Count > limit && limit > 0)
            {
                _mapsOnCooldown.RemoveAt(0);
            }
        }
    }
}

