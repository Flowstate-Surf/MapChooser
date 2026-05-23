using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using System.Threading.Tasks;

namespace MapChanger.Menu;

public class AdminMapsVoteMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;
    private readonly HashSet<string> _selectedMaps = new();
    private readonly NominationConfig _nominationConfig;

    public AdminMapsVoteMenu(ISwiftlyCore core, MapLister mapLister, NominationConfig nominationConfig)
    {
        _core = core;
        _mapLister = mapLister;
        _nominationConfig = nominationConfig;
    }

    public void Show(IPlayer player, Action<IPlayer, List<string>> onStartVote)
    {
        var oldMenu = _core.MenusAPI.GetCurrentMenu(player);
        int? indexToRestore = null;
        if (oldMenu?.Tag?.ToString() == "AdminMapsVoteMenu")
        {
            indexToRestore = oldMenu.GetCurrentOptionIndex(player);
        }

        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var builder = _core.MenusAPI.CreateBuilder();
        
        string title = "Select Maps for Vote:";
        try { title = localizer["map_chooser.admin_vote.title"]; } catch { }
        builder.Design.SetMenuTitle($"{title} ({_selectedMaps.Count})");

        // Start Vote Option
        string startText = "START VOTE";
        try { startText = localizer["map_chooser.admin_vote.start"]; } catch { }
        var startOption = new ButtonMenuOption($"<font color='orange'>{startText}</font>");
        startOption.Enabled = _selectedMaps.Count > 0;
        startOption.Click += (sender, args) =>
        {
            _core.Scheduler.NextTick(() =>
            {
                var currentMenu = _core.MenusAPI.GetCurrentMenu(args.Player);
                if (currentMenu != null)
                {
                    _core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                }

                StartCountdown(args.Player, 5, onStartVote);
            });
            return ValueTask.CompletedTask;
        };
        builder.AddOption(startOption);

        // Map Options
        foreach (var map in MapFilter.Apply(_mapLister.Maps, _nominationConfig))
        {
            bool isSelected = _selectedMaps.Contains(map.Name);
            var option = new ButtonMenuOption($"{(isSelected ? "<font color='green'>[X]</font> " : "<font color='red'>[ ]</font> ")}{map.Name}");
            option.Click += (sender, args) =>
            {
                if (isSelected)
                    _selectedMaps.Remove(map.Name);
                else
                    _selectedMaps.Add(map.Name);

                _core.Scheduler.NextTick(() => Show(player, onStartVote));
                return ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        var menu = builder.Build();
        menu.Tag = "AdminMapsVoteMenu";
        _core.MenusAPI.OpenMenuForPlayer(player, menu);

        if (indexToRestore.HasValue && indexToRestore.Value != -1)
        {
            _core.Scheduler.NextTick(() => menu.MoveToOptionIndex(player, indexToRestore.Value));
        }
    }

    private void StartCountdown(IPlayer admin, int seconds, Action<IPlayer, List<string>> onStartVote)
    {
        if (seconds <= 0)
        {
            onStartVote(admin, _selectedMaps.ToList());
            return;
        }

        var localizer = _core.Translation.GetPlayerLocalizer(admin);
        _core.PlayerManager.SendChat($"{localizer["map_chooser.prefix"]} Vote starting in [red]{seconds}[default] seconds...");
        
        _core.Scheduler.DelayBySeconds(1, () => StartCountdown(admin, seconds - 1, onStartVote));
    }
}
