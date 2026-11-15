# Survival Brief — settlement survival HUD mod

> Status: implemented (v0.1)  
> Target game version: Timberborn 1.1.2.8 (verified on local Steam build)  
> Last updated: 2026-11-15  
> Chinese documentation: [README.zh-CN.md](README.zh-CN.md)

---

## 0. One-liner

A **display-only** small mod: a compact panel stays on the top-right and always shows how many days until the next drought or badtide; while a hazard is active it shows how many days remain. It does not change gameplay values, does not write save data, and does not depend on Harmony.

---

## 1. Requirements and scope

### In scope
- Persistent display: during the current cycle, how many days until the next hazard (drought or badtide).
- While a hazard is active: how many days until it ends.
- Localized copy (follows game language; at least zhCN + enUS).

### Out of scope (this release)
- Do not predict hazard **type in the next cycle**. See §3.3: hazard type is rolled only when the cycle starts, so it is unknowable one cycle ahead.
- No display in the map editor.
- No balance or numeric changes; no buildings or blueprints touched.
- No save serialization (pure ephemeral UI; nothing to persist).

---

## 2. Verified current state (all backed by measurements)

| Item | Measured value / evidence |
|---|---|
| Game version | `1.1.2.8-ffc2de0-sm` (`Info.plist` `CFBundleShortVersionString`) |
| Unity / runtime | `6000.5.11f1`, Mono; 658 assemblies under `Data/Managed` are referenceable |
| Official mod support | `Player.log` line 50: `Modded: true, official`; 5 mods already run normally |
| Mod folder | `~/Documents/Timberborn/Mods/SurvivalBrief/` (currently only `manifest.json` + `thumbnail.png`, `Id: Empty.Timer`) |
| Reference implementations | Workshop Ladder (building mod) and Goods Statistics (HUD mod) use the same mechanism |
| Build toolchain | **No .NET SDK on this machine** (`dotnet` missing); install .NET 8 SDK to compile |
| Unity required? | **No**. See §6.1: all types/members used are `public`; no DllPublicizer or Unity project needed |
| UI assets | Stock UXML/USS live in `Data/StreamingAssets/Modding/UI.zip`; style class names can be reused directly |

---

## 3. In-game weather model (decompile-verified, not guessed)

### 3.1 Data sources

All from `Timberborn.WeatherSystem` / `Timberborn.HazardousWeatherSystem` / `Timberborn.GameCycleSystem`:

| Member | Meaning | Verified semantics (IL-level) |
|---|---|---|
| `GameCycleService.Cycle` | Current cycle index | 1, 2, 3… |
| `GameCycleService.CycleDay` | Day within current cycle (integer) | Starts at 1 |
| `GameCycleService.PartialCycleDay` | Day within cycle (fractional progress) | `0.0 ~ CycleLengthInDays` |
| `WeatherService.TemperateWeatherDuration` | Temperate days this cycle | From `TemperateWeatherDurationService` |
| `WeatherService.HazardousWeatherDuration` | Hazard days this cycle | From `HazardousWeatherService` |
| `WeatherService.CycleLengthInDays` | Total days this cycle | Temperate + hazard |
| `WeatherService.HazardousWeatherStartCycleDay` | First hazard day | **= temperate days + 1** |
| `WeatherService.IsHazardousWeather` | Currently in hazard phase | **= `CycleDay >= HazardousWeatherStartCycleDay`** |
| `WeatherService.NextDayIsHazardousWeather()` | Hazard tomorrow? | `Start <= CycleDay+1 <= CycleLength` |
| `HazardousWeatherService.CurrentCycleHazardousWeather` | Hazard type this cycle | `IHazardousWeather`, `Id` is `DroughtWeather` / `BadtideWeather` |
| `HazardousWeatherApproachingTimer.DaysToHazardousWeather` | Days until hazard starts (float) | **= `HazardousWeatherStartCycleDay - PartialCycleDay`** |

Key formulas (use as-is; no need to re-derive):

```
Total days     = temperate + hazard
Hazard starts  = temperate + 1
Days to hazard = hazard start day − PartialCycleDay   // float; positive = not yet started
Hazard left    = total days − PartialCycleDay + 1     // while in hazard phase
```

### 3.2 What a real cycle looks like

