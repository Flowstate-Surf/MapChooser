using MapChanger.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;

namespace MapChanger.Commands;

public class RemoveMapCommand
{
    private readonly ISwiftlyCore _core;
    private readonly MapCycleManager _cycleManager;

    public RemoveMapCommand(ISwiftlyCore core, MapCycleManager cycleManager)
    {
        _core = core;
        _cycleManager = cycleManager;
    }

    public void Execute(ICommandContext context)
    {
        if (context.Args.Length < 1)
        {
            context.Reply(_core.Localizer["map_chooser.removemap.usage"]);
            return;
        }

        string name = string.Join(" ", context.Args);

        bool removed = _cycleManager.RemoveMap(name);
        if (!removed)
        {
            context.Reply(_core.Localizer["map_chooser.cycle.not_found", name]);
            return;
        }

        _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.cycle.map_removed", name]);
    }
}
