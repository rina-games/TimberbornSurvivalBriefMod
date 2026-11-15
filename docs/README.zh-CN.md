# Survival Brief — 聚落生存简报 Mod 方案

> 状态：已实现（v0.1）
> 目标游戏版本：Timberborn 1.1.2.8（本机 Steam 版实测）
> 最后更新：2026-11-15
> English: [README.en-US.md](README.en-US.md)

---

## 0. 一句话方案

做一个**纯显示类**小 Mod：在顶部右侧常驻一块小面板，始终显示"距离下一场干旱 / 恶水潮还有几天"，灾害进行中则显示"剩余几天"。不改变任何玩法数值，不写存档，不依赖 Harmony。

---

## 1. 需求与边界

### 做
- 常驻显示：当前周期内，下一场灾害（干旱 or 恶水潮）还有多少天到来。
- 灾害进行中：显示该灾害还剩多少天结束。
- 中文文案（跟随游戏语言，至少 zhCN + enUS）。

### 不做（本期）
- 不预测**下一周期**的灾害类型。原因见 §3.3：灾害类型在周期开始时才随机确定，提前一轮不可知。
- 不做地图编辑器内的显示。
- 不做任何数值/平衡改动，不碰建筑与蓝图。
- 不做存档序列化（纯瞬时显示，无需存盘）。

---

## 2. 已核实的现状（都有实测证据）

| 项目 | 实测值 / 证据 |
|---|---|
| 游戏版本 | `1.1.2.8-ffc2de0-sm`（Info.plist `CFBundleShortVersionString`） |
| Unity / 运行时 | `6000.5.11f1`，Mono；`Data/Managed` 下 658 个程序集可引用 |
| 官方 Mod 支持 | `Player.log` 第 50 行 `Modded: true, official`；已有 5 个 Mod 正常运行 |
| Mod 目录 | `~/Documents/Timberborn/Mods/SurvivalBrief/`（当前只有 `manifest.json` + `thumbnail.png`，`Id: Empty.Timer`） |
| 参考实现 | Workshop 里的 Ladder（建筑 Mod）与 Goods Statistics（HUD Mod）都用了同一套机制 |
| 构建工具链 | **本机没有任何 .NET SDK**（`dotnet` 不存在），需要装 .NET 8 SDK 才能编译 |
| 是否需要 Unity | **不需要**。见 §6.1：用到的类型/成员全部是 `public`，不需要 DllPublicizer，也不需要 Unity 工程 |
| 界面资源 | 原版 UXML/USS 源码就在 `Data/StreamingAssets/Modding/UI.zip`，样式类名可以直接复用 |

---

## 3. 游戏内的天气模型（反编译核实，不是猜的）

### 3.1 数据来源

全部来自 `Timberborn.WeatherSystem` / `Timberborn.HazardousWeatherSystem` / `Timberborn.GameCycleSystem`：

| 成员 | 含义 | 实测语义（IL 级） |
|---|---|---|
| `GameCycleService.Cycle` | 当前周期号 | 1,2,3… |
| `GameCycleService.CycleDay` | 当前周期内第几天（整数） | 1 起 |
| `GameCycleService.PartialCycleDay` | 当前周期内第几天（带小数的日进度） | `0.0 ~ CycleLengthInDays` |
| `WeatherService.TemperateWeatherDuration` | 本周期温和期天数 | 由 `TemperateWeatherDurationService` 决定 |
| `WeatherService.HazardousWeatherDuration` | 本周期灾害期天数 | 由 `HazardousWeatherService` 决定 |
| `WeatherService.CycleLengthInDays` | 本周期总天数 | = 温和期 + 灾害期 |
| `WeatherService.HazardousWeatherStartCycleDay` | 灾害从第几天开始 | **= 温和期天数 + 1** |
| `WeatherService.IsHazardousWeather` | 是否正处灾害期 | **= `CycleDay >= HazardousWeatherStartCycleDay`** |
| `WeatherService.NextDayIsHazardousWeather()` | 明天是否灾害 | `Start <= CycleDay+1 <= CycleLength` |
| `HazardousWeatherService.CurrentCycleHazardousWeather` | 本周期灾害类型 | `IHazardousWeather`，`Id` 为 `DroughtWeather` / `BadtideWeather` |
| `HazardousWeatherApproachingTimer.DaysToHazardousWeather` | 距灾害开始的天数（float） | **= `HazardousWeatherStartCycleDay - PartialCycleDay`** |

关键公式（可直接用，不必自己推）：

```
总天数      = 温和期 + 灾害期
灾害开始日  = 温和期 + 1
距离灾害    = 灾害开始日 − PartialCycleDay        // float，正值表示还没来
灾害剩余    = 总天数 − PartialCycleDay + 1         // 灾害期内
```

### 3.2 一个真实周期长什么样

周期 = 温和期 → 灾害期（灾害固定在周期尾部，不会在周期开头）。所以"距离灾害"在温和期是从大变小，进入灾害后变负值 —— 用 `IsHazardousWeather` 切换两套显示逻辑即可。