Cycle = temperate phase → hazard phase (hazard is always at the tail, never at the start). So “days to hazard” counts down during temperate; after hazard starts it goes negative — switch display logic with `IsHazardousWeather`.

### 3.3 Why next-cycle “drought vs badtide” cannot be predicted early

- Hazard type is chosen at **cycle start** by `HazardousWeatherService.SetForCycle(cycle)`; `HazardousWeatherSelectedEvent` fires only after selection.
- `BadtideWeather.CanOccurAtCycle(cycle)` / `GetBadtideChance()` only answer whether badtide **can** occur this cycle and at what probability — not the actual outcome.
- Conclusion: the mod only shows **this cycle’s** hazard; that is the data limit and matches vanilla behavior.

---

## 4. What vanilla already has (gap analysis)

Vanilla top-right has `WeatherPanel` (`UI.zip → Views/Game/WeatherPanel.uxml`):

```
NineSliceVisualElement  name=WeatherPanel  class="top-right-item weather-panel square-large--green"
├── ProgressRoot / Progress  → progress when hazard is approaching or active
└── Label ForecastCounter    → day count
```

Behavior (`WeatherPanel.SetPanelContent`, IL-verified):

- **Temperate, outside “approaching” window**: number hidden (`ToggleDisplayStyle(daysFloat > 0)`); only weather icon + tooltip.
- **Inside approaching window** (`ApproachingNotificationDays`): shows “days left”. Measured value **3 days** (`Blueprints.zip → Configurations/HazardousWeatherUI.blueprint.json`: `ApproachingNotificationDays: 3`).
- **Hazard active**: shows remaining days.

**Clear vanilla gaps:**
1. Outside the last 3 temperate days, no day count on screen — tooltip only.
2. During hazard the number is small and ambiguous (“3” = left or already 3 days? user must guess).

The mod’s value is **always-on, labeled, unambiguous** information. That is the sole design driver for this plan.

---

## 5. Display options compared

| Option | Approach | Pros | Cons | Verdict |
|---|---|---|---|---|
| A. Standalone top-right panel | `UILayout.AddTopRight(element, order)` with custom panel beside vanilla weather | Natural placement, side-by-side, no UI occlusion, public API | Must build VisualElement + reuse USS classes for look | **Recommended** |
| B. Bottom bar | `UILayout.AddBottomRight(...)` | Same implementation pattern | Bottom bar is actions/tools; weather info feels wrong there | Fallback |
| C. Patch vanilla `WeatherPanel` text | Harmony on `WeatherPanel.SetPanelContent` | 100% native look | Conflicts with Mod Settings / other HUD mods; breaks on vanilla updates | Not recommended |

**Recommend A**: public API (`AddTopRight` is `public`), no invasive patches, same pattern as mature mods like Goods Statistics.

Suggested two-state copy:

```
Temperate:  [☀] Drought in 3 days
Hazard:     [☠] Badtide, 4 days left
```

- Hazard type first (drought / badtide), then days — avoids a lone number.
- Optional: reuse vanilla blink styles when ≤3 days (P3).

---

## 6. Technical implementation

### 6.1 Build approach (key conclusion: no Unity)

| Decision | Choice | Rationale |
|---|---|---|
| Target framework | `netstandard2.1` | Unity 6 Mono game assemblies use netstandard 2.1 profile; `Managed/netstandard.dll` exists |
| Compiler | .NET 8 SDK `dotnet build` (install required; not on machine yet) | Only need a dll output; no Unity Editor |
| Game DLL refs | `<HintPath>` to `…/Timberborn.app/Contents/Resources/Data/Managed/*.dll`, `<Private>false</Private>` | Do not bundle game dlls in the mod |
| Publicizer needed? | **No** | Verified: `UILayout`, `WeatherService`, `HazardousWeatherService`, `EventBus`, `ILoc`, `NineSliceVisualElement`, etc. — **types and needed members are all public** |
| Harmony needed? | No | Read state + custom UI only; no vanilla method patches |

Required assembly references: `Timberborn.WeatherSystem`, `Timberborn.HazardousWeatherSystem`, `Timberborn.HazardousWeatherSystemUI`, `Timberborn.GameCycleSystem`, `Timberborn.UILayoutSystem`, `Timberborn.CoreUI`, `Timberborn.SingletonSystem`, `Timberborn.Localization`, `Bindito.Core`, `UnityEngine.CoreModule`, `UnityEngine.UIElementsModule`.

