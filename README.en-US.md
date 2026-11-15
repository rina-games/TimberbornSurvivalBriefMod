# Survival Brief

A Timberborn mod: top-bar hazard countdown plus food and water runway; click to open consumption and building details (aligned with the stock wellbeing panel).

Contact: [jove.rina@outlook.com](mailto:jove.rina@outlook.com)

| | |
|---|---|
| Mod Id | `Jove.SurvivalBrief` |
| Minimum game version | 1.1.0.0 |

## Development layout

| Path | Role |
|------|------|
| This repo (e.g. `~/Workbench/Games/TimberbornSurvivalBriefMod`) | Source, docs, tools |
| `~/Documents/Timberborn/Mods/SurvivalBrief` | Mod folder the game loads (runtime files only) |

## Install (play)

1. Ensure `~/Documents/Timberborn/Mods/SurvivalBrief/` contains `manifest.json`, `Scripts/Jove.SurvivalBrief.dll`, `Localizations/`, and `thumbnail.png` (a Release build from this repo syncs them automatically).
2. Enable **Survival Brief** in the in-game mod manager.

When installing from a release archive, copy its contents into `~/Documents/Timberborn/Mods/SurvivalBrief/`; do not drop the whole dev repo into Mods.

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cd src
dotnet build -c Release
```

The build deploys the DLL plus `manifest.json`, `Localizations/`, and `thumbnail.png` to `~/Documents/Timberborn/Mods/SurvivalBrief/`, and updates this repo’s `Scripts/` for releases. Override the install path with `-p:ModInstallDir=/path/to/mod`.

After editing localized strings:

```bash
python3 tools/check-localization.py
```

## Docs

- [中文设计说明](docs/README.zh-CN.md)
- [English design notes](docs/README.en-US.md)

## License

[MIT](LICENSE)