### 3.3 为什么不能提前预测"下周期是干旱还是恶水潮"

- 灾害类型由 `HazardousWeatherService.SetForCycle(cycle)` 在**周期开始时**决定，`HazardousWeatherSelectedEvent` 只在选定后发出。
- `BadtideWeather.CanOccurAtCycle(cycle)` / `GetBadtideChance()` 只能告诉"这个周期**可不可以**发生恶水潮、概率多少"，不是确定结果。
- 结论：Mod 只显示"本周期的灾害"，这是数据层面能做到的极限，也是原版的做法。

---

## 4. 原版已经有什么（差异定位）

原版顶部右侧有个 `WeatherPanel`（`UI.zip → Views/Game/WeatherPanel.uxml`）：

```
NineSliceVisualElement  name=WeatherPanel  class="top-right-item weather-panel square-large--green"
├── 进度条 ProgressRoot / Progress  → 灾害临近或进行中的进度
└── Label ForecastCounter           → 数字（天数）
```

行为（`WeatherPanel.SetPanelContent` 的 IL 核实）：

- **温和期，但还没进入"临近窗口"**：数字被隐藏（`ToggleDisplayStyle(daysFloat > 0)`），只有一个天气图标 + 悬浮提示。
- **进入临近窗口**（`ApproachingNotificationDays`）：才显示"还有几天"。该值实测为 **3 天**（`Blueprints.zip → Configurations/HazardousWeatherUI.blueprint.json`：`ApproachingNotificationDays: 3`）。
- **灾害进行中**：显示剩余天数。

**所以原版的缺口很明确：**
1. 温和期前 3 天以外，屏幕上没有天数，要悬停才看得到提示；
2. 灾害期间数字小、含义不直觉（"3" 是剩余还是已过 3 天，得猜）。

Mod 的价值就是把这层信息做成**常驻、带文字、语义明确**的显示。这是本方案唯一的设计依据。

---

## 5. 显示方案对比

| 方案 | 做法 | 优点 | 缺点 | 结论 |
|---|---|---|---|---|
| A. 顶部右侧独立面板 | `UILayout.AddTopRight(element, order)` 挂一块自绘面板，紧挨原版天气图标 | 位置自然、与原版并排、不遮 UI、官方公开方法 | 需要自己搭 VisualElement + 复用 USS 类名保证风格统一 | **推荐** |
| B. 底部栏 | `UILayout.AddBottomRight(...)` | 实现同上 | 底部栏是"操作/工具"区域，放天气信息不符合习惯 | 备选 |
| C. 覆盖原版 WeatherPanel 文本 | Harmony 改 `WeatherPanel.SetPanelContent` | 外观 100% 原生 | 与"Mod Settings / 其它 HUD Mod"冲突风险高；原版改版即失效 | 不推荐 |

**推荐 A**，理由：用的是公开 API（`AddTopRight` 是 `public`），不侵入原版逻辑，和 Goods Statistics 这类成熟 Mod 的做法一致。

显示内容建议（两态）：

```
温和期：  [☀] 干旱 3 天后
灾害期：  [☠] 恶水潮 剩 4 天
```

- 灾害类型在前（干旱 / 恶水潮），天数在后，避免先看到一个孤零零的数字。
- 灾害临近（≤3 天）可用原版同一套闪烁样式类，保持观感一致（可选，P3 再定）。

---

## 6. 技术实现

### 6.1 构建方式（关键结论：不用 Unity）

| 决策 | 选择 | 依据 |
|---|---|---|
| 目标框架 | `netstandard2.1` | Unity 6 Mono 的游戏程序集是 netstandard 2.1 profile，`Managed/netstandard.dll` 存在 |
| 编译工具 | .NET 8 SDK 的 `dotnet build`（需安装，本机暂无） | 只要能产出 dll，不需要 Unity 编辑器 |
| 引用游戏 DLL | `<HintPath>` 指向 `…/Timberborn.app/Contents/Resources/Data/Managed/*.dll`，`<Private>false</Private>` | 避免把游戏 dll 打包进 Mod |
| 是否需要 Publicizer | **不需要** | 实测：`UILayout`、`WeatherService`、`HazardousWeatherService`、`EventBus`、`ILoc`、`NineSliceVisualElement` 等**类型与所需成员全部 public** |
| 是否需要 Harmony | 不需要 | 纯读状态 + 自绘 UI，不 patch 任何原版方法 |

需要的程序集引用：`Timberborn.WeatherSystem`、`Timberborn.HazardousWeatherSystem`、`Timberborn.HazardousWeatherSystemUI`、`Timberborn.GameCycleSystem`、`Timberborn.UILayoutSystem`、`Timberborn.CoreUI`、`Timberborn.SingletonSystem`、`Timberborn.Localization`、`Bindito.Core`、`UnityEngine.CoreModule`、`UnityEngine.UIElementsModule`。

### 6.2 Mod 目录结构（Mods/SurvivalBrief/）

