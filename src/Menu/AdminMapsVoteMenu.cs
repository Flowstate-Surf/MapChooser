using MapChanger.Models;
using MapChanger.Dependencies;
using MapChanger.Helpers;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Menu;

/// <summary>
/// HudKit-based admin multi-select menu for the !mapsvote command. Each map toggles in/out of
/// <see cref="_selectedMaps"/>; a "START VOTE" row (enabled once anything is selected) kicks off
/// the countdown. Ported off the native MenusAPI; selection state is kept per menu instance so it
/// survives the re-render after each toggle.
/// </summary>
public class AdminMapsVoteMenu
{
    private readonly ISwiftlyCore _core;
    private readonly MapLister _mapLister;
    private readonly HashSet<string> _selectedMaps = new();
    private readonly NominationConfig _nominationConfig;
    private readonly MapChooserHudMenuService _hudMenu;

    public AdminMapsVoteMenu(ISwiftlyCore core, MapLister mapLister, NominationConfig nominationConfig, MapChooserHudMenuService hudMenu)
    {
        _core = core;
        _mapLister = mapLister;
        _nominationConfig = nominationConfig;
        _hudMenu = hudMenu;
    }

    public void Show(IPlayer player, Action<IPlayer, List<string>> onStartVote)
    {
        var localizer = _core.Translation.GetPlayerLocalizer(player);

        string title = "Select Maps for Vote:";
        try { title = localizer["map_chooser.admin_vote.title"]; } catch { }

        string startText = "START VOTE";
        try { startText = localizer["map_chooser.admin_vote.start"]; } catch { }

        var options = new List<MapChooserHudOption>
        {
            new MapChooserHudOption(
                $"{startText} ({_selectedMaps.Count})",
                Enabled: _selectedMaps.Count > 0,
                p =>
                {
                    _hudMenu.Close(p);
                    StartCountdown(p, 5, onStartVote);
                })
        };

        foreach (var map in MapFilter.Apply(_mapLister.Maps, _nominationConfig))
        {
            bool isSelected = _selectedMaps.Contains(map.Name);
            var captured = map;
            options.Add(new MapChooserHudOption(
                $"{(isSelected ? "[X] " : "[ ] ")}{captured.Name}",
                Enabled: true,
                p =>
                {
                    if (!_selectedMaps.Remove(captured.Name))
                        _selectedMaps.Add(captured.Name);
                    // Re-render so the checkbox state + START VOTE enable refresh.
                    Show(p, onStartVote);
                }));
        }

        _hudMenu.Show(player, title, options);
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