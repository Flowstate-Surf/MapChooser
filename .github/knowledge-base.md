# MapChanger — Knowledge Base

Workspace-specific notes that supplement the public SwiftlyS2-Toolkit skill.
Keep entries terse.

## Origin

- Ported from the MIT-licensed upstream
  [SwiftlyS2-Plugins/MapChooser](https://github.com/SwiftlyS2-Plugins/MapChooser)
  and rebranded `MapChooser` → `MapChanger` (namespace, assembly, plugin Id,
  config root key).
- Upstream copyright preserved verbatim in [LICENSE.MIT](LICENSE.MIT).

## Domain

- End-to-end map management: RTV, nominations, end-of-map vote, extend votes,
  manual votemap, admin change-map, map cycle auto-advance, cycle editing.

## Conventions

- Config root key in `config.jsonc`: `MapChangerMaps` (renamed from
  `MapChooserMaps`).
- Command aliases, vote thresholds, cooldowns, and per-map MinPlayers /
  MaxPlayers all live in [resources/templates/config.jsonc](resources/templates/config.jsonc)
  and [resources/templates/maps.jsonc](resources/templates/maps.jsonc).
- Translations are JSONC under [resources/translations](resources/translations).
- All long-lived state goes through `PluginState` in
  [src/Dependencies/PluginState.cs](src/Dependencies/PluginState.cs); manager
  singletons are constructed in [src/MapChanger.cs](src/MapChanger.cs).

## External integrations

- Workshop maps supported via `Id` field (numeric workshop ID) in
  `maps.jsonc`; otherwise `Id` is a stock map name (`de_dust2`).

## Migration notes (from MapChooser)

- Server admins migrating from MapChooser must rename the top-level config key
  `MapChooserMaps` → `MapChangerMaps` and place the plugin folder at
  `addons/swiftly/plugins/MapChanger/`.
- The `Id` in `PluginMetadata` and SwiftlyS2 permission flags
  (`admin.changemap`, `admin.mapsvote`) are unchanged from upstream defaults.
