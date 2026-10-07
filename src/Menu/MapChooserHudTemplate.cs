using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HudKit.Shared;

namespace MapChanger.Menu;

/// <summary>
/// Small helper base so MapChanger's HUD templates can call <c>Set</c>/<c>Class</c> imperatively
/// (like a scratch buffer) instead of re-deriving every dialog variable inside
/// <see cref="Apply(IHudSurface)"/> by hand. Mirrors the same pattern FlowtimerS2 uses for its own
/// HudKit templates (see FlowtimerHudTemplate.cs) so the two plugins' HUD code stay recognisable
/// to anyone jumping between them.
/// </summary>
public abstract class MapChooserHudTemplate : HudTemplate
{
    private readonly Dictionary<(string Panel, string Name), string> _values = new();
    private readonly Dictionary<(string Panel, string Name), bool> _classes = new();

    protected MapChooserHudTemplate(Action<string>? onClick = null)
    {
        if (onClick is not null)
            OnClickHandler = context => onClick(context.ButtonId);
    }

    public void Set(string panel, string name, string value) => _values[(panel, name)] = value;

    public void Class(string panel, string name, bool enabled) => _classes[(panel, name)] = enabled;

    public override void Apply(IHudSurface surface)
    {
        // HudKit's Close() hides a HUD by clearing this class, so the root has to opt back in here
        // every time the template is (re)applied.
        surface.Class(RootId, "show", true);

        foreach (var ((panel, name), value) in _values)
            surface.Set(panel, name, value);

        foreach (var ((panel, name), enabled) in _classes)
            surface.Class(panel, name, enabled);
    }
}

/// <summary>
/// Drives the compiled <c>panorama/layout/custom_game/mapchooser/menu.vxml_c</c> layout: a title,
/// a hint line, and up to <see cref="MaxOptions"/> selectable rows (<c>option_0</c>..<c>option_7</c>).
/// Used for every menu <see cref="MapChooserHudMenuService"/> shows — votemap/RTV/EOF/admin pickers
/// all reduce to "title + list of labelled, optionally-disabled choices".
/// </summary>
public sealed class MapChooserMenuTemplate : MapChooserHudTemplate
{
    public const int MaxOptions = 8;

    private readonly bool _capturesMouse;
    public MapChooserMenuTemplate(Action<string> onClick, bool capturesMouse = true) : base(onClick)
    {
        _capturesMouse = capturesMouse;
        Set(RootId, "hint", capturesMouse ? "CLICK AN OPTION TO SELECT" : "TYPE !1 – !8 IN CHAT TO VOTE");
        Set(RootId, "close_hint", capturesMouse ? "CLOSE" : "!0  CLOSE");
        Class(RootId, "passive-vote", !capturesMouse);
    }

    public override string LayoutName => "mapchooser/menu";

    public override string RootId => "mapchooser-root";

    /// <summary>Nomination/admin menus capture the mouse; automated votes use chat commands.</summary>
    public override bool CapturesInput => _capturesMouse;

    public MapChooserMenuTemplate Title(string title)
    {
        Set(RootId, "title", title);
        return this;
    }

    /// <param name="index">0-based row slot (0..<see cref="MaxOptions"/>-1).</param>
    public MapChooserMenuTemplate Option(int index, string label, bool disabled = false)
    {
        string panel = $"option_{index}";
        var info = ParseMapLabel(label);
        Set(RootId, panel, info.Name);
        Set(RootId, $"number_{index}", $"!{index + 1}");
        Set(RootId, $"tier_{index}", info.Tier);
        Set(RootId, $"bonus_{index}", info.Bonus);
        Set(RootId, $"type_{index}", info.Type);
        Class($"tier_{index}", "hidden", info.Tier.Length == 0);
        Class($"bonus_{index}", "hidden", info.Bonus.Length == 0);
        Class($"type_{index}", "hidden", info.Type.Length == 0);
        Class($"tier_{index}", "tier-one", info.Tier == "T1");
        Class($"tier_{index}", "tier-two", info.Tier == "T2");
        Class(panel, "disabled", disabled);
        Class(panel, "hidden", false);
        return this;
    }

    public MapChooserMenuTemplate HideOption(int index)
    {
        Class($"option_{index}", "hidden", true);
        return this;
    }

    // Display-only parsing: callbacks retain the full configured map name/ID.
    public static (string Name, string Tier, string Bonus, string Type) ParseMapLabel(string label)
    {
        var match = Regex.Match(label, @"^(?<name>(?:surf_|bhop_).+?)\s+T(?<tier>\d+)(?:\s*\|\s*(?<bonus>\d+B))?(?:\s*\|\s*(?<type>[LSH]))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success
            ? (match.Groups["name"].Value, "T" + match.Groups["tier"].Value, match.Groups["bonus"].Value.ToUpperInvariant(), match.Groups["type"].Value.ToUpperInvariant())
            : (label, "", "", "");
    }

    /// <summary>Row clicks arrive as their own panel id (<c>option_0</c>..<c>option_7</c>) — no remap needed.</summary>
    public override string MapButton(string buttonId) => buttonId.EndsWith("_label", StringComparison.Ordinal) ? buttonId[..^6] : buttonId;
}