```
Mods/SurvivalBrief/
├── manifest.json              # 已有，需改 Id → Jove.Timer，Version 0.1.0
├── thumbnail.png              # 已有
├── Scripts/
│   └── Jove.Timer.dll         # 构建产物（游戏只认这个目录下的 dll）
├── Localizations/
│   ├── enUS.csv
│   └── zhCN.csv
├── src/                       # 源码与工程（不影响加载）
│   ├── Jove.Timer.csproj
│   ├── TimerConfigurator.cs
│   ├── WeatherCountdown.cs
│   └── WeatherCountdownPanel.cs
└── docs/
    ├── README.md
    ├── README.zh-CN.md        # 本文件
    └── README.en-US.md
```

注意：**不要**在 Mod 根目录创建 `version-x` 子目录，一旦存在，游戏会忽略根目录内容、只加载版本子目录。

### 6.3 代码骨架（三段，全部用公开 API）

```csharp
// 1) 注册：Bindito 会自动发现 Mod 程序集里带 [Context("Game")] 的 Configurator
[Context("Game")]
public class TimerConfigurator : Configurator
{
    protected override void Configure()
    {
        Bind<WeatherCountdown>().AsSingleton();
        Bind<WeatherCountdownPanel>().AsSingleton();
    }
}

// 2) 数据层：只读状态，事件驱动刷新
public class WeatherCountdown : ILoadableSingleton
{
    // 构造注入：GameCycleService、WeatherService、HazardousWeatherService、EventBus
    // [OnEvent] 订阅 CycleDayStartedEvent / HazardousWeatherStartedEvent / HazardousWeatherEndedEvent
    public string Text { get; }      // 已本地化的显示文本
    public bool IsWarning { get; }
    public void Load() { /* 建 UI、首次刷新 */ }
}

// 3) UI 层：自绘面板挂到顶部右侧
public class WeatherCountdownPanel : ILoadableSingleton
{
    public void Load()
    {
        var root = new NineSliceVisualElement();
        root.AddToClassList("top-right-item");
        root.AddToClassList("square-large--green");
        var label = new Label();
        label.AddToClassList("game-text-normal");
        root.Add(label);
        _uiLayout.AddTopRight(root, 0);   // public API
    }
}
```

样式类名全部来自原版 USS（已确认存在）：`.top-right-item`（GameMiscStyle.uss:167）、`.square-large--green`（CommonStyle.uss:123）、`.game-text-normal`、`.text--yellow`。

### 6.4 本地化

Mod 的本地化按 `Localizations/<语言>.csv`（`ID,Text,Comment`）合并进游戏语言表，文件名即语言码。新增键统一加前缀：

```
ID,Text,Comment
Jove.Timer.DroughtIn,Drought in {0} days,-
Jove.Timer.BadtideIn,Badtide in {0} days,-
Jove.Timer.DroughtLeft,Drought ends in {0} days,-
Jove.Timer.BadtideLeft,Badtide ends in {0} days,-
```

zhCN 直接用原版术语（`Weather.Drought=干旱`、`Weather.Badtide=恶水潮`，取自原版 `zhCN.csv`）：

```
Jove.Timer.DroughtIn,{0} 天后干旱,-
Jove.Timer.BadtideIn,{0} 天后恶水潮,-
Jove.Timer.DroughtLeft,干旱剩 {0} 天,-
Jove.Timer.BadtideLeft,恶水潮剩 {0} 天,-
```

灾害名称也可直接复读原版键 `Weather.Drought` / `Weather.Badtide`，不硬编码中文。

---

## 7. 分阶段实施与验收

### P0 探针（先证明数据对，再谈 UI）
- 产出：最小 `Jove.Timer.dll`，不做 UI，每个游戏日往 `Player.log` 打一行：
  `[Timer] cycle=.. day=.. partial=.. start=.. len=.. hazard=.. id=.. daysTo=..`
- 目的：用真实数字校准**取整口径**（向上取整 vs 四舍五入），并确认 `PartialCycleDay` 的小数在暂停/快进下的行为。
- 验收：连续 1 个完整周期（≥12 天）日志行数正确；`daysTo` 与原版 WeatherPanel 显示的数字在最后 3 天一致。

### P1 数据层完备
- 两态文本生成、灾害类型识别、边界（`HazardousWeatherDuration=0`、恶水潮被禁用的存档）。
- 验收：日志正确输出"干旱 3 天后 / 恶水潮 剩 4 天"，负数不串到温和期分支。

### P2 常驻面板（本 Mod 的交付形态）
- `AddTopRight` 挂面板，中文文案，灾害期换配色/图标。
- 验收：1440×900 与 3456×2234 两种分辨率下不遮挡原版天气图标与顶部按钮；截图确认。

### P3 打磨
- 临近 ≤3 天闪烁（复用 `.weather-panel--blink` 思路，样式类按需自建）；enUS 文案；`manifest.json` 文案与缩略图；可选打包进 Steam Workshop。

---

## 8. 验证方法（每阶段自查用）

