using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Game;
using KeyValue.Runtime;
using Model;
using UI.Builder;
using UI.Windows;
using UnityEngine;

namespace Anticipation
{
    // The window: cars off the railroad, grouped by the interchange they'll come back to.
    internal sealed class AnticipationWindow : BuilderWindowBase
    {
        private static AnticipationWindow _shared;

        public override string Identifier => "jamesplotts.anticipation";

        protected override string Title => "Anticipation";

        public override WindowConfig Config => WindowConfig.Resizable(new Vector2Int(480, 380), new Vector2Int(340, 160), Window.Position.UpperRight);

        public static void Toggle()
        {
            // The game destroys mod windows when a map unloads; make a new one when that's happened.
            if (_shared == null)
            {
                WindowManager manager = WindowManager.Shared;
                if (manager == null)
                    return;
                _shared = manager.CreateWindowWithComponent<AnticipationWindow>();
            }
            if (_shared.Window.IsShown)
                _shared.Window.CloseWindow();
            else
                _shared.Show();
        }

        public static void CloseIfOpen()
        {
            if (_shared != null && _shared.Window.IsShown)
                _shared.Window.CloseWindow();
        }

        protected override void BuildContent(UIPanelBuilder builder)
        {
            foreach (IDisposable observer in OffRailroad.ObserveAll(builder.Rebuild))
                builder.AddObserver(observer);
            IKeyValueObject store = OffRailroad.GameStore;
            if (store != null)
                builder.RebuildOnPropertyChange(store, OffRailroad.LoadBrokerKey);
            List<Away> all = OffRailroad.All();
            if (all == null)
            {
                builder.AddLabelEmptyState(OffRailroad.Problem ?? "The railroad isn't loaded.");
                return;
            }
            if (all.Count == 0)
            {
                builder.AddLabelEmptyState("No cars are off the railroad. Cars sent through an interchange to be loaded, equipment you've bought, and Load Broker orders show up here until they're on the railroad.");
                return;
            }
            builder.ButtonStrip(strip =>
            {
                strip.AddButtonSelectable("Log", !TableView, () => { TableView = false; builder.Rebuild(); });
                strip.AddButtonSelectable("Table", TableView, () => { TableView = true; builder.Rebuild(); });
            });
            builder.VScrollView(scroll =>
            {
                foreach (IGrouping<string, Away> byInterchange in all.GroupBy(a => a.Interchange?.DisplayName ?? "Off the railroad"))
                {
                    Away first = byInterchange.First();
                    string heading = first.Interchange == null ? byInterchange.Key
                        : $"{byInterchange.Key}  ({byInterchange.Sum(a => CarCount(a)).Pluralize("car")})";
                    scroll.AddSection(heading, section =>
                    {
                        if (TableView)
                        {
                            Table(section, byInterchange, builder);
                            return;
                        }
                        foreach (Away away in byInterchange)
                        {
                            Away row = away;
                            section.AddField(Names(row), () => When(row), UIPanelBuilder.Frequency.Periodic);
                        }
                    });
                }
            });
        }

        // Which view the window shows; remembered between games.
        private const string ViewPref = "jamesplotts.anticipation.view";
        private static bool? _tableView;

        private static bool TableView
        {
            get
            {
                if (!_tableView.HasValue)
                {
                    try { _tableView = PlayerPrefs.GetInt(ViewPref, 1) == 1; }
                    catch (Exception) { _tableView = true; }
                }
                return _tableView.Value;
            }
            set
            {
                _tableView = value;
                try { PlayerPrefs.SetInt(ViewPref, value ? 1 : 0); }
                catch (Exception) { }
            }
        }

        private const float CarColumn = 110f, TimeColumn = 150f;

        // Which column the table is sorted by, and which way. Click a heading to sort by it; click
        // it again to reverse.
        private enum SortBy { Car, Arrival, Description }
        private static SortBy _sortBy = SortBy.Arrival;
        private static bool _descending;

        private static string Heading(string text, SortBy column) =>
            _sortBy == column ? $"<b>{text} {(_descending ? "\u25BC" : "\u25B2")}</b>" : $"<b>{text}</b>";

        private static void SortOn(SortBy column, UIPanelBuilder window)
        {
            if (_sortBy == column)
                _descending = !_descending;
            else
            {
                _sortBy = column;
                _descending = false;
            }
            window.Rebuild();
        }

