# Survival Brief

Timberborn 生存简报 Mod：顶栏显示灾害倒计时与食物/水可撑天数，点击展开消耗与建筑详情（对齐原版康乐度面板）。

联系：[jove.rina@outlook.com](mailto:jove.rina@outlook.com)

| | |
|---|---|
| Mod Id | `Jove.SurvivalBrief` |
| 最低游戏版本 | 1.1.0.0 |

## 开发与工作区

| 路径 | 用途 |
|------|------|
| 本仓库（如 `~/Workbench/Games/TimberbornSurvivalBriefMod`） | 源码、文档、工具 |
| `~/Documents/Timberborn/Mods/SurvivalBrief` | 游戏实际加载的 Mod（仅运行时文件） |

## 安装（游玩）

1. 确保 `~/Documents/Timberborn/Mods/SurvivalBrief/` 内有 `manifest.json`、`Scripts/Jove.SurvivalBrief.dll`、`Localizations/`、`thumbnail.png`（在本仓库执行 Release 构建会自动同步到该目录）。
2. 在游戏 Mod 管理器中启用 **Survival Brief**。

从他人发布的压缩包安装时，将文件夹内容放进 `~/Documents/Timberborn/Mods/SurvivalBrief/` 即可，勿把源码目录整包拷进 Mods。

## 从源码构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)。

```bash
cd src
dotnet build -c Release
```

构建会把 `Jove.SurvivalBrief.dll` 以及 `manifest.json`、`Localizations/`、`thumbnail.png` 部署到 `~/Documents/Timberborn/Mods/SurvivalBrief/`，并更新本仓库的 `Scripts/`（便于提交发布用 dll）。自定义 Mod 目录可加：`-p:ModInstallDir=/path/to/mod`。

修改文案后运行：

```bash
python3 tools/check-localization.py
```

## 文档

- [中文设计说明](docs/README.zh-CN.md)
- [English design notes](docs/README.en-US.md)

## 许可证

[MIT](LICENSE)