| 目的 | 操作 / 命令 |
|---|---|
| 装 SDK | `curl -sSL https://dot.net/v1/dotnet-install.sh \| bash -s -- --channel 8.0`，之后 `~/.dotnet/dotnet --version` |
| 编译 | `cd Mods/SurvivalBrief/src && dotnet build -c Release`，产物复制到 `../Scripts/` |
| 看是否加载 | `grep -n "Modded" ~/Library/Logs/Mechanistry/Timberborn/Player.log` → 列表里应出现 `Timer (v0.1.0)` |
| 看探针数据 | `grep "\[Timer\]" ~/Library/Logs/Mechanistry/Timberborn/Player.log` |
| 快速跳天 | 游戏内开发者模式（`Alt + Shift + Z`）可快速推进/跳季 |

---

## 9. 风险与对策

| 风险 | 影响 | 对策 |
|---|---|---|
| 取整口径与原版不一致 | 数字差 1 天，观感差 | P0 用真实日志对比原版数字，确定用 `Ceiling` 还是 `Floor` |
| 每帧刷字符串有 GC 压力 | 大规模聚居地掉帧 | 只在 `CycleDayStartedEvent` / 天气事件 / 数值变化时更新文本 |
| 顶部区域被其它 HUD Mod 挤占 | 面板被遮挡 | 用 `AddTopRight(element, order)` 的 order 参数排到天气图标左侧 |
| 游戏小版本更新改了 API | Mod 失效 | `manifest.json` 的 `MinimumGameVersion` 写 `1.1.0.0`；必要时用 `version-1.1` 子目录分版本 |
| 恶水潮被禁用（老存档 / 难度设置） | 显示"恶水潮"但不会来 | 判 `BadtideWeather.CanOccurAtCycle(cycle)` 与 `GetBadtideChance()`，为 0 时只显示干旱 |

---

## 10. 决策记录（2026-11-15 已定，按推荐采纳）

| 项目 | 决策 |
|---|---|
| 显示位置 | 顶部右侧独立面板，`UILayout.AddTopRight(element, order)` 挂在原版天气图标左侧 |
| 文案形态 | 类别 + 天数：`干旱 3 天后` / `恶水潮 剩 4 天`；术语跟随官方译名（`Weather.Badtide` = 恶水潮） |
| 天数口径 | 向上取整（`CeilToInt`，还有 0.2 天显示 1） |
| 显示范围 | 只显示当前周期的灾害；下一周期类型不可预测（§3.3） |

进度：P0 探针已实测校准；P1 数据层 + P2 顶部面板已上线运行（面板文案形如 `恶水潮 1 天 8 小时后`，只舍不入、精确到游戏小时，最后一小时切分钟）。实测中发现游戏会把同一 Mod 程序集加载成 3 份，静态字段不共享，去重改为检查 `UILayout._topRight` 里是否已存在同名元素。

---

## 11. 资源统计（需求分析，待定口径）

### 11.1 资源全景：60 种物资、5 个统计组

数据来源：游戏自带 `StreamingAssets/Modding/Blueprints.zip → Goods/`（60 个 `GoodSpec`）+ 原版 `zhCN.csv` 译名；分组来自 `GoodGroups/GoodGroup.*.blueprint.json`（`GoodGroupSpec`，排序 10/20/30/50/60）。

| 组（Order） | 数量 | 单资源组 | 内容 |
|---|---|---|---|
| 材料 Materials (10) | 24 | 否 | 原木、木板、齿轮、纸、书籍、松脂、树脂木板、蒲公英、解毒剂、金属废料、金属部件、金属块、炸药、烟花、提取物、机关人主体/肢体/头部、生物燃料、穿孔卡、催化剂、润滑油、咖啡、泥土 |
| 原料 Ingredients (20) | 17 | 否 | 土豆、栗子、青荷根、小麦、小麦面粉、猫尾草根、猫尾草粉、枫糖浆、木薯、大豆、油菜籽、菜籽油、玉米、茄子、蘑菇、水藻、咖啡豆 |
| 食物 Food (30) | 17 | 否 | 浆果、胡萝卜、葵花籽、烤土豆、烤栗子、烤青荷根、面包、猫尾草饼干、枫糖糕饼、大头菜、红树果实、发酵木薯、发酵大豆、发酵蘑菇、玉米口粮、茄子口粮、水藻口粮 |
| 水 Water (50) | 1 | 是 | 水 |
| 恶水 Badwater (60) | 1 | 是 | 恶水（废物，正常情况下不需要统计） |

### 11.2 能读到哪些统计口径（均已核实为 public，可直接调用）

