using System.Collections.Generic;
using System.Linq;
using Timberborn.Beavers;
using Timberborn.BlueprintSystem;
using Timberborn.ConstructionSites;
using Timberborn.EntityNaming;
using Timberborn.EntitySystem;
using Timberborn.Gathering;
using Timberborn.Goods;
using Timberborn.GoodsUI;
using Timberborn.InventorySystem;
using Timberborn.Localization;
using Timberborn.NeedSpecs;
using Timberborn.NaturalResourcesLifecycle;
using Timberborn.Population;
using Timberborn.ResourceCountingSystemUI;
using Timberborn.SingletonSystem;
using Timberborn.Workshops;
using Timberborn.Yielding;
using UnityEngine;

namespace Jove.SurvivalBrief
{
    public class SupplyLine
    {
        public string GoodId;
        public string Name;
        public Sprite Icon;
        public float PointsPerUnit;
        public float UnitsPerBeaverPerDay;
        public float HoursPerUnit;
        public float TotalPerDay;
        public int Stock;
        public int ProducerBuildings;
        public float ProducedPerDay;
        public float DaysLeft;
    }

    public class ConstructionNeed
    {
        public string GoodId;
        public string GoodName;
        public int Remaining;
        public int Sites;
    }

    public class BuildingDraw
    {
        public string BuildingName;
        public string GoodId;
        public string GoodName;
        public int Buildings;
        public int Working;
        public float AmountPerDay;
        public float OutputPerDay;
        public string ProductId;
        public int Stock;
    }

    public class ConsumptionModel : ILoadableSingleton, IUpdatableSingleton
    {
        private const string HungerNeedId = "Hunger";
        private const string ThirstNeedId = "Thirst";
        private const string WaterGoodId = "Water";
        private const string FoodGroupId = "Food";
        private const float RefreshIntervalSeconds = 2f;

        private readonly PopulationService _populationService;
        private readonly BeaverPopulation _beaverPopulation;
        private readonly ISpecService _specService;
        private readonly ContextualResourceCountingService _countingService;
        private readonly EntityRegistry _entityRegistry;
        private readonly GoodDescriber _goodDescriber;
        private readonly ILoc _loc;
        private readonly IGoodService _goodService;
        private readonly HashSet<string> _unresolvableGoods = new HashSet<string>();
        private readonly EventBus _eventBus;

        private float _nextRefreshTime;
        private bool _dirty = true;
        private bool _loggedNoBeavers;
        private int _diagManufactories;
        private int _diagWithRecipe;
        private int _diagWorking;
        private int _diagSites;
        private int _diagSiteGoods;
        private int _lastDiag = int.MinValue;
        private int _lastFingerprint = int.MinValue;

        public int Adults { get; private set; }

        public int Children { get; private set; }

        public int Eaters { get; private set; }

        public float HungerPerDay { get; private set; }

        public float ThirstPerDay { get; private set; }

        public List<SupplyLine> Foods { get; } = new List<SupplyLine>();

        public float FoodTotalPerDay { get; private set; }

        public float FoodTotalDays { get; private set; }

        public int FoodTotalStock { get; private set; }

        public SupplyLine Water { get; private set; }

        public List<BuildingDraw> BuildingDraws { get; } = new List<BuildingDraw>();

        public List<ConstructionNeed> ConstructionNeeds { get; } = new List<ConstructionNeed>();

        public ConsumptionModel(
            PopulationService populationService,
            BeaverPopulation beaverPopulation,
            ISpecService specService,
            ContextualResourceCountingService countingService,
            EntityRegistry entityRegistry,
            GoodDescriber goodDescriber,
            IGoodService goodService,
            ILoc loc,
            EventBus eventBus)
        {
            _populationService = populationService;
            _beaverPopulation = beaverPopulation;
            _specService = specService;
            _countingService = countingService;
            _entityRegistry = entityRegistry;
            _goodDescriber = goodDescriber;
            _goodService = goodService;
            _loc = loc;
            _eventBus = eventBus;
        }

        public void Load()
        {
            _eventBus.Register(this);
            Refresh();
        }

        [OnEvent]
        public void OnPopulationChanged(PopulationChangedEvent populationChangedEvent)
        {
            Refresh();
        }

