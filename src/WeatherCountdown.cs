using Timberborn.GameCycleSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;
using UnityEngine;

namespace Jove.SurvivalBrief
{
    public enum HazardKind
    {
        None,
        Drought,
        Badtide
    }

    public class WeatherCountdown : ILoadableSingleton, IUpdatableSingleton
    {
        private const string DroughtWeatherId = "DroughtWeather";
        private const string BadtideWeatherId = "BadtideWeather";
        private const float HoursPerDay = 24f;

        private readonly EventBus _eventBus;
        private readonly GameCycleService _gameCycleService;
        private readonly WeatherService _weatherService;
        private readonly HazardousWeatherService _hazardousWeatherService;
        private readonly ILoc _loc;

        private bool _hasState;
        private int _lastCycle = int.MinValue;
        private int _lastDay = int.MinValue;
        private int _lastTick = int.MinValue;

        public HazardKind Kind { get; private set; }

        public bool InHazard { get; private set; }

        public float RemainingHours { get; private set; }

        public float RemainingDays { get; private set; }

        public float SpanDays { get; private set; }

        public string DurationText { get; private set; } = "";

        public string Text { get; private set; } = "";

        public WeatherCountdown(
            EventBus eventBus,
            GameCycleService gameCycleService,
            WeatherService weatherService,
            HazardousWeatherService hazardousWeatherService,
            ILoc loc)
        {
            _eventBus = eventBus;
            _gameCycleService = gameCycleService;
            _weatherService = weatherService;
            _hazardousWeatherService = hazardousWeatherService;
            _loc = loc;
        }

        public void Load()
        {
            _eventBus.Register(this);
            Recompute();
            Debug.Log("[SurvivalBrief] countdown loaded: " + Describe());
        }

        [OnEvent]
        public void OnCycleDayStarted(CycleDayStartedEvent cycleDayStartedEvent)
        {
            Recompute();
            Debug.Log("[SurvivalBrief] day started: " + Describe());
        }

        [OnEvent]
        public void OnHazardousWeatherStarted(HazardousWeatherStartedEvent hazardousWeatherStartedEvent)
        {
            Recompute();
            Debug.Log("[SurvivalBrief] hazard started: " + Describe());
        }

        [OnEvent]
        public void OnHazardousWeatherEnded(HazardousWeatherEndedEvent hazardousWeatherEndedEvent)
        {
            Recompute();
            Debug.Log("[SurvivalBrief] hazard ended: " + Describe());
        }

        public void UpdateSingleton()
        {
            var partialCycleDay = _gameCycleService.PartialCycleDay;
            var ticksPerDay = RemainingHours < 1f ? 1440f : HoursPerDay;
            var tick = Mathf.FloorToInt(partialCycleDay * ticksPerDay);

            if (!_hasState
                || _gameCycleService.Cycle != _lastCycle
                || _gameCycleService.CycleDay != _lastDay
                || tick != _lastTick)
            {
                Recompute();
            }
        }

        private void Recompute()
        {
            _hasState = true;
            _lastCycle = _gameCycleService.Cycle;
            _lastDay = _gameCycleService.CycleDay;
            _lastTick = Mathf.FloorToInt(_gameCycleService.PartialCycleDay * HoursPerDay);

            InHazard = _weatherService.IsHazardousWeather;
            Kind = ResolveKind();

            if (Kind == HazardKind.None)
            {
                RemainingHours = 0f;
                RemainingDays = 0f;
                SpanDays = 0f;
                DurationText = "";
                Text = "";
                return;
            }

            var partialCycleDay = _gameCycleService.PartialCycleDay;
            var startDay = _weatherService.HazardousWeatherStartCycleDay;
            var hazardDuration = _weatherService.HazardousWeatherDuration;

            var remainingDays = InHazard
                ? startDay + hazardDuration - partialCycleDay
                : startDay - partialCycleDay;

            RemainingDays = Mathf.Max(0f, remainingDays);
            SpanDays = InHazard
                ? Mathf.Max(0.01f, hazardDuration)
                : Mathf.Max(0.01f, startDay - 1f);
            RemainingHours = RemainingDays * HoursPerDay;

            if (RemainingHours <= 0f)
            {
                DurationText = "";
                Text = "";
                return;
            }

            DurationText = FormatDuration(RemainingHours);
            Text = _loc.T(LocKey(), DurationText);
        }

        private string FormatDuration(float hours)
        {
            var totalHours = Mathf.FloorToInt(hours);
            var days = totalHours / 24;
            var remainderHours = totalHours % 24;

            if (days > 0 && remainderHours > 0)
            {
                return _loc.T("Jove.SurvivalBrief.Duration.DaysHours", days, remainderHours);
            }

            if (days > 0)
            {
                return _loc.T("Jove.SurvivalBrief.Duration.Days", days);
            }

            if (totalHours > 0)
            {
                return _loc.T("Jove.SurvivalBrief.Duration.Hours", totalHours);
            }

            var minutes = Mathf.Max(1, Mathf.FloorToInt(hours * 60f));
            return _loc.T("Jove.SurvivalBrief.Duration.Minutes", minutes);
        }

        private HazardKind ResolveKind()
        {
            if (_weatherService.HazardousWeatherDuration <= 0)
            {
                return HazardKind.None;
            }

            var weather = _hazardousWeatherService.CurrentCycleHazardousWeather;
            if (weather == null)
            {
                return HazardKind.None;
            }

            var id = weather.Id;
            if (id == DroughtWeatherId)
            {
                return HazardKind.Drought;
            }

            if (id == BadtideWeatherId)
            {
                return HazardKind.Badtide;
            }

            Debug.LogWarning("[SurvivalBrief] unknown hazardous weather id: " + id);
            return HazardKind.None;
        }

        private string LocKey()
        {
            if (Kind == HazardKind.Badtide)
            {
                return InHazard ? "Jove.SurvivalBrief.BadtideLeft" : "Jove.SurvivalBrief.BadtideIn";
            }

            return InHazard ? "Jove.SurvivalBrief.DroughtLeft" : "Jove.SurvivalBrief.DroughtIn";
        }

        private string Describe()
        {
            return string.Format(
                "cycle={0} day={1} partial={2:F3} start={3} len={4} inHazard={5} kind={6} hoursLeft={7:F2} text='{8}'",
                _gameCycleService.Cycle,
                _gameCycleService.CycleDay,
                _gameCycleService.PartialCycleDay,
                _weatherService.HazardousWeatherStartCycleDay,
                _weatherService.CycleLengthInDays,
                InHazard,
                Kind,
                RemainingHours,
                Text);
        }
    }
}