| 数据 | API | 说明 |
|---|---|---|
| 分组/单物资库存 | `ContextualResourceCountingService.GetContextualResourceCount(goodId)` | 原版顶部计数器用的就是这个，区分当前选区 |
| 全局 / 分区域 | `ResourceCountingService.GetGlobalResourceCount` / `GetDistrictResourceCount` | 区域维度 |
| 容量 | `ResourceCount.TotalCapacity`、`InputOutputCapacity` | 原版界面不直接显示 |
| 填充率 | `ResourceCount.FillRate` | 0~1 |
| 在途物资 | `CarriedToStockpilesStock`、`CarriedToProcessors` | 搬运中的量 |
| 加工中 | `StockUnderProcessing`、`BufferedInput`、`BufferedOutputStock` | 生产链中间态 |
| 产量 / 消耗 | `DistrictGoodsBalance.GetProduction(id)` / `GetConsumption(id)` | 当日累计，可用于净变化 |

### 11.3 与原版 / 已装 Mod 的重叠（必须避免重复造轮子）

- **原版顶部栏本来就有 5 组计数器**：`TopBarPanel` 遍历 `GoodGroupSpecs` 建计数器（水/恶水单值，材料/原料/食物可展开多行），悬停有仓库明细提示。你截图里没看到，应该是计数器栏被收起或不在可视位置。
- **你已装的 `Goods Statistics`** 已经在顶部栏加趋势箭头、耗尽天数，还有批次统计面板。
- 结论：只做"库存数量"，就是重复。差异化只有三条：**容量/填充率**、**产量−消耗净变化**、**常驻明细列表**。

### 11.4 四个候选方案

| 方案 | 内容 | 评价 |
|---|---|---|
| R1 关键资源（推荐先做） | 固定 6 项：水、食物、原木、木板、齿轮、金属块，显示 `数量/容量` | 生存 + 扩张瓶颈全覆盖，容量是原版没有的信息，一屏放得下 |
| R2 五组全量 | 5 组各一行 `数量/容量`，与原版计数器同构但补容量 | 与原版重复度高，顶部更挤 |
| R3 净变化 | 每组 `今日产量 − 消耗` | 与 Goods Statistics 直接冲突 |
| R4 可展开明细 | 点开某组看 17~24 种明细 | 交互成本高，留到 V2 |

### 11.5 待定（见 §12）

统计范围、显示口径、面板位置。

---

## 12. 资源统计（已实现 v0.1，口径待你确认）

### 12.1 澄清：这里的"资源"指**未采集的自然资源**，不是仓库存货

游戏里两类东西容易混：

| 类别 | 数量 | 数据来源 | 是否本功能范围 |
|---|---|---|---|
| 物资（仓库存货） | 60 种 / 5 组 | `GoodSpec` + `ResourceCountingService` | ❌ 原版顶部计数器已有 |
| **未采集的自然资源** | **22 种**（7 树 + 3 灌木 + 12 作物） | `NaturalResource` 标记的实体 + `Cuttable/Gatherable` 产出 | ✅ 本功能 |

22 种自然资源与产出（实测自 `Blueprints.zip → NaturalResources/`）：

| 类别 | 种类（产出） |
|---|---|
| 树 7 种 | 桦树（原木×1）、松树（原木×2、松脂×2）、橡树（原木×8）、枫树（原木×6、枫糖浆×3）、栗子树（原木×4、栗子×3）、红树（原木×2、红树果实×4）、多肉植物（水×2） |
| 灌木 3 种 | 蓝莓（浆果×3）、咖啡树（咖啡豆×1）、蒲公英（蒲公英×1） |
| 作物 12 种 | 胡萝卜×3、土豆×1、小麦×3、大头菜×2、玉米×2、茄子×3、木薯×1、大豆×2、油菜籽×3、向日葵×2、猫尾草根×3、青荷根×3 |

### 12.2 已实现的统计口径

- **数量**：地图上该种类的存活实体数（`LivingNaturalResource.IsDead == false` 才计数）。
- **成熟度**：`Growable.IsGrown` 判定，悬停显示"成熟 N / 总数 N"。
- **可产出总量**：按每棵/每株 `YielderSpec.Yield.Amount` × 数量累加，例如"原木 ×336"。
- 实现：`EntityRegistry.Entities` 全量扫描 → 按 `LabeledEntity.DisplayName` 分组；每 3 秒重算一次，内容指纹不变则不重建 UI。
- 位置：`UILayout.AddBottomRight`，两列紧凑列表，分组头（树 / 灌木 / 作物），只列出数量 >0 的种类。

### 12.3 「当前 / 初始」已加上（2026-11-15 确认）

自然资源没有仓库容量，所谓"数量/容量"落地为 **当前数量 / 载入时数量**：

- 快照时机：第一次扫到非空数据时记录基线（即读档/开局时的原始数量）；只在本局有效，不写存档。
- 行显示：`橡树 42/60`（初始为 0 的新增种类只显示 `12`）。
- 表头：`共 126 / 168 处`。
- 悬停三行：`橡树：42 处（成熟 30）` / `载入时 60 处（已减少 18）` / `可产出：原木 ×336`。
- 说明：这是"本局载入后已消耗多少"，不是地图生成时的原始量——存档里没有保存该信息，无法回溯。

---

## 13. 消耗分析（2026-11-15 换向：取代原来的"未采集资源"面板）

### 13.1 需求来源：不是估的，是读需求规格