### 6.2 Mod folder layout (`Mods/SurvivalBrief/`)

```
Mods/SurvivalBrief/
├── manifest.json              # exists; change Id → Jove.Timer, Version 0.1.0
├── thumbnail.png              # exists
├── Scripts/
│   └── Jove.Timer.dll         # build output (game only loads dlls here)
├── Localizations/
│   ├── enUS.csv
│   └── zhCN.csv
├── src/                       # source and project (not loaded by game)
│   ├── Jove.Timer.csproj
│   ├── TimerConfigurator.cs
│   ├── WeatherCountdown.cs
│   └── WeatherCountdownPanel.cs
└── docs/
    ├── README.md
    ├── README.zh-CN.md
    └── README.en-US.md
```

Note: **do not** create a `version-x` subfolder at mod root; if present, the game ignores root content and only loads the version subfolder.

### 6.3 Code skeleton (three parts, all public APIs)

```csharp
// 1) Registration: Bindito auto-discovers Configurator with [Context("Game")] in the mod assembly
[Context("Game")]
public class TimerConfigurator : Configurator
{
    protected override void Configure()
    {
        Bind<WeatherCountdown>().AsSingleton();
        Bind<WeatherCountdownPanel>().AsSingleton();
    }
}

// 2) Data layer: read-only state, event-driven refresh
public class WeatherCountdown : ILoadableSingleton
{
    // ctor inject: GameCycleService, WeatherService, HazardousWeatherService, EventBus
    // [OnEvent] subscribe CycleDayStartedEvent / HazardousWeatherStartedEvent / HazardousWeatherEndedEvent
    public string Text { get; }      // localized display string
    public bool IsWarning { get; }
    public void Load() { /* build UI, first refresh */ }
}

// 3) UI layer: custom panel on top-right
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

Style classes from stock USS (confirmed): `.top-right-item` (GameMiscStyle.uss:167), `.square-large--green` (CommonStyle.uss:123), `.game-text-normal`, `.text--yellow`.

### 6.4 Localization

Mod strings live in `Localizations/<locale>.csv` (`ID,Text,Comment`) merged into the game table; filename is the locale code. New keys use a common prefix:

```
ID,Text,Comment
Jove.Timer.DroughtIn,Drought in {0} days,-
Jove.Timer.BadtideIn,Badtide in {0} days,-
Jove.Timer.DroughtLeft,Drought ends in {0} days,-
Jove.Timer.BadtideLeft,Badtide ends in {0} days,-
```

zhCN uses vanilla terms (`Weather.Drought=干旱`, `Weather.Badtide=恶水潮` from stock `zhCN.csv`):

```
Jove.Timer.DroughtIn,{0} 天后干旱,-
Jove.Timer.BadtideIn,{0} 天后恶水潮,-
Jove.Timer.DroughtLeft,干旱剩 {0} 天,-
Jove.Timer.BadtideLeft,恶水潮剩 {0} 天,-
```

Hazard names can reuse vanilla keys `Weather.Drought` / `Weather.Badtide` instead of hard-coded Chinese.

---

## 7. Phased delivery and acceptance

### P0 Probe (prove data before UI)
- Output: minimal `Jove.Timer.dll`, no UI; one `Player.log` line per game day:
  `[Timer] cycle=.. day=.. partial=.. start=.. len=.. hazard=.. id=.. daysTo=..`
- Goal: calibrate **rounding** (ceil vs round) on real numbers; confirm `PartialCycleDay` fractional behavior when paused/fast-forwarded.
- Acceptance: one full cycle (≥12 days) with correct log lines; `daysTo` matches vanilla `WeatherPanel` in the last 3 days.

### P1 Data layer complete
- Two-state text, hazard type id, edge cases (`HazardousWeatherDuration=0`, saves with badtide disabled).
- Acceptance: log shows strings like “Drought in 3 days / Badtide 4 days left”; negatives do not leak into temperate branch.

### P2 Persistent panel (shipping shape)
- `AddTopRight` panel, localized copy, hazard-phase colors/icons.
- Acceptance: at 1440×900 and 3456×2234, does not cover vanilla weather icon or top buttons; screenshots.

### P3 Polish
- Blink when ≤3 days (`.weather-panel--blink` pattern); enUS copy; `manifest.json` text and thumbnail; optional Steam Workshop upload.

---

## 8. Verification (per phase)

| Goal | Action / command |
|---|---|
| Install SDK | `curl -sSL https://dot.net/v1/dotnet-install.sh \| bash -s -- --channel 8.0`, then `~/.dotnet/dotnet --version` |
| Build | `cd Mods/SurvivalBrief/src && dotnet build -c Release`, copy artifact to `../Scripts/` |
| Loaded? | `grep -n "Modded" ~/Library/Logs/Mechanistry/Timberborn/Player.log` → list should include `Timer (v0.1.0)` |
| Probe data | `grep "\[Timer\]" ~/Library/Logs/Mechanistry/Timberborn/Player.log` |
| Skip days | In-game dev mode (`Alt + Shift + Z`) to advance seasons quickly |

