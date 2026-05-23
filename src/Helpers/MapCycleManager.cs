using MapChanger.Models;
using MapChanger.Dependencies;
using System.Text.Json;
using SwiftlyS2.Shared;

namespace MapChanger.Helpers;

public class MapCycleManager
{
    private readonly ISwiftlyCore _core;
    private readonly PluginState _state;
    private readonly MapLister _mapLister;
    private readonly ChangeMapManager _changeMapManager;
    private readonly MapChangerConfig _config;
    private readonly string _mapsFilePath;
    private int _cycleIndex = 0;

    public int CycleIndex => _cycleIndex;

    public MapCycleManager(ISwiftlyCore core, PluginState state, MapLister mapLister, ChangeMapManager changeMapManager, MapChangerConfig config, string mapsFilePath)
    {
        _core = core;
        _state = state;
        _mapLister = mapLister;
        _changeMapManager = changeMapManager;
        _config = config;
        _mapsFilePath = mapsFilePath;
    }

    public void OnMapStart(string mapName, string workshopId)
    {
        var maps = _mapLister.Maps;
        int idx = maps.ToList().FindIndex(m =>
            m.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(m.Id) && m.Id.Equals(mapName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(m.Id) && !string.IsNullOrEmpty(workshopId) &&
             m.Id.Equals(workshopId, StringComparison.OrdinalIgnoreCase)));
        if (idx >= 0) _cycleIndex = idx;
    }

    public Map? PreviewNextMap()
    {
        var maps = _mapLister.Maps;
        if (maps.Count == 0) return null;
        if (_config.Cycle.RandomOrder) return null;
        return maps[(_cycleIndex + 1) % maps.Count];
    }

    public void TriggerCycleChange()
    {
        var maps = _mapLister.Maps;
        if (maps.Count == 0)
        {
            _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.cycle.no_maps"]);
            return;
        }

        Map nextMap;
        if (_config.Cycle.RandomOrder)
        {
            var currentMapName = _core.Engine?.GlobalVars.MapName.ToString() ?? "";
            var currentWorkshopId = _core.Engine?.WorkshopId ?? "";
            var candidates = maps.Where(m =>
                !m.Name.Equals(currentMapName, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(m.Id) || !m.Id.Equals(currentMapName, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(m.Id) || string.IsNullOrEmpty(currentWorkshopId) ||
                 !m.Id.Equals(currentWorkshopId, StringComparison.OrdinalIgnoreCase))
            ).ToList();
            nextMap = candidates.Count > 0
                ? candidates[new Random().Next(candidates.Count)]
                : maps[0];
        }
        else
        {
            nextMap = maps[(_cycleIndex + 1) % maps.Count];
        }

        _changeMapManager.ScheduleMapChange(nextMap.Name, true, false);
    }

    public bool AddMap(string name, string id)
    {
        if (_mapLister.Maps.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return false;

        _mapLister.AddMap(new Map(name, id));
        SaveMaps();
        return true;
    }

    public bool RemoveMap(string name)
    {
        bool removed = _mapLister.RemoveMap(name);
        if (removed)
        {
            if (_cycleIndex >= _mapLister.Maps.Count)
                _cycleIndex = Math.Max(0, _mapLister.Maps.Count - 1);
            SaveMaps();
        }
        return removed;
    }

    public bool MoveMapUp(string name)
    {
        int before = _mapLister.Maps.ToList().FindIndex(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        bool moved = _mapLister.MoveUp(name);
        if (moved)
        {
            if (_cycleIndex == before) _cycleIndex = before - 1;
            else if (_cycleIndex == before - 1) _cycleIndex = before;
            SaveMaps();
        }
        return moved;
    }

    public bool MoveMapDown(string name)
    {
        int before = _mapLister.Maps.ToList().FindIndex(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        bool moved = _mapLister.MoveDown(name);
        if (moved)
        {
            if (_cycleIndex == before) _cycleIndex = before + 1;
            else if (_cycleIndex == before + 1) _cycleIndex = before;
            SaveMaps();
        }
        return moved;
    }

    private void SaveMaps()
    {
        var maps = _mapLister.Maps.Select(m =>
        {
            var entry = new Dictionary<string, object?> { ["Name"] = m.Name, ["Id"] = m.Id };
            if (m.MinPlayers > 0) entry["MinPlayers"] = m.MinPlayers;
            if (m.MaxPlayers > 0) entry["MaxPlayers"] = m.MaxPlayers;
            return entry;
        }).ToList();

        var wrapper = new Dictionary<string, object>
        {
            ["MapChangerMaps"] = new Dictionary<string, object> { ["Maps"] = maps }
        };

        var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_mapsFilePath, json);
    }
}