        // One row per car: its number, when it's due on the railroad, and what it went for.
        private static void Table(UIPanelBuilder section, IEnumerable<Away> group, UIPanelBuilder window)
        {
            section.HStack(row =>
            {
                row.AddButton(Heading("Car #", SortBy.Car), () => SortOn(SortBy.Car, window)).Width(CarColumn);
                row.AddButton(Heading("Arrival Time", SortBy.Arrival), () => SortOn(SortBy.Arrival, window)).Width(TimeColumn);
                row.AddButton(Heading("Description", SortBy.Description), () => SortOn(SortBy.Description, window)).RectTransform.FlexibleWidth();
            });
            var rows = new List<(string car, Away away)>();
            foreach (Away away in group)
            {
                List<string> cars = away.OnOrder ? new List<string> { "On order" } : Shown(away).Select(c => c.DisplayName).ToList();
                if (cars.Count == 0)
                    cars = away.Cars.Select(c => c.DisplayName).ToList();
                rows.AddRange(cars.Select(c => (c, away)));
            }
            IOrderedEnumerable<(string car, Away away)> sorted;
            switch (_sortBy)
            {
                case SortBy.Car:
                    sorted = _descending ? rows.OrderByDescending(r => r.car, CarNumberOrder.Instance) : rows.OrderBy(r => r.car, CarNumberOrder.Instance);
                    break;
                case SortBy.Description:
                    sorted = _descending ? rows.OrderByDescending(r => r.away.What, StringComparer.OrdinalIgnoreCase) : rows.OrderBy(r => r.away.What, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    sorted = _descending ? rows.OrderByDescending(r => r.away.Expected.TotalSeconds) : rows.OrderBy(r => r.away.Expected.TotalSeconds);
                    break;
            }
            foreach ((string car, Away away) in sorted.ThenBy(r => r.car, CarNumberOrder.Instance))
            {
                Away a = away;
                section.HStack(row =>
                {
                    row.AddLabel(car).Width(CarColumn);
                    row.AddLabel(() => Arrival(a), UIPanelBuilder.Frequency.Periodic).Width(TimeColumn);
                    row.AddLabel(a.What + (a.HeldOver ? " <color=#E0A040>(held over: the interchange was full)</color>" : "")).FlexibleWidth();
                });
            }
        }

        // Car numbers in number order: "SR 99" before "SR 1203", then by railroad mark.
        private sealed class CarNumberOrder : IComparer<string>
        {
            public static readonly CarNumberOrder Instance = new CarNumberOrder();

            public int Compare(string x, string y)
            {
                (string mark, long number) a = Split(x), b = Split(y);
                int c = string.Compare(a.mark, b.mark, StringComparison.OrdinalIgnoreCase);
                if (c != 0)
                    return c;
                c = a.number.CompareTo(b.number);
                return c != 0 ? c : string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            }

            private static (string, long) Split(string s)
            {
                s = s ?? "";
                int i = s.Length;
                while (i > 0 && char.IsDigit(s[i - 1]))
                    i--;
                long n = i < s.Length && long.TryParse(s.Substring(i), out long v) ? v : -1;
                return (s.Substring(0, i).Trim(), n);
            }
        }

        private static string Arrival(Away away)
        {
            GameDateTime now = TimeWeather.Now;
            if (away.Expected <= now)
                return "Any moment";
            string day = Day(away.Expected, now);
            day = char.ToUpper(day[0]) + day.Substring(1);
            return $"{day} {away.Expected.TimeString()}\n<size=85%>in {away.Expected.IntervalString(now, GameDateTimeInterval.Style.Short)}</size>";
        }

        // Tenders ride with their engine; name the engine only, as the game's own notices do.
        private static IEnumerable<Car> Shown(Away away) => away.Cars.Where(c => !c.CarType.IsTender);

        private static int CarCount(Away away) => Math.Max(1, Shown(away).Count());

        private static string Names(Away away)
        {
            if (away.OnOrder)
                return "On order (Load Broker)";
            List<string> names = Shown(away).Select(c => c.DisplayName).ToList();
            if (names.Count == 0)
                names = away.Cars.Select(c => c.DisplayName).ToList();
            return names.Count <= 2 ? string.Join(", ", names) : $"{names[0]} +{names.Count - 1} more";
        }

        private static string When(Away away)
        {
            GameDateTime now = TimeWeather.Now;
            string when = away.Expected <= now ? "any moment"
                : $"{Day(away.Expected, now)} {away.Expected.TimeString()} (in {away.Expected.IntervalString(now, GameDateTimeInterval.Style.Short)})";
            string held = away.HeldOver ? ", <color=#E0A040>held over: the interchange was full</color>" : "";
            return $"{away.What}\n{(away.OnOrder ? "Arrives" : "Back")} {when}{held}";
        }

        private static string Day(GameDateTime time, GameDateTime now)
        {
            int days = time.Day - now.Day;
            return days == 0 ? "today" : days == 1 ? "tomorrow" : time.DayString();
        }
    }
}
