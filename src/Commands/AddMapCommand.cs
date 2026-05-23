using MapChanger.Helpers;
using MapChanger.Models;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;

namespace MapChanger.Commands;

public class AddMapCommand
{
    private readonly ISwiftlyCore _core;
    private readonly MapCycleManager _cycleManager;

    public AddMapCommand(ISwiftlyCore core, MapCycleManager cycleManager)
    {
        _core = core;
        _cycleManager = cycleManager;
    }

    public void Execute(ICommandContext context)
    {
        if (context.Args.Length < 1)
        {
            context.Reply(_core.Localizer["map_chooser.addmap.usage"]);
            return;
        }

        string name = context.Args[0];
        string id = context.Args.Length > 1 ? context.Args[1] : name;

        bool added = _cycleManager.AddMap(name, id);
        if (!added)
        {
            context.Reply(_core.Localizer["map_chooser.cycle.already_exists", name]);
            return;
        }

        _core.PlayerManager.SendChat(_core.Localizer["map_chooser.prefix"] + " " + _core.Localizer["map_chooser.cycle.map_added", name]);
    }
}