| 数据 | 来源 | 实测值 |
|---|---|---|
| 饥饿衰减 | `NeedSpec(Id=Hunger).DailyDelta` | **−0.8 点/天**（每只海狸） |
| 口渴衰减 | `NeedSpec(Id=Thirst).DailyDelta` | **−0.7 点/天**（每只海狸） |
| 每份食物的饱腹值 | `GoodSpec.ConsumptionEffects[NeedId=Hunger].Points` | **全部食物都是 0.30** |
| 每份水的解渴值 | `GoodSpec(Water).ConsumptionEffects[NeedId=Thirst].Points` | **0.33** |

由此得出（这是本功能的核心数字）：

- **每只海狸每天吃 0.8 ÷ 0.3 ≈ 2.67 份食物，即每 9 小时吃一份。**
- **每只海狸每天喝 0.7 ÷ 0.33 ≈ 2.12 份水，即每 11.3 小时喝一份。**
- 17 种食物的饱腹值完全相同（0.30），只有"饮食多样性"需求不同（多数 +0.20，葵花籽 +0.50）——所以**换食物种类不省粮，只是满足多样性**。

### 13.2 建筑消耗

- 扫描所有 `Manufactory` 且 `Workshop.CurrentlyWorking == true` 的建筑，读 `Manufactory.CurrentRecipe`。
- 每天消耗 = `配方原料数量 × 24 ÷ 配方周期小时数`。
- 按「建筑类型 × 物资」汇总，例如「面包房×2：小麦 12/天」。

### 13.3 面板内容（底部右侧）

```
消耗分析
海狸 17 只（成年 12 / 幼年 5）
每只每天：食物 0.8 点 · 水 0.7 点
食物
 [图标] 浆果   2.7/只·天 · 45/天 · 可撑 20 天
水
 [图标] 水     2.1/只·天 · 36/天 · 可撑 12 天
建筑消耗
 面包房×2 小麦 12/天
```

- 只列库存 >0 的食物种类（最多 8 行），按单位消耗排序；"可撑天数" = 库存 ÷ 全群每天消耗。
- 悬停：`浆果：每份 0.3 点 / 每 9 小时消耗一份，库存 900`。
- 幼年海狸同样计入消耗（游戏里饥饿/口渴需求对全部海狸生效）。
- 刷新：人口变化事件 + 每 2 秒兜底；内容指纹不变不重建 UI。

### 13.4 已知边界

- 不统计"建造消耗"（原木/木板等建材），那是材料不是食物；需要的话可以另开一档。
- 建筑消耗按"正在开工"计算，停工/缺料时会归零，属于真实状态而不是理论产能。
- 机器人（Bots）不吃饭，未计入 Eaters。

### 13.5 建造需求与燃料（2026-11-15 追加）

- **建造需求**：扫描所有 `ConstructionSite`，读它 `Inventory.AllowedGoods`（每种建材的需求上限）与 `Inventory.Stock`（已送达量），差额即"还缺多少"，按物资汇总并统计在建处数。显示为 `原木 120 · 在建 3 处`。
- **燃料消耗**：`RecipeSpec.Fuel` 实测是**物资 Id 字符串**（不是数量），每 `CyclesFuelLasts` 个生产周期烧 1 份 → 每天消耗 = `24 ÷ (周期小时 × CyclesFuelLasts)`，并入该建筑的"建筑消耗"行。
- 面板最终分区顺序：人口 → 每只每天需求 → 食物 → 水 → 建造需求 → 建筑消耗。

---

## 14. 踩坑：本地化 CSV 的多余逗号会让游戏启动直接崩

**现象**：进游戏即弹错误报告，`1 Exception` 内容是
`InvalidDataException: Localization file contains errors: Unnecessary comma found in enUS (line 13/18/22)`，
调用栈停在 `LocalizationCsvValidator.Validate` → `LocalizationService.Load` → `SingletonLifecycleService.LoadAll`，
游戏在单例加载阶段就中断，等于整个 Mod 不可用。

**根因**：`Localizations/<语言>.csv` 是严格的 3 列格式 `ID,Text,Comment`。enUS 文案里写了英文逗号
（`Per beaver per day: food {0} pts, water {1} pts`），CSV 解析出第 4 列，校验器直接抛异常。
游戏自带的校验器是"零容忍"，不会跳过这一行。

**解法**：文案内的逗号改成 `·`，或者整段用双引号包起来（`ID,"text, with comma",-`）。

**防回归**：`tools/check-localization.py`，改完文案先跑：

```
python3 tools/check-localization.py    # 期望输出：enUS.csv OK / zhCN.csv OK
```

中文文案用的是全角 `，`，不触发这个问题，所以只有英文/数字标点要小心。

---

## 15. 踩坑：蓝图里有、存档里没有的物资会让刷新整个失败

**现象**：面板只显示「还没有海狸」，其余区块全部空白；`Player.log` 里每 2 秒刷一条
`[Timer] consumption refresh failed: System.ArgumentException: GoodSpec with id AlgaeRation not found!`