---

## 9. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Rounding differs from vanilla | Off by 1 day | P0 log compare with vanilla; pick `Ceiling` vs `Floor` |
| Per-frame string updates → GC | FPS drops in large colonies | Update only on `CycleDayStartedEvent` / weather events / value changes |
| Other HUD mods crowd top-right | Panel hidden | Use `AddTopRight(element, order)` to sit left of weather icon |
| Minor game update breaks API | Mod stops working | `manifest.json` `MinimumGameVersion` `1.1.0.0`; `version-1.1` subfolder if needed |
| Badtide disabled (old save / difficulty) | Shows “badtide” but it never comes | Check `BadtideWeather.CanOccurAtCycle(cycle)` and `GetBadtideChance()`; if 0, show drought only |

---

## 10. Decision log (2026-11-15, adopted per recommendations)

| Topic | Decision |
|---|---|
| Placement | Standalone top-right panel via `UILayout.AddTopRight(element, order)`, left of vanilla weather icon |
| Copy shape | Category + days: `Drought in 3 days` / `Badtide 4 days left`; terms follow official names (`Weather.Badtide` = 恶水潮) |
| Day rounding | Round up (`CeilToInt`; 0.2 days left shows as 1) |
| Scope | Current-cycle hazard only; next-cycle type unpredictable (§3.3) |

