using System;
using System.Collections.Generic;
using System.Linq;
using HudKit.Shared;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MapChanger.Menu;

/// <summary>
/// Drives <see cref="MapChooserMenuTemplate"/> for every click-based menu in MapChanger (RTV /
/// end-of-map vote, nominate, votemap, setnextmap, cycle, admin pickers). One HudKit layout
/// instance per viewer at a time — showing a new menu for a player replaces whatever MapChooser
/// menu they already had open, mirroring how FlowtimerS2's MenuHud service manages its single
/// per-player HUD instance.
///
/// The panorama layout only has 8 row slots compiled in, so lists longer than that are paged:
/// each page shows up to 6 content rows plus "◄ Prev" / "Next ►" rows pinned to the last two
/// slots. Single-page menus use all 8 slots for content (preserving the old behavior for votes),
/// and may append a "◄ Back" row for sub-menus.
/// </summary>
public sealed partial class MapChooserHudMenuService
{
    private readonly ISwiftlyCore _core;
    private IHudKit _hudKit;

    public void SetHudKit(IHudKit hudKit) { CloseAll(); _hudKit = hudKit; }

    private sealed class MenuState
    {
        public required string Title;
        public required List<MapChooserHudOption> Options;
        public int Page;
        public ulong SteamId;
        public bool AllowClose;
        public bool CapturesMouse;
    }

    private readonly Dictionary<int, IHudHandle> _handles = new();
    private readonly Dictionary<int, MenuState> _menus = new();

    /// <summary>Row slots in the compiled layout.</summary>
    private const int TotalRows = 8;

    public MapChooserHudMenuService(ISwiftlyCore core, IHudKit hudKit)
    {
        _core = core;
        _hudKit = hudKit;
    }

    /// <summary>True if this viewer currently has a MapChooser menu open.</summary>
    public bool IsOpen(IPlayer player) => player.IsValid && _handles.ContainsKey(player.Slot);

    /// <summary>
    /// Spawns (or replaces) the viewer's MapChooser menu with <paramref name="title"/> and
    /// <paramref name="options"/>. Lists longer than a page are paged automatically.
    /// </summary>
    /// <param name="onBack">When set, a "◄ Back" row is appended that invokes it (for sub-menus).</param>
    public void Show(IPlayer player, string title, List<MapChooserHudOption> options, Action<IPlayer>? onBack = null, bool allowClose = true, bool capturesMouse = true)
    {
        if (!player.IsValid) return;
        int slot = player.Slot;

        // Despawn any menu this viewer already had open before spawning the new one — HudKit keys
        // active layouts by LayoutName, so re-showing the same layout would replace it anyway, but
        // this also clears our own per-slot bookkeeping up front.
        Close(slot);

        var entries = new List<MapChooserHudOption>(options);
        if (onBack is not null) entries.Add(new MapChooserHudOption("◄ Back", true, onBack));
        var state = new MenuState { Title = title, Options = entries, Page = 0, SteamId = player.SteamID, AllowClose = allowClose, CapturesMouse = capturesMouse };
        _menus[slot] = state;
        Render(player, slot, state);
    }

    /// <summary>Despawns the viewer's MapChooser menu, if one is open. Safe to call when none is open.</summary>
    public void Close(IPlayer player)
    {
        if (!player.IsValid) return;
        Close(player.Slot);
    }

    private void Close(int slot)
    {
        if (_handles.Remove(slot, out var handle))
            handle.Close();
        _menus.Remove(slot);
        _pictureMenus.Remove(slot);
    }

    /// <summary>Despawns every viewer's MapChooser menu — used on map shutdown/vote reset.</summary>
    public void CloseAll()
    {
        foreach (var handle in _handles.Values)
            handle.Close();
        _handles.Clear();
        _menus.Clear();
        _pictureMenus.Clear();
    }

    // Called during world teardown: do not send close messages to departing clients.
    public void ForgetAll()
    {
        _handles.Clear();
        _menus.Clear();
        _pictureMenus.Clear();
    }

    // Content rows per page: when paged, the last two slots are reserved for Prev / Next.
    private static int ContentRowsPerPage(bool paged) => paged ? TotalRows - 2 : TotalRows;

    private static int PageCount(MenuState s)
    {
        if (s.Options.Count <= TotalRows) return 1;
        int content = TotalRows - 2; // worst-case (paged) capacity so the page count never changes mid-navigation
        return Math.Max(1, (int)Math.Ceiling(s.Options.Count / (double)content));
    }

