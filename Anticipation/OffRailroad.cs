using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Game;
using Game.State;
using KeyValue.Runtime;
using Model;
using Model.Ops;

namespace Anticipation
{
    // One group of cars the game has taken off the map (its "bardo"), and when it comes back.
    internal sealed class Away
    {
        public List<Car> Cars;
        // Shown under the interchange: "Coal at Sylva Mine", "Purchased", ...
        public string What;
        public Interchange Interchange;
        // When the game lets the cars come back; they arrive with the first service at or after it.
        public GameDateTime Due;
        public GameDateTime Expected;
        // Due, and the service that should have brought them has passed (the interchange was full).
        public bool HeldOver;
        // Not a car yet: a Load Broker order, which becomes a car when it comes in.
        public bool OnOrder;
    }

    // Reads the game's return schedules. Each interchange, and each off-map industry served
    // through one (InterchangedIndustryLoader), keeps groups of car ids with a return time.
    // The schedule type is internal to the game, so it's reached by reflection.
    internal static class OffRailroad
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static PropertyInfo _loaderSchedule;
        private static PropertyInfo _interchangeSchedule;
        private static MethodInfo _enumerateGroups;
        private static MethodInfo _observe;
        private static FieldInfo _returnTime;
        private static FieldInfo _carIds;
        private static FieldInfo _paid;
        private static bool _resolved;

        // The reason it couldn't read the schedules, or null.
        public static string Problem { get; private set; }

        private static bool Resolve()
        {
            if (_resolved)
                return Problem == null;
            _resolved = true;
            try
            {
                _loaderSchedule = typeof(InterchangedIndustryLoader).GetProperty("BardoSchedule", Any);
                _interchangeSchedule = typeof(Interchange).GetProperty("BardoSchedule", Any);
                Type schedule = _interchangeSchedule?.PropertyType;
                _enumerateGroups = schedule?.GetMethod("EnumerateGroups", Any);
                _observe = schedule?.GetMethod("Observe", Any);
                Type group = schedule?.GetNestedType("Group", Any);
                _returnTime = group?.GetField("ReturnTime", Any);
                _carIds = group?.GetField("CarIds", Any);
                _paid = group?.GetField("PaidAmount", Any);
                if (_loaderSchedule == null || _enumerateGroups == null || _returnTime == null || _carIds == null)
                    Problem = "This version of Railroader keeps its interchange schedule differently; Anticipation needs an update.";
            }
            catch (Exception ex)
            {
                Problem = "Couldn't read the interchange schedule: " + ex.Message;
            }
            if (Problem != null)
                ModEntry.Log?.Error("Anticipation: " + Problem);
            return Problem == null;
        }

        // Every group off the railroad, soonest back first. Null if the schedules can't be read.
        public static List<Away> All()
        {
            OpsController ops = OpsController.Shared;
            TrainController trains = TrainController.Shared;
            if (!Resolve() || ops == null || trains == null || ops.AllIndustryComponents == null)
                return null;
            GameDateTime now = TimeWeather.Now;
            var all = new List<Away>();
            foreach (IndustryComponent component in ops.AllIndustryComponents)
            {
                object schedule;
                Interchange interchange;
                if (component is InterchangedIndustryLoader loader)
                {
                    schedule = _loaderSchedule.GetValue(loader);
                    interchange = loader.Industry?.GetComponentInChildren<Interchange>();
                }
                else if (component is Interchange ic)
                {
                    schedule = _interchangeSchedule.GetValue(ic);
                    interchange = ic;
                }
                else
                    continue;
                if (schedule == null)
                    continue;
                foreach (object group in (IEnumerable)_enumerateGroups.Invoke(schedule, null))
                {
                    var cars = new List<Car>();
                    foreach (string id in (IEnumerable<string>)_carIds.GetValue(group))
                    {
                        if (trains.TryGetCarForId(id, out Car car))
                            cars.Add(car);
                    }
                    if (cars.Count == 0)
                        continue;
                    var away = new Away
                    {
                        Cars = cars,
                        What = Describe(component, group),
                        Interchange = interchange,
                        Due = (GameDateTime)_returnTime.GetValue(group),
                    };
                    away.Expected = away.Due;
                    if (interchange != null)
                    {
                        away.Expected = interchange.GetServiceTimeAtOrAfter(away.Due);
                        if (away.Expected < now)
                        {
                            away.HeldOver = true;
                            away.Expected = interchange.GetNextServiceTime(now, out _);
                        }
                    }
                    all.Add(away);
                }
            }
            all.AddRange(LoadBrokerOrders(ops, now));
            return all.OrderBy(a => a.Expected.TotalSeconds).ToList();
        }

