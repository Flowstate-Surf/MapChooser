using SwiftlyS2.Shared;

namespace MapChanger.Helpers;

/// <summary>
/// Safe accessors for engine state that may be unavailable during map transitions.
/// Ported from upstream MapChooser v1.3.0 (src/Helpers/EngineHelpers.cs).
/// </summary>
internal static class EngineHelpers
{
    /// <summary>Engine deref — call only from gameplay events or a Core.Scheduler.NextWorldUpdate callback (NOT from OnMapLoad/OnMapUnload).</summary>
    public static bool TryGetEngineSnapshot(this ISwiftlyCore core, out string mapName, out string workshopId, out float currentTime)
    {
        mapName = string.Empty;
        workshopId = string.Empty;
        currentTime = 0f;

        try
        {
            if (core.Engine is not { } engine) return false;
            var gv = engine.GlobalVars;
            mapName = gv.MapName.ToString() ?? string.Empty;
            workshopId = engine.WorkshopId ?? string.Empty;
            currentTime = gv.CurrentTime;
            return true;
        }
        catch
        {
            mapName = string.Empty;
            workshopId = string.Empty;
            currentTime = 0f;
            return false;
        }
    }

    /// <summary>Engine deref — call only from gameplay events or a Core.Scheduler.NextWorldUpdate callback (NOT from OnMapLoad/OnMapUnload).</summary>
    public static float TryGetCurrentTime(this ISwiftlyCore core)
    {
        try
        {
            if (core.Engine is { } engine)
                return engine.GlobalVars.CurrentTime;
        }
        catch { }
        return 0f;
    }

    /// <summary>Engine deref — call only from gameplay events or a Core.Scheduler.NextWorldUpdate callback (NOT from OnMapLoad/OnMapUnload).</summary>
    public static string TryGetWorkshopId(this ISwiftlyCore core)
    {
        try
        {
            if (core.Engine is { } engine)
                return engine.WorkshopId ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    /// <summary>Engine deref — the current map's name, or empty when the engine isn't readable.</summary>
    public static string TryGetCurrentMapName(this ISwiftlyCore core)
    {
        try
        {
            if (core.Engine is { } engine)
                return engine.GlobalVars.MapName.ToString() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }
}