    private void Render(IPlayer player, int slot, MenuState state)
    {
        int pageCount = PageCount(state);
        state.Page = Math.Clamp(state.Page, 0, pageCount - 1);
        bool paged = pageCount > 1;
        int contentRows = ContentRowsPerPage(paged);
        int start = state.Page * contentRows;

        var template = new MapChooserMenuTemplate(buttonId => { if (state.CapturesMouse) OnButtonClicked(slot, buttonId); }, state.CapturesMouse);
        template.Title(state.Title);
        template.Class("menu_close", "hidden", !state.AllowClose);

        int row = 0;
        for (int i = 0; i < contentRows; i++, row++)
        {
            int index = start + i;
            if (index < state.Options.Count)
                template.Option(row, state.Options[index].Label, disabled: !state.Options[index].Enabled);
            else
                template.HideOption(row);
        }

        if (paged)
        {
            // Pinned nav rows in the last two slots, greyed out at the ends.
            template.Option(row++, "◄ Prev", disabled: state.Page == 0);
            template.Option(row++, "Next ►", disabled: state.Page >= pageCount - 1);
        }
        for (; row < TotalRows; row++)
            template.HideOption(row);

        // Re-showing the same LayoutName redraws the already-open handle in place (HudKit Show),
        // so the player's cursor doesn't recenter on every page flip.
        _handles[slot] = _hudKit.Show(player, template);
    }

    /// <summary>Chat-number input is accepted only for this player's active passive vote.</summary>
    public bool SelectNumber(IPlayer player, int number)
    {
        if (!player.IsValid || number < 0 || number > TotalRows
            || !_menus.TryGetValue(player.Slot, out var state)
            || state.CapturesMouse || state.SteamId != player.SteamID) return false;
        OnButtonClicked(player.Slot, number == 0 ? "menu_close" : $"option_{number - 1}");
        return true;
    }

    private void OnButtonClicked(int slot, string buttonId)
    {
        if (!_menus.TryGetValue(slot, out var state)) return;
        var viewer = _core.PlayerManager.GetPlayer(slot);
        if (viewer is not { IsValid: true } || viewer.SteamID != state.SteamId) { Close(slot); return; }
        if (buttonId == "menu_close") { if (state.AllowClose) Close(slot); return; }
        if (!buttonId.StartsWith("option_", StringComparison.Ordinal)) return;
        if (!int.TryParse(buttonId.AsSpan("option_".Length), out int row)) return;

        int pageCount = PageCount(state);
        bool paged = pageCount > 1;
        int contentRows = ContentRowsPerPage(paged);

        // Nav rows live in the last two slots when paged.
        if (paged && row == TotalRows - 2) // Prev
        {
            if (state.Page > 0) state.Page--;
            var p = _core.PlayerManager.GetPlayer(slot);
            if (p is not null && p.IsValid) Render(p, slot, state);
            return;
        }
        if (paged && row == TotalRows - 1) // Next
        {
            if (state.Page < pageCount - 1) state.Page++;
            var p = _core.PlayerManager.GetPlayer(slot);
            if (p is not null && p.IsValid) Render(p, slot, state);
            return;
        }
        if (row < 0 || row >= contentRows) return;
        int index = state.Page * contentRows + row;
        if (index < 0 || index >= state.Options.Count) return;

        var option = state.Options[index];
        // The disabled/greyed-out row still occupies a slot and can still be clicked at the
        // Panorama layer (CSS "disabled" class is visual only) — enforce the real rule server-side.
        if (!option.Enabled) return;

        // Defer off the click-dispatch callstack: the callback often calls back into Show/Close on
        // this very service (e.g. registering a vote closes the menu), which shouldn't happen
        // re-entrantly from inside HudKit's own click dispatch. Same reasoning as FlowtimerS2's
        // MenuHud service (see MenuHud.cs TryHandleMenuHudClick's use of NextWorldUpdate).
        _core.Scheduler.NextWorldUpdate(() =>
        {
            var player = _core.PlayerManager.GetPlayer(slot);
            if (player is null || !player.IsValid || player.SteamID != state.SteamId
                || !_menus.TryGetValue(slot, out var current) || !ReferenceEquals(current, state)) return;
            option.OnSelect(player);
        });
    }
}