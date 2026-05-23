# Copilot Instructions — MapChanger (SwiftlyS2 plugin)

This workspace is a SwiftlyS2 C#/.NET 10 plugin. Use the **SwiftlyS2-Toolkit**
agents, prompts, and skills already installed under `.github/` for all
planning, editing, and review workflows.

## Workspace facts

- Plugin name: `MapChanger`
- Root namespace: `MapChanger`
- Main plugin class: `MapChanger` in [src/MapChanger.cs](src/MapChanger.cs)
- Project file: [MapChanger.csproj](MapChanger.csproj) — `net10.0`, NuGet `SwiftlyS2.CS2`
- Source layout: `src/Commands/`, `src/Helpers/`, `src/Menu/`, `src/Models/`, `src/Dependencies/`
- Runtime resources: [resources/templates](resources/templates) (config.jsonc, maps.jsonc), [resources/translations](resources/translations), [resources/gamedata](resources/gamedata)
- License: [LICENSE.MIT](LICENSE.MIT) (derived from upstream MapChooser; original copyright preserved)
- Toolkit skill entry: [.github/skills/SwiftlyS2-Toolkit/SKILL.md](.github/skills/SwiftlyS2-Toolkit/SKILL.md)
- Knowledge base: [.github/knowledge-base.md](.github/knowledge-base.md)

## Rules

1. Always consult the `SwiftlyS2-Toolkit` skill before authoring or modifying
   plugin code (commands, events, hooks, menus, schedulers, DI, etc.).
2. Prefer the templates under
   `.github/skills/SwiftlyS2-Toolkit/assets/` over inventing patterns.
3. Keep new code in the existing folder layout: commands in `src/Commands/`,
   long-lived managers in `src/Helpers/`, menus in `src/Menu/`, POCOs in
   `src/Models/`, shared plugin state in `src/Dependencies/`.
4. Do not put business logic directly in command/event handlers — push it into
   the manager classes (`VoteManager`, `ChangeMapManager`, `MapCycleManager`,
   `EndOfMapVoteManager`, `ExtendManager`, `MapCooldown`, `MapLister`).
5. Preserve upstream MIT attribution when redistributing.
6. Public reference sources: SwiftlyS2 docs (https://swiftlys2.net/docs/),
   sw2-mdwiki (https://github.com/himenekocn/sw2-mdwiki), upstream SwiftlyS2
   repo (https://github.com/swiftly-solution/swiftlys2), upstream MapChooser
   (https://github.com/SwiftlyS2-Plugins/MapChooser).

## Build

```powershell
dotnet build
```

The compiled `MapChanger.dll` is written to `build/MapChanger.dll`. Deploy it
along with the `resources/` folder to
`<cs2-server>/csgo/addons/swiftly/plugins/MapChanger/`.
