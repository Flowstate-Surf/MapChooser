# MapChanger

A [SwiftlyS2](https://swiftlys2.net/) map-voting plugin for Counter-Strike 2.

Provides Rock The Vote (RTV), end-of-map voting, map extensions, a sequential/random map cycle, and a **multi-tier / multi-type nomination menu** with player-count filtering — purpose-built for surf and bhop server rotations.

---

## Features

### 🗂️ Multi-Tier / Multi-Type Nomination Menu

The nomination flow (`!nom`) opens a **two-level menu**:

1. **Tier picker** — Shows every tier that has eligible maps (e.g. `T1 (12)`, `T2 (8)`, `T3 (5)`, `All Maps`). Maps already in cooldown or outside current player-count limits are excluded from counts automatically.
2. **Map list** — Displays only maps from the selected tier. Player taps a map to nominate it.

**Configuration** (in `config.jsonc`):

| Key | Values | Effect |
|-----|--------|--------|
| `Nomination.MapType` | `"surf"`, `"bhop"`, or `""` | Restricts the pool to maps whose name/id starts with the given prefix. Leave blank for all map types. |
| `Nomination.IncludeTiers` | `"1,2,3"` or `""` | Shows only the listed tiers in the picker. Leave blank to show all tiers. |

Each map in `maps.jsonc` carries a `Tier` field (integer) and an optional `Id` (workshop ID / `ws:<id>` prefix). Setting `MinPlayers` / `MaxPlayers` per map hides ineligible maps from both the tier counts and the map list.

---

### 🗳️ Rock The Vote (RTV)

- Players call `!rtv` to register a vote; `!unrtv` to withdraw.
- Triggers when the configured percentage of active players have voted (default **60 %**).
- Respects `MinPlayers`, `MinRounds`, and a per-failure cooldown (default 300 s).
- On success: immediate map change, bypassing the end-of-map vote.

### 🏁 End-of-Map Vote

- Triggered automatically when time remaining ≤ `TriggerSecondsBeforeEnd` (default 120 s) **or** rounds remaining ≤ `TriggerRoundsBeforeEnd` (default 4).
- Nominations are prioritised; remaining slots filled with random eligible maps.
- Configurable options count (`MapsToShow`, default 6) and vote timer (`VoteDuration`, default 30 s).
- Live vote counts shown per map during voting.

### ⏳ Map Extend

- Players vote with `!ext` / `!ve` / `!voteextend`.
- Adds `ExtendTimeStep` minutes (default 15) **or** `ExtendRoundStep` rounds per extend.
- Hard cap via `ExtendLimit` (default 3 extends per map).
- "Extend" option surfaced inside end-of-map vote when `AllowExtend` is enabled.

### 🔄 Map Cycle

- Sequential or random rotation (`Cycle.RandomOrder`).
- Admin commands: `!addmap`, `!removemap`, `!cyclemenu` (reorder / preview next map).

### ❄️ Map Cooldown

- Tracks the last *N* maps (`MapsInCooldown`, default 3).
- Cooldown maps are hidden from nominations and vote pools automatically.

### 🛠️ Admin Commands

| Command | Permission | Description |
|---------|-----------|-------------|
| `!changemap <map>` | `admin.changemap` | Force immediate map change |
| `!setnextmap <map>` | `admin.changemap` | Pre-set next map |
| `!mapsvote <m1> <m2> …` | `admin.mapsvote` | Start a custom vote on listed maps |
| `!addmap <name> [id]` | `admin.changemap` | Add map to rotation |
| `!removemap <name>` | `admin.changemap` | Remove map from rotation |
| `!cyclemenu` | `admin.changemap` | Interactive cycle management menu |

### 📢 Player Commands

| Command | Description |
|---------|-------------|
| `!rtv` / `!unrtv` | Vote / unvote to change map |
| `!nom` / `!nominate` | Open tier picker → nominate a map |
| `!timeleft` | Show time remaining on current map |
| `!nextmap` | Show scheduled next map |
| `!votemap` | Trigger an immediate map vote |
| `!ext` / `!ve` | Vote to extend current map |
| `!maplist` | List all maps with cooldown status |

---

## Map Pool (`maps.jsonc`)

Each entry:

```jsonc
{
  "Name": "surf_utopia_njv",
  "Id": "ws:12345678",   // workshop ID, "ws:<id>", or blank for vanilla
  "Tier": 2,
  "MinPlayers": 0,
  "MaxPlayers": 0
}
```

The bundled pool ships with **surf T1–T7** and **bhop T1–T10** maps (~886 entries).

---

## Requirements

- .NET 10 SDK
- SwiftlyS2-enabled CS2 server

## Build

```powershell
dotnet build
```

Output: `build/MapChanger.dll`

## Deploy

Copy to your server:

```
<cs2-server>/csgo/addons/swiftly/plugins/MapChanger/
  MapChanger.dll
  resources/
```

First-run generates `config.jsonc` and `maps.jsonc` under `resources/` if they do not exist.

## Development

This workspace uses the [SwiftlyS2-Toolkit](https://github.com/2oaJ/SwiftlyS2-Toolkit)
Copilot agents, prompts, and skills installed under `.github/`. See
[.github/copilot-instructions.md](.github/copilot-instructions.md).

## License

MIT — see [LICENSE.MIT](LICENSE.MIT). Derived from upstream [MapChooser](https://github.com/SwiftlyS2-Plugins/MapChooser); original copyright preserved.
