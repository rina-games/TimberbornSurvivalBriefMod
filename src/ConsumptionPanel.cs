using System.Reflection;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using Timberborn.UILayoutSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jove.SurvivalBrief
{
    public class ConsumptionPanel : ILoadableSingleton, IUpdatableSingleton
    {
        private const string VanillaPanelPath = "Game/DatePanel";
        private const string PanelName = "SurvivalBriefBar";
        private const string WeatherPanelName = "WeatherPanel";
        private const int TopRightOrder = 8;

        private static readonly FieldInfo TopRightField = typeof(UILayout).GetField(
            "_topRight",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly UILayout _uiLayout;
        private readonly EventBus _eventBus;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly ILoc _loc;
        private readonly ConsumptionModel _model;
        private readonly WeatherCountdown _countdown;
        private readonly PanelStack _panelStack;
        private readonly VisualElementLoader _visualElementLoader;

        private VisualElement _bar;
        private Label _label;
        private bool _attached;
        private ConsumptionDetailPanel _openPanel;
        private string _lastText = "";
        private HazardKind _lastKind = HazardKind.None;
        private float _placedRight = float.NaN;
        private float _placedTop = float.NaN;

        public ConsumptionPanel(
            UILayout uiLayout,
            EventBus eventBus,
            ITooltipRegistrar tooltipRegistrar,
            ILoc loc,
            ConsumptionModel model,
            WeatherCountdown countdown,
            PanelStack panelStack,
            VisualElementLoader visualElementLoader)
        {
            _uiLayout = uiLayout;
            _eventBus = eventBus;
            _tooltipRegistrar = tooltipRegistrar;
            _loc = loc;
            _model = model;
            _countdown = countdown;
            _panelStack = panelStack;
            _visualElementLoader = visualElementLoader;
        }

        public void Load()
        {
            _eventBus.Register(this);
            BuildBar();
            Refresh();
        }

        [OnEvent]
        public void OnShowPrimaryUI(ShowPrimaryUIEvent showPrimaryUIEvent)
        {
            Attach();
        }

        public void UpdateSingleton()
        {
            if (!_attached)
            {
                return;
            }

            _model.ConsumeDirty();
            Refresh();
            PlaceBesideWeather();
        }

        private void BuildBar()
        {
            _bar = _visualElementLoader.LoadVisualElement(VanillaPanelPath);
            _bar.name = PanelName;
            _bar.style.position = Position.Absolute;
            _bar.style.left = StyleKeyword.Auto;
            _bar.style.width = StyleKeyword.Auto;
            _bar.style.flexGrow = 0;
            _bar.style.marginTop = 0;
            _bar.style.marginLeft = 0;
            _bar.style.marginRight = 0;
            _label = _bar.Q<Label>("Text");
            if (_label != null)
            {
                _label.style.flexGrow = 0;
                _label.style.whiteSpace = WhiteSpace.NoWrap;
            }

            _bar.RegisterCallback<ClickEvent>(OnClicked);
            _tooltipRegistrar.Register(_bar, () => _loc.T("Jove.SurvivalBrief.Con.Hint"));
        }

        private void OnClicked(ClickEvent clickEvent)
        {
            if (_openPanel != null)
            {
                return;
            }

            try
            {
                var panel = new ConsumptionDetailPanel(
                    _panelStack,
                    _visualElementLoader,
                    _tooltipRegistrar,
                    _loc,
                    _model,
                    _countdown,
                    () => _openPanel = null);

                _openPanel = panel;
                _panelStack.Push(panel);
            }
            catch (System.Exception e)
            {
                _openPanel = null;
                Debug.LogError("[SurvivalBrief] failed to open detail panel: " + e);
            }
        }

        private void Attach()
        {
            if (_attached || _bar == null)
            {
                return;
            }

            if (AlreadyAttached())
            {
                return;
            }

            _attached = true;
            _uiLayout.AddTopRight(_bar, TopRightOrder);
            _bar.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Refresh();
            PlaceBesideWeather();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            PlaceBesideWeather();
        }

        private bool AlreadyAttached()
        {
            try
            {
                if (TopRightField == null)
                {
                    return false;
                }

                var container = TopRightField.GetValue(_uiLayout) as VisualElement;
                return container != null && container.Q(PanelName) != null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SurvivalBrief] dedupe check failed: " + e.Message);
                return false;
            }
        }

        private void Refresh()
        {
            if (_label == null)
            {
                return;
            }

            var weather = _countdown.Text ?? "";
            var stock = StockText();
            var text = weather.Length == 0 ? stock : weather + " · " + stock;
            var kind = _countdown.Kind;
            if (text == _lastText && kind == _lastKind)
            {
                return;
            }

            _lastText = text;
            _lastKind = kind;
            _label.text = text;
            _bar.EnableInClassList("date-panel--drought", kind == HazardKind.Drought);
            _bar.EnableInClassList("date-panel--badtide", kind == HazardKind.Badtide);
            _placedRight = float.NaN;
        }

        private string StockText()
        {
            var foodDays = _model.FoodTotalDays.ToString("0.#");
            var waterDays = _model.Water != null ? _model.Water.DaysLeft.ToString("0.#") : "-";
            return _loc.T("Jove.SurvivalBrief.Con.Compact", foodDays, waterDays);
        }

        private void PlaceBesideWeather()
        {
            if (_bar == null || _bar.parent == null)
            {
                return;
            }

            var weather = _bar.parent.Q(WeatherPanelName);
            if (weather == null)
            {
                return;
            }

            var parentWidth = _bar.parent.layout.width;
            var weatherX = weather.layout.x;
            var weatherY = weather.layout.y;
            if (float.IsNaN(parentWidth) || parentWidth < 2f || float.IsNaN(weatherX) || float.IsNaN(weatherY))
            {
                return;
            }

            // Anchor the right edge to the weather bar so a long label grows left and cannot cover it.
            var right = Mathf.Max(0f, parentWidth - weatherX + 4f);
            if (!float.IsNaN(_placedRight)
                && Mathf.Abs(_placedRight - right) < 0.5f
                && Mathf.Abs(_placedTop - weatherY) < 0.5f)
            {
                return;
            }

            _placedRight = right;
            _placedTop = weatherY;
            _bar.style.left = StyleKeyword.Auto;
            _bar.style.width = StyleKeyword.Auto;
            _bar.style.right = right;
            _bar.style.top = weatherY;
        }
    }
}