        private static string Describe(IndustryComponent component, object group)
        {
            if (component is InterchangedIndustryLoader loader)
            {
                string load = loader.load?.description;
                return string.IsNullOrEmpty(load) ? "At " + loader.name : load + " from " + loader.name;
            }
            int paid = _paid == null ? 0 : (int)_paid.GetValue(group);
            return paid > 0 ? $"Purchased, {paid:C0}" : "Off the railroad";
        }

        // Load Broker (jamesplotts/loadbroker), if it's installed, keeps its pending orders in the
        // game's key-value store under this key: one dictionary per carload, with the interchange,
        // load, supplier and the time it can come in. Read it directly; neither mod needs the other.
        public const string LoadBrokerKey = "loadbroker";

        public static IKeyValueObject GameStore
        {
            get
            {
                try { return StateManager.Shared?.Storage?.GameKeyValueObject; }
                catch (Exception) { return null; }
            }
        }

        private static IEnumerable<Away> LoadBrokerOrders(OpsController ops, GameDateTime now)
        {
            IKeyValueObject store = GameStore;
            if (store == null)
                yield break;
            Value saved = store[LoadBrokerKey];
            if (saved.Type != KeyValue.Runtime.ValueType.Array)
                yield break;
            foreach (Value order in saved.ArrayValue)
            {
                string interchangeId = order["interchange"].StringValue;
                Interchange interchange = ops.AllInterchanges?.FirstOrDefault(i => i.Identifier == interchangeId);
                if (!double.TryParse(order["due"].StringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double due))
                    continue;
                string loaderId = order["loader"].StringValue;
                InterchangedIndustryLoader loader = interchange?.Industry?.GetComponentsInChildren<InterchangedIndustryLoader>()
                    .FirstOrDefault(l => l.Identifier == loaderId);
                string load = Model.Ops.Definition.CarLoadLibrary.GetLoad(order["load"].StringValue)?.description ?? order["load"].StringValue;
                string from = loader != null ? " from " + loader.name : "";
                int cost = order["cost"].IntValue;
                var away = new Away
                {
                    Cars = new List<Car>(),
                    OnOrder = true,
                    What = $"{load}{from}, ordered for {cost:C0}",
                    Interchange = interchange,
                    Due = new GameDateTime(due),
                };
                away.Expected = away.Due;
                if (interchange != null)
                {
                    away.Expected = interchange.GetServiceTimeAtOrAfter(away.Due);
                    if (away.Expected < now)
                    {
                        away.HeldOver = true;
                        away.Expected = interchange.GetNextServiceTime(now, out _);
                    }
                }
                yield return away;
            }
        }

        // Calls back whenever any schedule changes (cars sent off, or cars returned).
        public static IEnumerable<IDisposable> ObserveAll(Action changed)
        {
            OpsController ops = OpsController.Shared;
            if (!Resolve() || _observe == null || ops?.AllIndustryComponents == null)
                yield break;
            foreach (IndustryComponent component in ops.AllIndustryComponents)
            {
                object schedule = component is InterchangedIndustryLoader loader ? _loaderSchedule.GetValue(loader)
                    : component is Interchange ic ? _interchangeSchedule.GetValue(ic) : null;
                if (schedule != null && _observe.Invoke(schedule, new object[] { changed }) is IDisposable observer)
                    yield return observer;
            }
        }
    }
}