        public void UpdateSingleton()
        {
            if (Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            Refresh();
        }

        public bool ConsumeDirty()
        {
            var dirty = _dirty;
            _dirty = false;
            return dirty;
        }

        private void Refresh()
        {
            try
            {
                var districtData = _populationService?.DistrictPopulationData;
                var districtAdults = districtData?.NumberOfAdults ?? 0;
                var districtChildren = districtData?.NumberOfChildren ?? 0;

                if (districtAdults + districtChildren > 0)
                {
                    Adults = districtAdults;
                    Children = districtChildren;
                }
                else
                {
                    Adults = SafeCount(() => _beaverPopulation.NumberOfAdults);
                    Children = SafeCount(() => _beaverPopulation.NumberOfChildren);
                }

                Eaters = Adults + Children;
                if (Eaters == 0 && !_loggedNoBeavers)
                {
                    _loggedNoBeavers = true;
                    Debug.LogWarning(string.Format(
                        "[SurvivalBrief] no beavers detected: district={0}+{1}, global={2}+{3}, districtData={4}",
                        districtAdults, districtChildren,
                        SafeCount(() => _beaverPopulation.NumberOfAdults),
                        SafeCount(() => _beaverPopulation.NumberOfChildren),
                        districtData == null ? "null" : "set"));
                }
                else if (Eaters > 0)
                {
                    _loggedNoBeavers = false;
                }

                HungerPerDay = NeedRate(HungerNeedId);
                ThirstPerDay = NeedRate(ThirstNeedId);

                Foods.Clear();
                foreach (var good in _specService.GetSpecs<GoodSpec>())
                {
                    if (good == null || good.GoodGroupId != FoodGroupId)
                    {
                        continue;
                    }

                    if (!GoodExists(good.Id))
                    {
                        continue;
                    }

                    var line = BuildLine(good, HungerNeedId, HungerPerDay);
                    if (line != null)
                    {
                        Foods.Add(line);
                    }
                }

                Foods.Sort((left, right) => right.UnitsPerBeaverPerDay.CompareTo(left.UnitsPerBeaverPerDay));

                FoodTotalPerDay = Foods.Count == 0 ? 0f : Foods[0].UnitsPerBeaverPerDay * Mathf.Max(1, Eaters);
                FoodTotalStock = 0;
                foreach (var food in Foods)
                {
                    FoodTotalStock += food.Stock;
                }

                FoodTotalDays = FoodTotalPerDay > 0f ? FoodTotalStock / FoodTotalPerDay : 0f;

                var waterSpec = _specService.GetSpecs<GoodSpec>().FirstOrDefault(g => g != null && g.Id == WaterGoodId);
                Water = waterSpec == null ? null : BuildLine(waterSpec, ThirstNeedId, ThirstPerDay);
                ApplyOutput();

                _diagManufactories = 0;
                _diagWithRecipe = 0;
                _diagWorking = 0;
                _diagSites = 0;
                _diagSiteGoods = 0;

                BuildBuildingDraws();
                BuildConstructionNeeds();

                var diag = _diagManufactories * 1000000 + _diagWithRecipe * 10000 + _diagWorking * 100 + Mathf.Min(99, ConstructionNeeds.Count);
                if (diag != _lastDiag)
                {
                    _lastDiag = diag;
                    Debug.Log(string.Format(
                        "[SurvivalBrief] scan: entities={0} mfg={1} mfgWithRecipe={2} working={3} draws={4} sites={5} needs={6}",
                        _entityRegistry.Entities.Count,
                        _diagManufactories, _diagWithRecipe, _diagWorking,
                        BuildingDraws.Count, _diagSites, ConstructionNeeds.Count));
                }

                var fingerprint = BuildFingerprint();
                if (fingerprint != _lastFingerprint)
                {
                    _lastFingerprint = fingerprint;
                    _dirty = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SurvivalBrief] consumption refresh failed: " + e);
            }
        }

        private float NeedRate(string needId)
        {
            foreach (var need in _specService.GetSpecs<NeedSpec>())
            {
                if (need != null && need.Id == needId)
                {
                    return Mathf.Max(0f, -need.DailyDelta);
                }
            }

            return 0f;
        }

        private SupplyLine BuildLine(GoodSpec good, string needId, float ratePerDay)
        {
            var points = 0f;
            foreach (var effect in good.ConsumptionEffects)
            {
                if (effect != null && effect.NeedId == needId)
                {
                    points += effect.Points;
                }
            }

            if (points <= 0f || ratePerDay <= 0f)
            {
                return null;
            }

            var unitsPerBeaverPerDay = ratePerDay / points;
            var stock = Stock(good.Id);

            return new SupplyLine
            {
                GoodId = good.Id,
                Name = Describe(good.Id),
                Icon = Icon(good.Id),
                PointsPerUnit = points,
                UnitsPerBeaverPerDay = unitsPerBeaverPerDay,
                HoursPerUnit = 24f / unitsPerBeaverPerDay,
                TotalPerDay = unitsPerBeaverPerDay * Mathf.Max(1, Eaters),
                Stock = stock,
                DaysLeft = unitsPerBeaverPerDay > 0f && Eaters > 0
                    ? stock / (unitsPerBeaverPerDay * Eaters)
                    : 0f,
            };
        }

        private void ApplyOutput()
        {
            var output = new Dictionary<string, OutputLine>();
            foreach (var entity in _entityRegistry.Entities)
            {
                if (entity == null || entity.RegisteredComponents == null)
                {
                    continue;
                }

                Manufactory manufactory = null;
                Workshop workshop = null;
                GathererFlag gathererFlag = null;
                Gatherable gatherable = null;
                LivingNaturalResource living = null;
                foreach (var component in entity.RegisteredComponents)
                {
                    if (component is Manufactory foundManufactory)
                    {
                        manufactory = foundManufactory;
                    }
                    else if (component is Workshop foundWorkshop)
                    {
                        workshop = foundWorkshop;
                    }
                    else if (component is GathererFlag foundFlag)
                    {
                        gathererFlag = foundFlag;
                    }
                    else if (component is Gatherable foundGatherable)
                    {
                        gatherable = foundGatherable;
                    }
                    else if (component is LivingNaturalResource foundLiving)
                    {
                        living = foundLiving;
                    }
                }

                if (manufactory != null)
                {
                    AddManufactoryOutput(output, manufactory, workshop);
                }

                if (gathererFlag != null)
                {
                    AddGathererBuildings(output, gathererFlag);
                }

                if (gatherable != null && (living == null || !living.IsDead))
                {
                    AddGatherableOutput(output, gatherable);
                }
            }

            foreach (var food in Foods)
            {
                ApplyOutputLine(food, output);
            }

            if (Water != null)
            {
                ApplyOutputLine(Water, output);
            }
        }

        private static void AddManufactoryOutput(Dictionary<string, OutputLine> output, Manufactory manufactory, Workshop workshop)
        {
            var recipe = manufactory.CurrentRecipe;
            if (recipe == null && manufactory.ProductionRecipes != null && manufactory.ProductionRecipes.Length > 0)
            {
                recipe = manufactory.ProductionRecipes[0];
            }

            if (recipe == null || recipe.CycleDurationInHours <= 0f || recipe.Products.IsDefaultOrEmpty)
            {
                return;
            }

            var working = workshop == null || workshop.CurrentlyWorking;
            foreach (var product in recipe.Products)
            {
                if (product == null || string.IsNullOrEmpty(product.Id) || product.Amount <= 0)
                {
                    continue;
                }

                var line = GetOutputLine(output, product.Id);
                line.Buildings++;
                if (working)
                {
                    line.PerDay += product.Amount * 24f / recipe.CycleDurationInHours;
                }
            }
        }

        private static void AddGathererBuildings(Dictionary<string, OutputLine> output, GathererFlag gathererFlag)
        {
            if (gathererFlag.AllowedGatherables.IsDefaultOrEmpty)
            {
                return;
            }

            var seen = new HashSet<string>();
            foreach (var gatherable in gathererFlag.AllowedGatherables)
            {
                var goodId = gatherable?.Yielder?.Yield?.Id;
                if (string.IsNullOrEmpty(goodId) || !seen.Add(goodId))
                {
                    continue;
                }

                GetOutputLine(output, goodId).Buildings++;
            }
        }

        private static void AddGatherableOutput(Dictionary<string, OutputLine> output, Gatherable gatherable)
        {
            var yield = gatherable.YielderSpec?.Yield;
            var days = gatherable.YieldGrowthTimeInDays;
            if (yield == null || string.IsNullOrEmpty(yield.Id) || yield.Amount <= 0 || days <= 0f)
            {
                return;
            }

            GetOutputLine(output, yield.Id).PerDay += yield.Amount / days;
        }

        private static OutputLine GetOutputLine(Dictionary<string, OutputLine> output, string goodId)
        {
            if (!output.TryGetValue(goodId, out var line))
            {
                line = new OutputLine();
                output[goodId] = line;
            }

            return line;
        }

        private static void ApplyOutputLine(SupplyLine supply, Dictionary<string, OutputLine> output)
        {
            if (!output.TryGetValue(supply.GoodId, out var line))
            {
                supply.ProducerBuildings = 0;
                supply.ProducedPerDay = 0f;
                return;
            }

            supply.ProducerBuildings = line.Buildings;
            supply.ProducedPerDay = line.PerDay;
        }

        private class OutputLine
        {
            public int Buildings;
            public float PerDay;
        }

        private static int SafeCount(System.Func<int> read)
        {
            try
            {
                return read();
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        private bool GoodExists(string goodId)
        {
            try
            {
                return _goodService.HasGood(goodId);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private string Describe(string goodId)
        {
            if (_unresolvableGoods.Contains(goodId))
            {
                return goodId;
            }

            try
            {
                var name = _goodDescriber.Describe(goodId);
                return string.IsNullOrEmpty(name) ? goodId : name;
            }
            catch (System.Exception)
            {
                _unresolvableGoods.Add(goodId);
                Debug.LogWarning("[SurvivalBrief] cannot describe good: " + goodId);
                return goodId;
            }
        }

        private Sprite Icon(string goodId)
        {
            try
            {
                return _goodDescriber.GetIcon(goodId);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private int Stock(string goodId)
        {
            try
            {
                return _countingService.GetContextualResourceCount(goodId).AvailableStock;
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        private void BuildBuildingDraws()
        {
            BuildingDraws.Clear();

            var perGood = new Dictionary<string, BuildingDraw>();

            foreach (var entity in _entityRegistry.Entities)
            {
                if (entity == null || entity.RegisteredComponents == null)
                {
                    continue;
                }

                Manufactory manufactory = null;
                Workshop workshop = null;
                LabeledEntity labeled = null;
                NamedEntity named = null;

                foreach (var component in entity.RegisteredComponents)
                {
                    if (component is Manufactory foundManufactory)
                    {
                        manufactory = foundManufactory;
                    }
                    else if (component is Workshop foundWorkshop)
                    {
                        workshop = foundWorkshop;
                    }
                    else if (component is LabeledEntity foundLabeled)
                    {
                        labeled = foundLabeled;
                    }
                    else if (component is NamedEntity foundNamed)
                    {
                        named = foundNamed;
                    }
                }

                if (manufactory == null)
                {
                    continue;
                }

                var recipe = manufactory.CurrentRecipe;
                if (recipe == null && manufactory.ProductionRecipes != null && manufactory.ProductionRecipes.Length > 0)
                {
                    recipe = manufactory.ProductionRecipes[0];
                }

                _diagManufactories++;

                if (recipe == null || recipe.CycleDurationInHours <= 0f)
                {
                    continue;
                }

                _diagWithRecipe++;

                var working = workshop == null || workshop.CurrentlyWorking;
                if (working)
                {
                    _diagWorking++;
                }

                var buildingName = BuildingName(labeled, named, recipe);
                var outputPerDay = 0f;
                var productId = "";
                if (!recipe.Products.IsDefaultOrEmpty)
                {
                    foreach (var product in recipe.Products)
                    {
                        if (product == null || string.IsNullOrEmpty(product.Id) || product.Amount <= 0)
                        {
                            continue;
                        }

                        if (productId.Length == 0)
                        {
                            productId = product.Id;
                        }

                        outputPerDay += product.Amount * 24f / recipe.CycleDurationInHours;
                    }
                }

                if (!working)
                {
                    outputPerDay = 0f;
                }

                if (!recipe.ConsumesIngredients && !recipe.ConsumesFuel)
                {
                    continue;
                }

                if (recipe.ConsumesFuel && recipe.CycleDurationInHours > 0f)
                {
                    var fuelId = recipe.Fuel;
                    var cyclesPerFuel = Mathf.Max(1f, recipe.CyclesFuelLasts);
                    if (!string.IsNullOrEmpty(fuelId))
                    {
                        AddDraw(perGood, buildingName, fuelId, 24f / (recipe.CycleDurationInHours * cyclesPerFuel), working, outputPerDay, productId);
                    }
                }

                foreach (var ingredient in recipe.Ingredients)
                {
                    if (ingredient == null || string.IsNullOrEmpty(ingredient.Id) || ingredient.Amount <= 0)
                    {
                        continue;
                    }

                    AddDraw(perGood, buildingName, ingredient.Id, ingredient.Amount * 24f / recipe.CycleDurationInHours, working, outputPerDay, productId);
                }
            }

            BuildingDraws.AddRange(perGood.Values);
            BuildingDraws.Sort((left, right) => right.AmountPerDay.CompareTo(left.AmountPerDay));
        }

        private string BuildingName(LabeledEntity labeled, NamedEntity named, RecipeSpec recipe)
        {
            if (labeled != null && !string.IsNullOrEmpty(labeled.DisplayName))
            {
                return labeled.DisplayName;
            }

            if (named != null && !string.IsNullOrEmpty(named.EntityName))
            {
                return named.EntityName;
            }

            if (!string.IsNullOrEmpty(recipe.DisplayLocKey))
            {
                var name = _loc.T(recipe.DisplayLocKey);
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            return recipe.Id;
        }

        private void AddDraw(
            Dictionary<string, BuildingDraw> perGood,
            string buildingName,
            string goodId,
            float amountPerDay,
            bool working,
            float outputPerDay,
            string productId)
        {
            var key = buildingName + "|" + goodId;
            if (!perGood.TryGetValue(key, out var draw))
            {
                draw = new BuildingDraw
                {
                    BuildingName = buildingName,
                    GoodId = goodId,
                    GoodName = Describe(goodId),
                    ProductId = productId ?? "",
                    Stock = Stock(string.IsNullOrEmpty(productId) ? goodId : productId),
                };
                perGood[key] = draw;
            }

            draw.Buildings++;
            if (working)
            {
                draw.Working++;
            }

            draw.AmountPerDay += amountPerDay;
            draw.OutputPerDay += outputPerDay;
        }

        private void BuildConstructionNeeds()
        {
            ConstructionNeeds.Clear();
            var perGood = new Dictionary<string, ConstructionNeed>();

            foreach (var entity in _entityRegistry.Entities)
            {
                if (entity == null || entity.RegisteredComponents == null)
                {
                    continue;
                }

                ConstructionSite site = null;
                foreach (var component in entity.RegisteredComponents)
                {
                    if (component is ConstructionSite constructionSite)
                    {
                        site = constructionSite;
                        break;
                    }
                }

                if (site == null)
                {
                    continue;
                }

                _diagSites++;

                var inventory = site.Inventory;
                if (inventory == null || inventory.Capacity <= 0)
                {
                    continue;
                }

                var current = new Dictionary<string, int>();
                foreach (var stock in inventory.Stock)
                {
                    if (!string.IsNullOrEmpty(stock.GoodId))
                    {
                        current[stock.GoodId] = current.TryGetValue(stock.GoodId, out var have) ? have + stock.Amount : stock.Amount;
                    }
                }

                foreach (var allowed in inventory.AllowedGoods)
                {
                    if (allowed.StorableGood == null)
                    {
                        continue;
                    }

                    var goodId = allowed.StorableGood.GoodId;
                    if (string.IsNullOrEmpty(goodId))
                    {
                        continue;
                    }

                    current.TryGetValue(goodId, out var have);
                    var remaining = allowed.Amount - have;
                    if (remaining <= 0)
                    {
                        continue;
                    }

                    if (!perGood.TryGetValue(goodId, out var need))
                    {
                        need = new ConstructionNeed
                        {
                            GoodId = goodId,
                            GoodName = Describe(goodId),
                        };
                        perGood[goodId] = need;
                    }

                    _diagSiteGoods++;
                    need.Remaining += remaining;
                    need.Sites++;
                }
            }

            ConstructionNeeds.AddRange(perGood.Values);
            ConstructionNeeds.Sort((left, right) => right.Remaining.CompareTo(left.Remaining));
        }

        private int BuildFingerprint()
        {
            var fingerprint = Eaters * 397;
            fingerprint = unchecked(fingerprint * 31 + Mathf.RoundToInt(HungerPerDay * 100f));
            fingerprint = unchecked(fingerprint * 31 + Mathf.RoundToInt(ThirstPerDay * 100f));

            foreach (var food in Foods)
            {
                fingerprint = unchecked(fingerprint * 31 + food.GoodId.GetHashCode());
                fingerprint = unchecked(fingerprint * 31 + food.Stock);
            }

            if (Water != null)
            {
                fingerprint = unchecked(fingerprint * 31 + Water.Stock);
            }

            fingerprint = unchecked(fingerprint * 31 + FoodTotalStock);

            foreach (var draw in BuildingDraws)
            {
                fingerprint = unchecked(fingerprint * 31 + draw.GoodId.GetHashCode());
                fingerprint = unchecked(fingerprint * 31 + draw.Buildings);
            }

            foreach (var need in ConstructionNeeds)
            {
                fingerprint = unchecked(fingerprint * 31 + need.GoodId.GetHashCode());
                fingerprint = unchecked(fingerprint * 31 + need.Remaining);
                fingerprint = unchecked(fingerprint * 31 + need.Sites);
            }

            return fingerprint;
        }
    }
}
