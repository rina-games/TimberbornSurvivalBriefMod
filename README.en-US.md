# Survival Brief

A Timberborn mod: top-bar hazard countdown plus food and water runway; click to open consumption and building details (aligned with the stock wellbeing panel).

| | |
|---|---|
| Mod Id | `Jove.SurvivalBrief` |
| Minimum game version | 1.1.0.0 |

## Install

1. Place this repository folder at `~/Documents/Timberborn/Mods/SurvivalBrief/` (include `manifest.json`, `Scripts/Jove.SurvivalBrief.dll`, `Localizations/`, and `thumbnail.png`).
2. Enable **Survival Brief** in the in-game mod manager.

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cd src
dotnet build -c Release
```

The build copies `Jove.SurvivalBrief.dll` into `Scripts/`. If it does not, copy manually from `~/Documents/Timberborn/.build/SurvivalBrief/bin/Release/`.

After editing localized strings:

```bash
python3 tools/check-localization.py
```

## Docs

- [中文设计说明](docs/README.zh-CN.md)
- [English design notes](docs/README.en-US.md)

## License

[MIT](LICENSE)