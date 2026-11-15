using System.Collections.Generic;
using System.Reflection;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.TooltipSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jove.SurvivalBrief
{
    public class ConsumptionDetailPanel : IPanelController
    {
        private const string BoxPath = "Game/Population/PopulationWellbeingBox";
        private const string GroupPath = "Game/Population/PopulationWellbeingCounterGroup";
        private const string RowPath = "Game/Population/PopulationWellbeingCounter";

        private const string PositiveClass = "wellbeing--positive";
        private const string NegativeClass = "wellbeing--negative";

        private const float ScrollMaxHeight = 420f;
        private const float DaysForFullBar = 20f;
        private const float ChipWidth = 76f;

        private static readonly Color HazardFill = new Color(0.79f, 0.62f, 0.22f, 1f);
        private static readonly Color HazardText = new Color(0.95f, 0.82f, 0.45f, 1f);

        private readonly PanelStack _panelStack;
        private readonly VisualElementLoader _visualElementLoader;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly ILoc _loc;
        private readonly ConsumptionModel _model;
        private readonly WeatherCountdown _countdown;
        private readonly System.Action _onClosed;

        private readonly VisualElement _root;
        private readonly ScrollView _scroll;

        private int _groupCount;
        private float _appliedScrollHeight = float.NaN;

        public ConsumptionDetailPanel(
            PanelStack panelStack,
            VisualElementLoader visualElementLoader,
            ITooltipRegistrar tooltipRegistrar,
            ILoc loc,
            ConsumptionModel model,
            WeatherCountdown countdown,
            System.Action onClosed)
        {
            _panelStack = panelStack;
            _visualElementLoader = visualElementLoader;
            _tooltipRegistrar = tooltipRegistrar;
            _loc = loc;
            _model = model;
            _countdown = countdown;
            _onClosed = onClosed;

            _root = Load(BoxPath);
            _root.style.flexGrow = 0;
            _root.style.flexShrink = 0;

            var border = _root.Q(className: "box__content-container");
            border.style.flexGrow = 1;
            border.style.alignSelf = Align.Center;

            _root.Q<Label>(className: "capsule-header__text").text = _loc.T("Jove.SurvivalBrief.Con.DetailTitle");
            _root.Q<Button>("CloseButton").clicked += Close;

            var goals = _root.Q("GoalsWrapper");
            if (goals != null)
            {
                goals.style.display = DisplayStyle.None;
            }

            BuildSummary(_root.Q(className: "population-wellbeing__header"));

            _scroll = _root.Q<ScrollView>("Items");
            _scroll.mode = ScrollViewMode.Vertical;
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _scroll.style.flexGrow = 0;
            _scroll.style.flexShrink = 1;
            _scroll.style.maxHeight = ScrollMaxHeight;
            _scroll.contentContainer.style.flexGrow = 0;
            _scroll.contentContainer.style.flexShrink = 0;
            _scroll.contentContainer.style.paddingBottom = 4;
            _scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(FitScroll);
            _root.RegisterCallback<GeometryChangedEvent>(FitScroll);

            AddFooter();
            Build();
        }

        public VisualElement GetPanel()
        {
            return _root;
        }

        public bool OnUIConfirmed()
        {
            Close();
            return true;
        }

        public void OnUICancelled()
        {
            Close();
        }

        private void Close()
        {
            try
            {
                _panelStack.Pop(this);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SurvivalBrief] failed to pop panel: " + e.Message);
            }

            _onClosed?.Invoke();
        }

        // UXML styles live on the temporary TemplateContainer; copy them onto the root after detach.
        private VisualElement Load(string path)
        {
            var element = _visualElementLoader.LoadVisualElement(path);
            var host = element.parent;
            if (host == null)
            {
                return element;
            }

            var sheets = host.styleSheets;
            for (var i = 0; i < sheets.count; i++)
            {
                var sheet = sheets[i];
                if (sheet != null && !element.styleSheets.Contains(sheet))
                {
                    element.styleSheets.Add(sheet);
                }
            }

            return element;
        }

        private void FitScroll(GeometryChangedEvent evt)
        {
            // Sum child heights; the content container height tracks the viewport when capped.
            var contentHeight = ContentHeight();
            if (contentHeight < 1f)
            {
                return;
            }

            var limit = ScrollLimit();
            var needsScroll = contentHeight > limit + 1f;
            var target = needsScroll ? limit : -1f;
            if (!float.IsNaN(_appliedScrollHeight) && Mathf.Abs(target - _appliedScrollHeight) < 0.5f)
            {
                return;
            }

            _appliedScrollHeight = target;
            if (needsScroll)
            {
                _scroll.style.height = limit;
                _scroll.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            }
            else
            {
                _scroll.style.height = StyleKeyword.Auto;
                _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            }
        }

        private float ContentHeight()
        {
            var height = 0f;
            var any = false;
            foreach (var child in _scroll.contentContainer.Children())
            {
                var childHeight = child.layout.height;
                if (float.IsNaN(childHeight) || childHeight <= 0f)
                {
                    continue;
                }

                any = true;
                height += childHeight;
                var style = child.resolvedStyle;
                if (!float.IsNaN(style.marginTop))
                {
                    height += style.marginTop;
                }

                if (!float.IsNaN(style.marginBottom))
                {
                    height += style.marginBottom;
                }
            }

            var padding = _scroll.contentContainer.resolvedStyle;
            if (!float.IsNaN(padding.paddingTop))
            {
                height += padding.paddingTop;
            }

            if (!float.IsNaN(padding.paddingBottom))
            {
                height += padding.paddingBottom;
            }

            return any ? height : 0f;
        }

        private float ScrollLimit()
        {
            var parent = _root.parent;
            var available = parent != null ? parent.resolvedStyle.height : 0f;
            var limit = ScrollMaxHeight;
            if (!float.IsNaN(available) && available > 320f)
            {
                limit = Mathf.Min(ScrollMaxHeight, available - 200f);
            }

            return Mathf.Max(160f, limit);
        }

        private const string RepoName = "TimberbornSurvivalBriefMod";
        private const string RepoUrl = "https://github.com/rina-games/TimberbornSurvivalBriefMod";

        private void AddFooter()
        {
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.Center;
            footer.style.alignItems = Align.Center;
            footer.style.flexShrink = 0;
            footer.style.marginTop = 8;
            footer.style.marginBottom = 8;

            footer.Add(FooterText(ModVersion() + "  ·  jove.rina@outlook.com  ·  "));

            var link = new VisualElement();
            link.style.flexDirection = FlexDirection.Row;
            link.style.alignItems = Align.Center;
            link.style.flexShrink = 0;
            var icon = GithubIcon();
            if (icon != null)
            {
                link.Add(icon);
            }

            link.Add(FooterText(RepoName));
            link.RegisterCallback<ClickEvent>(OpenRepository);
            System.Func<string> tip = () => RepoUrl;
            _tooltipRegistrar.Register(link, tip);
            footer.Add(link);

            var parent = _scroll.parent;
            parent.Insert(parent.IndexOf(_scroll) + 1, footer);
        }

        private static void OpenRepository(ClickEvent clickEvent)
        {
            Application.OpenURL(RepoUrl);
        }

        private static Label FooterText(string text)
        {
            var label = new Label(text);
            label.AddToClassList("game-text-small");
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.flexShrink = 0;
            return label;
        }

        private static Image GithubIcon()
        {
            var assembly = typeof(ConsumptionDetailPanel).Assembly;
            using (var stream = assembly.GetManifestResourceStream("Jove.SurvivalBrief.github-mark.png"))
            {
                if (stream == null)
                {
                    return null;
                }

                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                {
                    return null;
                }

                texture.filterMode = FilterMode.Bilinear;
                var image = new Image();
                image.image = texture;
                image.style.width = 12;
                image.style.height = 12;
                image.style.marginRight = 4;
                image.style.flexShrink = 0;
                return image;
            }
        }

        private static string ModVersion()
        {
            var info = typeof(ConsumptionDetailPanel).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var version = info == null ? null : info.InformationalVersion;
            if (string.IsNullOrEmpty(version))
            {
                return "0.1.0";
            }

            var plus = version.IndexOf('+');
            return plus < 0 ? version : version.Substring(0, plus);
        }

        private void BuildSummary(VisualElement header)
        {
            header.Clear();
            header.style.flexDirection = FlexDirection.Column;
            header.style.alignItems = Align.Center;

            if (_model.Eaters <= 0)
            {
                header.Add(SummaryLine(_loc.T("Jove.SurvivalBrief.Con.NoBeavers"), true));
                return;
            }

            header.Add(SummaryLine(
                _loc.T("Jove.SurvivalBrief.Con.Pop", _model.Eaters, _model.Adults, _model.Children),
                true));
            header.Add(SummaryLine(
                _loc.T(
                    "Jove.SurvivalBrief.Con.Needs",
                    _model.HungerPerDay.ToString("0.#"),
                    _model.ThirstPerDay.ToString("0.#")),
                false));
        }

        private static Label SummaryLine(string text, bool heading)
        {
            var label = new Label(text);
            label.AddToClassList(heading ? "game-text-heading" : "game-text-normal");
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            if (!heading)
            {
                label.AddToClassList("text--grey");
                label.style.marginTop = 4;
            }

            return label;
        }

        private enum MeterTone
        {
            Normal,
            Low,
            Hazard
        }

        private void Build()
        {
            _scroll.Clear();
            _groupCount = 0;
            _appliedScrollHeight = float.NaN;

            AddOverview();

            if (_model.Eaters <= 0)
            {
                return;
            }

            var food = BeginGroup(_loc.T("Jove.SurvivalBrief.Con.Food"));
            food.Add(Note(_loc.T(
                "Jove.SurvivalBrief.Con.FoodSummary",
                _model.FoodTotalPerDay.ToString("0"),
                _model.FoodTotalDays.ToString("0.#"))));
            AddSupplyRows(food, _model.Foods);

            var water = BeginGroup(_loc.T("Jove.SurvivalBrief.Con.Water"));
            if (_model.Water != null)
            {
                water.Add(SupplyRow(_model.Water));
            }
            else
            {
                water.Add(Note(_loc.T("Jove.SurvivalBrief.Con.None")));
            }

            var sites = 0;
            foreach (var need in _model.ConstructionNeeds)
            {
                sites += need.Sites;
            }

            var building = BeginGroup(_loc.T("Jove.SurvivalBrief.Con.BuildingSection"));
            if (_model.ConstructionNeeds.Count == 0 && _model.BuildingDraws.Count == 0)
            {
                building.Add(Note(_loc.T("Jove.SurvivalBrief.Con.None")));
                return;
            }

            building.Add(Note(_loc.T("Jove.SurvivalBrief.Con.BuildingSummary", sites, _model.BuildingDraws.Count)));
            foreach (var need in _model.ConstructionNeeds)
            {
                building.Add(ConstructionRow(need));
            }

            foreach (var draw in _model.BuildingDraws)
            {
                building.Add(BuildingRow(draw));
            }
        }

        private void AddOverview()
        {
            var rows = new List<VisualElement>();

            var hazard = HazardRow();
            if (hazard != null)
            {
                rows.Add(hazard);
            }

            if (_model.Water != null)
            {
                rows.Add(AggregateSupplyRow(_loc.T("Jove.SurvivalBrief.Con.Water"), _model.Water));
            }

            if (_model.Eaters > 0)
            {
                var buildings = 0;
                var produced = 0f;
                foreach (var food in _model.Foods)
                {
                    buildings += food.ProducerBuildings;
                    produced += food.ProducedPerDay;
                }

                rows.Add(StockMeter(
                    _loc.T("Jove.SurvivalBrief.Con.Food"),
                    buildings,
                    produced,
                    _model.FoodTotalStock,
                    _model.FoodTotalDays,
                    SupplyTooltip(buildings, produced, _model.FoodTotalStock, _model.FoodTotalPerDay, _model.FoodTotalDays)));
            }

            var buildingCount = 0;
            var producing = 0;
            var output = 0f;
            var buildingStock = 0;
            foreach (var draw in _model.BuildingDraws)
            {
                buildingCount += draw.Buildings;
                producing += draw.Working;
                output += draw.OutputPerDay;
                buildingStock += draw.Stock;
            }

            var sites = 0;
            foreach (var need in _model.ConstructionNeeds)
            {
                sites += need.Sites;
            }

            if (buildingCount > 0 || sites > 0 || output > 0f)
            {
                var fill = buildingCount <= 0 ? 0f : Mathf.Clamp01((float)producing / buildingCount);
                var row = MeterRow(
                    _loc.T("Jove.SurvivalBrief.Con.Buildings"),
                    Triple(buildingCount, output, buildingStock),
                    fill,
                    MeterTone.Normal,
                    FormatCount(buildingStock));
                var tooltip = _loc.T(
                        "Jove.SurvivalBrief.Con.TooltipBriefBuildings",
                        FormatCount(buildingCount),
                        producing)
                    + "\n"
                    + _loc.T(
                        "Jove.SurvivalBrief.Con.TooltipBuildingOutput",
                        FormatCount(output),
                        FormatCount(buildingStock))
                    + "\n"
                    + _loc.T("Jove.SurvivalBrief.Con.TooltipBriefSites", sites, _model.BuildingDraws.Count);
                _tooltipRegistrar.Register(row, () => tooltip);
                rows.Add(row);
            }

            if (rows.Count == 0)
            {
                return;
            }

            var items = BeginGroup(_loc.T("Jove.SurvivalBrief.Con.Brief"));
            foreach (var row in rows)
            {
                items.Add(row);
            }
        }

        private VisualElement HazardRow()
        {
            if (_countdown == null || _countdown.Kind == HazardKind.None || _countdown.RemainingDays <= 0f)
            {
                return null;
            }

            var inHazard = _countdown.InHazard;
            string name;
            if (_countdown.Kind == HazardKind.Badtide)
            {
                name = _loc.T(inHazard
                    ? "Jove.SurvivalBrief.Con.HazardBadtideNow"
                    : "Jove.SurvivalBrief.Con.HazardBadtide");
            }
            else
            {
                name = _loc.T(inHazard
                    ? "Jove.SurvivalBrief.Con.HazardDroughtNow"
                    : "Jove.SurvivalBrief.Con.HazardDrought");
            }

            var duration = _countdown.DurationText;
            var fill = Mathf.Clamp01(_countdown.RemainingDays / Mathf.Max(0.01f, _countdown.SpanDays));
            var row = MeterRow(
                name,
                _loc.T("Jove.SurvivalBrief.Con.HazardRemain", duration),
                fill,
                MeterTone.Hazard,
                duration);
            System.Func<string> hazardTip = () =>
            {
                var text = _countdown.Text;
                return string.IsNullOrEmpty(text) ? name : text;
            };
            _tooltipRegistrar.Register(row, hazardTip);
            return row;
        }

        private VisualElement AggregateSupplyRow(string name, SupplyLine line)
        {
            return StockMeter(
                name,
                line.ProducerBuildings,
                line.ProducedPerDay,
                line.Stock,
                line.DaysLeft,
                SupplyTooltip(line.ProducerBuildings, line.ProducedPerDay, line.Stock, line.TotalPerDay, line.DaysLeft));
        }

        private string SupplyTooltip(int buildings, float producedPerDay, float stock, float consumedPerDay, float daysLeft)
        {
            var days = daysLeft.ToString("0.#");
            return _loc.T(
                    "Jove.SurvivalBrief.Con.TooltipBriefSupply",
                    FormatCount(buildings),
                    FormatCount(producedPerDay),
                    FormatCount(stock))
                + "\n"
                + _loc.T(
                    "Jove.SurvivalBrief.Con.TooltipBriefSupplyDays",
                    consumedPerDay.ToString("0.#"),
                    days);
        }

        private VisualElement StockMeter(string name, int buildings, float perDay, float stock, float daysLeft, string tooltip)
        {
            var row = MeterRow(
                name,
                Triple(buildings, perDay, stock),
                Mathf.Clamp01(daysLeft / DaysForFullBar),
                daysLeft < 2f ? MeterTone.Low : MeterTone.Normal,
                _loc.T("Jove.SurvivalBrief.Con.DaysChip", daysLeft.ToString("0.#")));
            _tooltipRegistrar.Register(row, () => tooltip);
            return row;
        }

        private VisualElement BeginGroup(string title)
        {
            var group = Load(GroupPath);
            group.style.flexShrink = 0;
            group.style.width = Length.Percent(100);
            if (_groupCount > 0)
            {
                group.style.marginTop = 12;
            }

            _groupCount++;
            group.Q<Label>("Header").text = title;
            _scroll.Add(group);
            return group.Q("Items");
        }

        private void AddSupplyRows(VisualElement items, List<SupplyLine> lines)
        {
            var shown = 0;
            foreach (var line in lines)
            {
                if (line.Stock <= 0)
                {
                    continue;
                }

                shown++;
                items.Add(SupplyRow(line));
            }

            if (shown == 0)
            {
                items.Add(Note(_loc.T("Jove.SurvivalBrief.Con.None")));
            }
        }

        private VisualElement SupplyRow(SupplyLine line)
        {
            var low = line.DaysLeft < 2f;
            var row = MeterRow(
                line.Name,
                Triple(line.ProducerBuildings, line.ProducedPerDay, line.Stock),
                Mathf.Clamp01(line.DaysLeft / DaysForFullBar),
                low ? MeterTone.Low : MeterTone.Normal,
                _loc.T("Jove.SurvivalBrief.Con.DaysChip", line.DaysLeft.ToString("0.#")));

            var tooltip = _loc.T("Jove.SurvivalBrief.Con.TooltipSupplyHead", line.Name)
                + "\n"
                + _loc.T(
                    "Jove.SurvivalBrief.Con.TooltipSupplyBody",
                    line.ProducerBuildings,
                    FormatCount(line.ProducedPerDay),
                    FormatCount(line.Stock))
                + "\n"
                + _loc.T("Jove.SurvivalBrief.Con.TooltipFoodOnly", line.DaysLeft.ToString("0.#"));
            _tooltipRegistrar.Register(row, () => tooltip);
            return row;
        }

        private VisualElement ConstructionRow(ConstructionNeed need)
        {
            var row = MeterRow(
                need.GoodName,
                _loc.T("Jove.SurvivalBrief.Con.Sites", need.Sites),
                0f,
                MeterTone.Low,
                _loc.T("Jove.SurvivalBrief.Con.Missing", need.Remaining));

            var tooltip = _loc.T("Jove.SurvivalBrief.Con.TooltipConstruction", need.GoodName, need.Remaining, need.Sites);
            _tooltipRegistrar.Register(row, () => tooltip);
            return row;
        }

        private VisualElement BuildingRow(BuildingDraw draw)
        {
            var fill = draw.Buildings <= 0 ? 0f : Mathf.Clamp01((float)draw.Working / draw.Buildings);
            var row = MeterRow(
                draw.BuildingName,
                Triple(draw.Buildings, draw.OutputPerDay, draw.Stock),
                fill,
                MeterTone.Normal,
                FormatCount(draw.Stock));

            var tooltip = _loc.T("Jove.SurvivalBrief.Con.TooltipBuilding", draw.BuildingName)
                + "\n"
                + _loc.T("Jove.SurvivalBrief.Con.TooltipBuildingBody", draw.Buildings, draw.Working)
                + "\n"
                + _loc.T(
                    "Jove.SurvivalBrief.Con.TooltipBuildingOutput",
                    FormatCount(draw.OutputPerDay),
                    FormatCount(draw.Stock));
            _tooltipRegistrar.Register(row, () => tooltip);
            return row;
        }

        private VisualElement MeterRow(string name, string centerText, float fill01, MeterTone tone, string chipText)
        {
            var row = Load(RowPath);
            row.style.flexShrink = 0;

            var nameLabel = row.Q<Label>("Text");
            nameLabel.text = name;

            var low = tone == MeterTone.Low;
            var hazard = tone == MeterTone.Hazard;
            var progress = row.Q("Progress");
            var track = row.Q("ProgressWrapper");
            progress.style.width = Length.Percent(Mathf.Clamp01(fill01) * 100f);
            progress.EnableInClassList(NegativeClass, low);
            track.EnableInClassList(NegativeClass, low);
            if (hazard)
            {
                progress.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                progress.style.backgroundColor = HazardFill;
            }

            var center = row.Q<Label>("Count");
            center.text = centerText;
            center.style.whiteSpace = WhiteSpace.NoWrap;

            var chip = row.Q<Label>("AverageWellbeingShare");
            chip.text = chipText;
            chip.EnableInClassList(PositiveClass, tone == MeterTone.Normal);
            chip.EnableInClassList(NegativeClass, low);
            if (hazard)
            {
                chip.style.color = HazardText;
                chip.style.width = StyleKeyword.Auto;
                chip.style.minWidth = ChipWidth;
            }

            chip.style.width = ChipWidth;
            chip.style.flexShrink = 0;
            chip.style.whiteSpace = WhiteSpace.NoWrap;

            return row;
        }

        private string Triple(float buildings, float perDay, float stock)
        {
            return _loc.T(
                "Jove.SurvivalBrief.Con.BarText",
                FormatCount(buildings),
                FormatCount(perDay),
                FormatCount(stock));
        }

        private static string FormatCount(float value)
        {
            var abs = Mathf.Abs(value);
            if (abs >= 1000000f)
            {
                return (value / 1000000f).ToString("0.#") + "M";
            }

            if (abs >= 1000f)
            {
                return (value / 1000f).ToString("0.#") + "K";
            }

            return Mathf.RoundToInt(value).ToString();
        }

        private static Label Note(string text)
        {
            var label = new Label(text);
            label.AddToClassList("game-text-normal");
            label.AddToClassList("text--grey");
            label.style.paddingLeft = 12;
            label.style.marginTop = 2;
            label.style.marginBottom = 4;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}