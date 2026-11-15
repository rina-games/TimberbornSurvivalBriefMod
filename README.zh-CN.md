# Survival Brief

Timberborn 生存简报 Mod：顶栏显示灾害倒计时与食物/水可撑天数，点击展开消耗与建筑详情（对齐原版康乐度面板）。

| | |
|---|---|
| Mod Id | `Jove.SurvivalBrief` |
| 最低游戏版本 | 1.1.0.0 |

## 安装

1. 将本仓库整个文件夹放到 `~/Documents/Timberborn/Mods/SurvivalBrief/`（需包含 `manifest.json`、`Scripts/Jove.SurvivalBrief.dll`、`Localizations/`、`thumbnail.png`）。
2. 在游戏 Mod 管理器中启用 **Survival Brief**。

## 从源码构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)。

```bash
cd src
dotnet build -c Release
```

构建完成后会自动复制 `Jove.SurvivalBrief.dll` 到 `Scripts/`。若未复制，请从 `~/Documents/Timberborn/.build/SurvivalBrief/bin/Release/` 手动拷贝。

修改文案后运行：

```bash
python3 tools/check-localization.py
```

## 文档

- [中文设计说明](docs/README.zh-CN.md)
- [English design notes](docs/README.en-US.md)

## 许可证

[MIT](LICENSE)