**根因**：`ISpecService.GetSpecs<GoodSpec>()` 返回的是**全部蓝图**，包含本局不可用的物品
（`Good.AlgaeRation` 属于 `GoodCollection.IronTeeth`，Folktails 存档里根本不存在这个物资）。
`GoodDescriber.Describe(id)` 走的是 `IGoodService`，对未注册的物资直接抛 `ArgumentException`，
异常把整次 `Refresh()` 打断在第 3 步，人口字段还没来得及写回 → 面板显示「还没有海狸」。

**解法**（两点缺一不可）：

1. 遍历食物时先用 `IGoodService.HasGood(id)` 过滤，只保留当前存档真实存在的物资；
2. 所有 `GoodDescriber.Describe/GetIcon` 调用包一层 try/catch，失败时回退成物资 Id 并只记一次警告。

**教训**：Mod 里读蓝图规格要和"本局实际载入的物资表"对齐；任何一次异常都不该让整块面板的数据归零。

---

## 16. 面板简化为「折叠一行 + 点击弹层」（2026-11-15）

**改动理由**：完整版占掉底部右侧一大块，把原版面板压住，而大多数时候玩家只需要"我还撑几天"。

**结构**：

| 层 | 挂载点 | 内容 |
|---|---|---|
| 折叠条 | `UILayout.AddBottomRight(..., 1)` | `消耗分析 海狸 129 · 食物 52.3 天 · 水 24.3 天`，一行 |
| 弹层 | `UILayout.AddAbsoluteItem(...)`，`position: absolute; right: 12; bottom: 64` | 人口 / 每只每天需求 / 食物（合计 + 逐种）/ 水 / 建造需求 / 建筑消耗 |

- 点折叠条切换弹层；弹层右上角「收起」关闭。
- 弹层挂在绝对层（`_absoluteItems`）而不是底部栏，避免被底部栏布局裁剪。
- 弹层可见且数据指纹变化时才重建；折叠条每次数据变化都刷新。
- 两个元素都按名字去重（`TimerConsumptionCompact` / `TimerConsumptionDetail`），应对游戏把程序集加载成 3 份。

---

## 17. 详情改用原版弹窗框架（2026-11-15）

**结论**：可以，而且不用自己画羊皮纸边框——那套外观是游戏的 `Common/NamedBoxTemplate`
（`content-row-centered` + `sliced-border box__content-container` + `capsule-header` 标题胶囊 + `close-button`），
居民康乐度、设置框等都用它。

**接入方式**（和原版面板同一条管线，居中/ESC 关闭/阻止底层点击都是免费的）：

1. `ConsumptionDetailPanel : IPanelController`（`GetPanel()` / `OnUIConfirmed()` / `OnUICancelled()`）
2. `VisualElementLoader.LoadVisualElement("Common/NamedBoxTemplate")` 拿到框架
3. `Q<Label>("Header").text = 标题`，`Q<Button>("CloseButton").clicked += Close`
4. `Q<VisualElement>(null, "box__content-container")` 里塞一个 `game-scroll-view` 滚动容器，再填内容
5. 打开：`PanelStack.Push(panel)`；关闭：`PanelStack.Pop(this)`（ESC 走 `OnUICancelled`）

**没复用 `PopulationWellbeingBox` 本身**：那个 UXML 的内容是写死的（康乐度行、解锁部族），塞不进我们的数据；
只复用它的外壳模板。`DialogBox` 那套（`DialogBoxShower`）只支持纯文字 + 三个按钮，也不适合。

### 17.1 两处实现修正（2026-11-15）

- **不能直接加载 `Common/NamedBoxTemplate`**：模板里的 Header 是 `LocalizableLabel`（`ILocalizableElement`），
  脱离 UXML 单独 `LoadVisualElement` 时没有 `text-loc-key`，初始化器直接抛
  `InvalidOperationException: text-loc-key is not set for ILocalizableElement`。
  改为按同一份 UXML 的类名手工搭：`content-row-centered` → `sliced-border box__content-container` → `capsule-header` + Label + `close-button`。
- **建筑名字不能只认 `LabeledEntity`**：建筑实体上不一定有该组件，导致 `manufactories=0`。
  改为 `LabeledEntity.DisplayName → NamedEntity.EntityName → 配方 DisplayLocKey → 配方 Id` 四级回退。
- **建造需求要过滤空库存**：存档里 938 个实体都带 `Inventory:ConstructionSite`（已完工建筑的占位组件），
  必须同时满足 `Inventory != null && Capacity > 0 && 差额 > 0` 才算在建。

---

## 18. 界面重排：弹窗居中 + 表格化，底部只留一行（2026-11-15）

**底部折叠条**（简化）：去掉标题，只留一行 `129 只 · 食 52.3 天 · 水 24.3 天`，悬停提示"点击展开消耗详情"。

