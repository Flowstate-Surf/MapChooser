using System;
using SwiftlyS2.Shared.Players;
using HudKit.Shared;

namespace MapChanger.Menu;

/// <summary>
/// One row of a <see cref="MapChooserHudMenuService"/> menu.
/// </summary>
/// <param name="Label">Text shown on the row (already localized/formatted by the caller).</param>
/// <param name="Enabled">
/// False renders the row greyed out and unclickable (e.g. a map currently in cooldown) — the row
/// still occupies a slot so players can see it exists and why it can't be picked right now.
/// </param>
/// <param name="OnSelect">Invoked with the viewer when they click this row.</param>
public sealed record MapChooserHudOption(string Label, bool Enabled, Action<IPlayer> OnSelect);
