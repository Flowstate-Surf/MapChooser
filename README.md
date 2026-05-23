# Admin

A [SwiftlyS2](https://swiftlys2.net/) plugin for Counter-Strike 2.

## Requirements

- .NET 10 SDK
- A SwiftlyS2-enabled CS2 server (for deployment)

## Build

```powershell
dotnet build
```

The output `bin/Debug/Admin.dll` is the loadable plugin assembly.

## Deploy

Copy the build output to your server:

```
<cs2-server>/csgo/addons/swiftly/plugins/Admin/Admin.dll
```

## Development

This workspace uses the [SwiftlyS2-Toolkit](https://github.com/2oaJ/SwiftlyS2-Toolkit)
Copilot agents, prompts, and skills, installed under `.github/`. See
[.github/copilot-instructions.md](.github/copilot-instructions.md).