**弹窗**：
- 居中：面板根节点 `flexGrow:1 + alignItems/justifyContent: center`，窗口在面板层正中；
- 响应式：宽度 `62%`，`minWidth 460 / maxWidth 780`；内容滚动区高度取 `Screen.height × 0.55`（限制 320~900px）；
- 结构：
  - 人口块：大号数字 + `只海狸（成年 x · 幼年 y）`，下面灰色一行"每只每天：食物 0.8 点 · 水 0.7 点"；
  - 食物/水：分区标题 + 灰色表头（名称 / 库存 / 每天 / 可撑）+ 数据行（图标 + 名称 + 右对齐数值）；
  - 可撑天数用条形表示：30 天满格，`<2 天红 / <6 天黄 / 其余绿`，条宽按比例；
  - 建造需求：`物资名 · 在建 N 处 · 还缺 M`；
  - 建筑消耗：`建筑名 ×N（开工 M） · 原料 日均/天`。

---

## 19. 弹窗改成"康乐度"式行布局 + 修满屏高度（2026-11-15）

**高度失控的根因**：`CoreStyle.uss` 里 `.box__content-container { align-self: stretch; flex-grow: 1; }`，
放进了我那个 `flexGrow:1` 的全屏居中容器后，边框被拉到整屏高度，内容只占顶部一小块。
修法：边框上覆盖 `flex-grow: 0; align-self: center;`，让窗口按内容自适应高度；只有内容超出时才由 ScrollView 限高滚动。

**行样式（对齐"居民康乐度"）**：

```
基本需要式分区标题
 名称列(132px)   整行长条（条内居中显示：库存 16820 · 每天 363）   胶囊(74px)
 浆果           ███████████████████░░░░░░░            46.4 天
 面包           ███████████████████████████          60.7 天
```

- 去掉表头，信息压进条内；右侧胶囊显示"可撑 X 天"，底色随天数变化（>6 绿 / 2~6 黄 / <2 红）。
- 条满格 = 20 天，便于前期判断；行高 22px，与康乐度接近。
- 建造需求：`物资名 … 在建 3 处 … [还缺 12]`；
- 建筑消耗：`建筑名 … ×4（开工 4） … [192/天]`。

### 19.1 间距与滚动条（2026-11-15）

- 行高/间距：条高 `30px`（原 22），行间 `6px`，分区标题上 `14px` 下 `6px`，与康乐度的节奏一致。
- 滚动条：`new ScrollView(ScrollViewMode.Vertical)` + `verticalScrollerVisibility = Auto` + `game-scroll-view` 类，
  拖动条样式来自 `GameStyle.uss` 里 `.game-scroll-view > ... > .unity-slider .unity-base-slider__dragger`（bg-pixel-2）。
- 窗口高度：边框 `maxHeight: 82%`，并在首次 `GeometryChangedEvent` 里用 `resolvedStyle.height × 0.72` 兜底限高
  （UI Toolkit 的百分比高度依赖父级确定高度，且 `Screen.height` 是物理像素、与 UI 坐标有缩放差，单用任一都会失准）。

### 19.2 按「居民康乐度」逐项对齐（2026-11-15）

康乐度那一行在 `PopulationStyle.uss` 里是这套尺寸（`.population-wellbeing-counter*`）：

| 元素 | 原版值 |
|---|---|
| 行 | `height: 20px; margin: 2px 0; flex-direction: row` |
| 名称 | `width: 40%; padding-left: 12px; 左对齐` |
| 条外框 | `height: 100%; width: 80%; margin-right: 2px`，底图 `bg-pixel-2` |
| 条填充 | `height: 100%`，底图 `needview-background`（绿）/`-red` |
| 条内数字 | `position: absolute; width: 100%; -unity-text-align: middle-center` |
| 右侧数值块 | `width: 50px; margin: 0 6px`，NineSlice 底 + `--background-slice: 10` |

这些类写在 `PopulationStyle.uss`，只对康乐度那个 UXML 生效，HUD 文档里没有加载，所以**不能直接套类名**，
只能按其数值手工复刻，已落地为：行高 22px / 上下各 1px / 名称 40% / 条与行同高 / 条内文字绝对居中 /
胶囊 56px 深底 + 1px 彩色描边 + 彩色文字（原版是九宫格底图，我们用同色系描边近似）。

溢出修复：
- 内容放进普通列容器（`flexGrow:0`），避免被 ScrollView 的内容区拉伸导致行距异常；
- 窗口高度在首次 `GeometryChangedEvent` 里按 `resolvedStyle.height` 真实像素计算：滚动区 64%、窗口 84%，再配合 `ScrollerVisibility.Auto` 出现原版滚动条。

### 19.3 直接克隆康乐度 UXML（2026-11-15）

`ConsumptionDetailPanel` 改为加载 `Game/Population/PopulationWellbeingBox`，行用 `PopulationWellbeingCounter`，分组用 `PopulationWellbeingCounterGroup`。样式表从克隆产生的 `TemplateContainer` 拷到根节点上，否则进度条纹理与 `game-scroll-view` 滚动条样式会丢失。内容超出约 420px 时锁定滚动区高度并显示纵向滚动条；内容少时不占位滚动条。