Progress: P0 probe calibrated in play; P1 data layer + P2 top panel shipped (panel text like `Badtide in 1 day 8 hours`, truncate don't round, game-hour precision, switch to minutes in the last hour). Found the game loads the same mod assembly **3 times** so static fields are not shared; dedup checks `UILayout._topRight` for an element with the same name.

---

## 11. Resource statistics (requirements analysis, metrics TBD)

### 11.1 Landscape: 60 goods, 5 stat groups

Sources: `StreamingAssets/Modding/Blueprints.zip → Goods/` (60 `GoodSpec`) + vanilla `zhCN.csv` names; groups from `GoodGroups/GoodGroup.*.blueprint.json` (`GoodGroupSpec`, order 10/20/30/50/60).

| Group (order) | Count | Single-good group? | Contents |
|---|---|---|---|
| Materials (10) | 24 | No | Logs, planks, gears, paper, books, resin, resin planks, dandelions, antidote, scrap metal, metal parts, metal blocks, dynamite, fireworks, extract, golem body/limbs/head, biofuel, punch cards, catalyst, lubricant, coffee, dirt |
| Ingredients (20) | 17 | No | Potatoes, chestnuts, spaddle bulbs, wheat, flour, cattail roots, cattail flour, maple syrup, cassava, soybeans, rapeseed, canola oil, corn, eggplants, mushrooms, algae, coffee beans |
| Food (30) | 17 | No | Berries, carrots, sunflower seeds, baked potato/chestnut/spaddle, bread, cattail cookies, maple pastries, turnips, mangrove fruit, fermented cassava/soy/mushroom, corn rations, eggplant rations, algae rations |
| Water (50) | 1 | Yes | Water |
| Badwater (60) | 1 | Yes | Badwater (waste; normally not worth tracking) |

### 11.2 Readable stat APIs (verified public)

| Data | API | Notes |
|---|---|---|
| Group / per-good stock | `ContextualResourceCountingService.GetContextualResourceCount(goodId)` | Same as vanilla top counters; respects current selection |
| Global / per district | `ResourceCountingService.GetGlobalResourceCount` / `GetDistrictResourceCount` | District dimension |
| Capacity | `ResourceCount.TotalCapacity`, `InputOutputCapacity` | Not shown in vanilla UI |
| Fill rate | `ResourceCount.FillRate` | 0–1 |
| In transit | `CarriedToStockpilesStock`, `CarriedToProcessors` | Being hauled |
| Processing | `StockUnderProcessing`, `BufferedInput`, `BufferedOutputStock` | Production chain intermediates |
| Production / consumption | `DistrictGoodsBalance.GetProduction(id)` / `GetConsumption(id)` | Daily totals; net change possible |

### 11.3 Overlap with vanilla / installed mods (avoid duplicating wheels)

- **Vanilla top bar already has 5 group counters**: `TopBarPanel` iterates `GoodGroupSpecs` (water/badwater single value; materials/ingredients/food expandable). Hover shows warehouse detail. If missing in screenshots, counters may be collapsed or off-screen.
- **Installed `Goods Statistics`** adds trend arrows, days-until-empty, and batch stats on the top bar.
- Conclusion: “stock count only” duplicates existing UI. Differentiation: **capacity/fill rate**, **production − consumption net**, **persistent detail list**.

### 11.4 Four candidate designs

| Design | Content | Assessment |
|---|---|---|
| R1 Key resources (recommended first) | Fixed 6: water, food, logs, planks, gears, metal blocks; show `qty/capacity` | Survival + expansion bottlenecks; capacity is new info; fits one screen |
| R2 All five groups | One row per group `qty/capacity`, like vanilla but with capacity | High overlap; crowded top bar |
| R3 Net change | Per group `today production − consumption` | Conflicts with Goods Statistics |
| R4 Expandable detail | Drill into 17–24 goods per group | High interaction cost; defer to V2 |

### 11.5 TBD (see §12)

Stat scope, display metrics, panel placement.

---

## 12. Resource statistics (implemented v0.1, confirm metrics with you)

### 12.1 Clarification: “resources” here mean **uncollected natural resources**, not warehouse stock

Two easy-to-confuse categories:

| Category | Count | Data source | In scope? |
|---|---|---|---|
| Goods (warehouse) | 60 / 5 groups | `GoodSpec` + `ResourceCountingService` | ❌ vanilla top counters exist |
| **Uncollected natural resources** | **22** (7 trees + 3 bushes + 12 crops) | entities tagged `NaturalResource` + `Cuttable`/`Gatherable` yields | ✅ this feature |

22 natural resources and yields (from `Blueprints.zip → NaturalResources/`):

| Category | Types (yield) |
|---|---|
| Trees (7) | Birch (log×1), pine (log×2, resin×2), oak (log×8), maple (log×6, syrup×3), chestnut (log×4, chestnut×3), mangrove (log×2, fruit×4), succulent (water×2) |
| Bushes (3) | Blueberry (berry×3), coffee (bean×1), dandelion (dandelion×1) |
| Crops (12) | Carrot×3, potato×1, wheat×3, turnip×2, corn×2, eggplant×3, cassava×1, soybean×2, rapeseed×3, sunflower×2, cattail root×3, spaddle×3 |

### 12.2 Implemented metrics

- **Count**: living entities on the map (`LivingNaturalResource.IsDead == false`).
- **Maturity**: `Growable.IsGrown`; tooltip “mature N / total N”.
- **Total yield**: sum `YielderSpec.Yield.Amount` × count per type, e.g. “logs ×336”.
- Implementation: scan `EntityRegistry.Entities` → group by `LabeledEntity.DisplayName`; recompute every 3s, skip UI rebuild if content fingerprint unchanged.
- Placement: `UILayout.AddBottomRight`, compact two-column list, section headers (trees / bushes / crops), only types with count > 0.

### 12.3 “Current / initial” added (confirmed 2026-11-15)

Natural resources have no warehouse capacity; “qty/capacity” becomes **current count / count at load**:

- Snapshot: baseline on first non-empty scan (start of session after load); session-only, not saved.
- Row: `Oak 42/60` (if initial was 0 for a new type, show `12` only).
- Header: `126 / 168 total`.
- Tooltip three lines: `Oak: 42 (30 mature)` / `60 at load (18 depleted)` / `Yield: logs ×336`.
- Note: “depleted since this load”, not map-gen original — not stored in saves, cannot reconstruct.

---

## 13. Consumption analysis (2026-11-15 pivot: replaces the uncollected-resources panel)

### 13.1 Requirements source: spec-backed, not estimated

| Data | Source | Measured |
|---|---|---|
| Hunger decay | `NeedSpec(Id=Hunger).DailyDelta` | **−0.8 / day** per beaver |
| Thirst decay | `NeedSpec(Id=Thirst).DailyDelta` | **−0.7 / day** per beaver |
| Food satiation per serving | `GoodSpec.ConsumptionEffects[NeedId=Hunger].Points` | **all foods 0.30** |
| Water per serving | `GoodSpec(Water).ConsumptionEffects[NeedId=Thirst].Points` | **0.33** |

Core arithmetic:

- **Each beaver eats ~0.8 ÷ 0.3 ≈ 2.67 food servings/day, i.e. one serving every 9 hours.**
- **Each beaver drinks ~0.7 ÷ 0.33 ≈ 2.12 water/day, i.e. one serving every 11.3 hours.**
- All 17 foods share 0.30 hunger points; only diet diversity differs (mostly +0.20, sunflower +0.50) — **switching food type does not save food, only diversity**.

### 13.2 Building consumption

- Scan all `Manufactory` with `Workshop.CurrentlyWorking == true`, read `Manufactory.CurrentRecipe`.
- Daily consumption = `recipe input qty × 24 ÷ recipe cycle hours`.
- Aggregate by building type × good, e.g. “Bakery×2: wheat 12/day”.

### 13.3 Panel content (bottom-right)

```
Consumption analysis
17 beavers (12 adult / 5 child)
Per beaver per day: food 0.8 pts · water 0.7 pts
Food
 [icon] Berries   2.7/beaver·day · 45/day · lasts 20 days
Water
 [icon] Water     2.1/beaver·day · 36/day · lasts 12 days
Building consumption
 Bakery×2 wheat 12/day
```

- Food types with stock > 0 only (max 8 rows), sorted by per-unit use; “days left” = stock ÷ colony daily use.
- Tooltip: `Berries: 0.3 pts per serving / one every 9 h, stock 900`.
- Juveniles count (hunger/thirst apply to all beavers).
- Refresh: population events + 2s fallback; no UI rebuild if fingerprint unchanged.

### 13.4 Known limits

- No “construction material” totals (logs/planks etc.) unless added separately.
- Building use counts only “currently working”; idle/out of inputs drops to zero (actual state, not theoretical throughput).
- Bots do not eat; excluded from eaters.

### 13.5 Construction demand and fuel (added 2026-11-15)

- **Construction demand**: scan `ConstructionSite`, read `Inventory.AllowedGoods` (per-material cap) and `Inventory.Stock` (delivered); shortfall summed by good + count of active sites. Display `Logs 120 · 3 sites building`.
- **Fuel**: `RecipeSpec.Fuel` is a **good id string** (not a quantity); one unit per `CyclesFuelLasts` production cycles → daily = `24 ÷ (cycle hours × CyclesFuelLasts)`, merged into building consumption rows.
- Final section order: population → per-beaver daily needs → food → water → construction demand → building consumption.

---

## 14. Pitfall: extra commas in localization CSV crash startup

**Symptom**: error on launch, `InvalidDataException: Localization file contains errors: Unnecessary comma found in enUS (line 13/18/22)`, stack in `LocalizationCsvValidator.Validate` → `LocalizationService.Load` → `SingletonLifecycleService.LoadAll`; game aborts during singleton load — mod unusable.

**Cause**: `Localizations/<locale>.csv` is strict 3 columns `ID,Text,Comment`. English commas in text (`Per beaver per day: food {0} pts, water {1} pts`) parse as a 4th column; validator throws. Zero tolerance — no row skip.

**Fix**: use middle dot `·` instead of comma, or wrap field in double quotes (`ID,"text, with comma",-`).

**Regression guard**: `tools/check-localization.py` after copy edits:

```
python3 tools/check-localization.py    # expect: enUS.csv OK / zhCN.csv OK
```

Chinese copy uses full-width `，`, which avoids this; watch English punctuation.

---

## 15. Pitfall: blueprint goods missing from save break entire refresh

**Symptom**: panel shows only “No beavers yet”, all sections blank; `Player.log` every 2s:
`[Timer] consumption refresh failed: System.ArgumentException: GoodSpec with id AlgaeRation not found!`

**Cause**: `ISpecService.GetSpecs<GoodSpec>()` returns **all blueprints**, including goods unavailable this session (`Good.AlgaeRation` is `GoodCollection.IronTeeth`, absent in Folktails saves). `GoodDescriber.Describe(id)` uses `IGoodService` and throws on unregistered goods; exception aborts `Refresh()` at step 3 before population fields update → “No beavers yet”.

**Fix** (both required):

1. When iterating foods, filter with `IGoodService.HasGood(id)` first.
2. Wrap all `GoodDescriber.Describe/GetIcon` in try/catch; fallback to good id, log warning once.

**Lesson**: blueprint specs must align with **this session’s loaded good table**; one exception must not zero the whole panel.

---

## 16. Panel simplified to “collapsed row + click overlay” (2026-11-15)

**Why**: full panel hogged bottom-right and covered vanilla UI; players usually only need “how many days left”.

**Structure**:

| Layer | Mount | Content |
|---|---|---|
| Collapsed bar | `UILayout.AddBottomRight(..., 1)` | One line: `Consumption 129 beavers · food 52.3 d · water 24.3 d` |
| Overlay | `UILayout.AddAbsoluteItem(...)`, `position: absolute; right: 12; bottom: 64` | Population / per-beaver needs / food (total + per type) / water / construction / buildings |

- Click bar toggles overlay; “Close” top-right on overlay.
- Overlay on absolute layer (`_absoluteItems`) to avoid bottom-bar clipping.
- Rebuild overlay only when visible and fingerprint changed; bar refreshes on any data change.
- Dedup by name (`TimerConsumptionCompact` / `TimerConsumptionDetail`) for triple assembly load.

---

## 17. Detail view uses vanilla dialog shell (2026-11-15)

**Conclusion**: feasible without hand-drawn parchment — appearance comes from `Common/NamedBoxTemplate`
(`content-row-centered` + `sliced-border box__content-container` + `capsule-header` + `close-button`), same as population wellbeing and settings.

**Integration** (same pipeline as vanilla panels — centering, ESC, input block included):

1. `ConsumptionDetailPanel : IPanelController` (`GetPanel()` / `OnUIConfirmed()` / `OnUICancelled()`)
2. `VisualElementLoader.LoadVisualElement("Common/NamedBoxTemplate")` for shell
3. `Q<Label>("Header").text = title`, `Q<Button>("CloseButton").clicked += Close`
4. `Q<VisualElement>(null, "box__content-container")` → `game-scroll-view` scroll host, then content
5. Open: `PanelStack.Push(panel)`; close: `PanelStack.Pop(this)` (ESC → `OnUICancelled`)

**Did not reuse `PopulationWellbeingBox` UXML body**: rows are fixed (wellbeing, faction unlocks); only the shell template was reused. `DialogBox` / `DialogBoxShower` is text + three buttons only — poor fit.

### 17.1 Two implementation fixes (2026-11-15)

- **Cannot load `Common/NamedBoxTemplate` standalone**: Header is `LocalizableLabel` (`ILocalizableElement`); without `text-loc-key` from UXML, initializer throws `InvalidOperationException: text-loc-key is not set for ILocalizableElement`. Built manually with same class names: `content-row-centered` → `sliced-border box__content-container` → `capsule-header` + Label + `close-button`.
- **Building names need fallbacks beyond `LabeledEntity`**: not always present → `manufactories=0`. Fallback chain: `LabeledEntity.DisplayName` → `NamedEntity.EntityName` → recipe `DisplayLocKey` → recipe `Id`.
- **Construction demand must filter empty inventories**: 938 entities may still have `Inventory:ConstructionSite` after completion; require `Inventory != null && Capacity > 0 && shortfall > 0`.

---

## 18. UI reshuffle: centered dialog + tables, bottom one-liner (2026-11-15)

**Collapsed bar** (simplified): drop title; one line `129 · food 52.3 d · water 24.3 d`, tooltip “Click for consumption details”.

**Dialog**:
- Center: root `flexGrow:1 + alignItems/justifyContent: center`;
- Responsive: width `62%`, `minWidth 460 / maxWidth 780`; scroll area height `Screen.height × 0.55` (clamp 320–900px);
- Structure:
  - Population block: large count + `beavers (adult x · child y)`, gray line “Per beaver per day: food 0.8 pts · water 0.7 pts”;
  - Food/water: section title + gray header (name / stock / daily / lasts) + rows (icon + name + right-aligned numbers);
  - Days-left bars: 30 days = full bar; `<2 red / <6 yellow / else green`;
  - Construction: `good · N sites · M short`;
  - Buildings: `name ×N (M working) · input daily/day`.

---

## 19. Dialog rows like “wellbeing” layout + full-screen height fix (2026-11-15)

**Full-height bug**: `CoreStyle.uss` `.box__content-container { align-self: stretch; flex-grow: 1; }` inside a `flexGrow:1` fullscreen center container stretched the border to full screen while content sat at the top. Fix: override `flex-grow: 0; align-self: center` so height follows content; `ScrollView` caps height only when needed.

**Row layout (aligned with population wellbeing)**:

```
Basic-needs-style section title
 Name (132px)   full-width bar (centered text: stock 16820 · daily 363)   capsule (74px)
 Berries       ███████████████████░░░░░░░            46.4 d
 Bread         ███████████████████████████          60.7 d
```

- No table header; info inside bar; right capsule “X days left” with color by days (>6 green / 2–6 yellow / <2 red).
- Bar full scale = 20 days; row height 22px, near wellbeing UI.
- Construction: `good … 3 sites … [short 12]`; buildings: `name … ×4 (4 working) … [192/day]`.

### 19.1 Spacing and scrollbar (2026-11-15)

- Row spacing: bar height `30px` (was 22), `6px` between rows, section title `14px` top / `6px` bottom, matching wellbeing rhythm.
- Scrollbar: `new ScrollView(ScrollViewMode.Vertical)` + `verticalScrollerVisibility = Auto` + `game-scroll-view`; dragger from `GameStyle.uss` `.game-scroll-view > ... > .unity-slider .unity-base-slider__dragger` (`bg-pixel-2`).
- Window height: border `maxHeight: 82%`; first `GeometryChangedEvent` also caps scroll at `resolvedStyle.height × 0.72` (UI Toolkit % height needs resolved parent; `Screen.height` is physical pixels — either alone drifts).

### 19.2 Line-by-line match with “population wellbeing” (2026-11-15)

Wellbeing row sizing in `PopulationStyle.uss` (`.population-wellbeing-counter*`):

| Element | Vanilla |
|---|---|
| Row | `height: 20px; margin: 2px 0; flex-direction: row` |
| Name | `width: 40%; padding-left: 12px; left align` |
| Bar frame | `height: 100%; width: 80%; margin-right: 2px`, bg `bg-pixel-2` |
| Bar fill | `height: 100%`, bg `needview-background` (green) / `-red` |
| In-bar text | `position: absolute; width: 100%; -unity-text-align: middle-center` |
| Right value | `width: 50px; margin: 0 6px`, NineSlice + `--background-slice: 10` |

Those classes live in `PopulationStyle.uss` and apply only to wellbeing UXML; HUD docs do not load that sheet — **class names cannot be reused verbatim**; values were hand-replicated: row 22px / 1px vertical margin / name 40% / bar full row height / centered in-bar text / capsule 56px dark fill + 1px color border + colored text (vanilla uses nine-slice; we approximate with border).

Overflow fixes:
- Content in a plain column (`flexGrow:0`) so ScrollView content stretch does not break row gaps;
- On first `GeometryChangedEvent`, real pixel height: scroll 64%, window 84%, with `ScrollerVisibility.Auto` for vanilla-style scrollbar.

### 19.3 Wellbeing row cloning (2026-11-15)

`ConsumptionDetailPanel` now **clones** rows from `Game/Population/PopulationWellbeingBox` and `PopulationWellbeingCounter` (including structure and styling from those templates) instead of hand-built colors and ad-hoc bar markup. Population wellbeing USS still applies to the cloned subtree, so the consumption dialog tracks vanilla wellbeing visuals without maintaining a parallel style sheet.