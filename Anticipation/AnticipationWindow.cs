using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Game;
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
            List<Away> all = OffRailroad.All();
            if (all == null)
            {
                builder.AddLabelEmptyState(OffRailroad.Problem ?? "The railroad isn't loaded.");
                return;
            }
            if (all.Count == 0)
            {
                builder.AddLabelEmptyState("No cars are off the railroad. Cars sent through an interchange to be loaded, and equipment you've bought, show up here until they're back.");
                return;
            }
            builder.VScrollView(scroll =>
            {
                foreach (IGrouping<string, Away> byInterchange in all.GroupBy(a => a.Interchange?.DisplayName ?? "Off the railroad"))
                {
                    Away first = byInterchange.First();
                    string heading = first.Interchange == null ? byInterchange.Key
                        : $"{byInterchange.Key}  ({byInterchange.Sum(a => CarCount(a)).Pluralize("car")})";
                    scroll.AddSection(heading, section =>
                    {
                        foreach (Away away in byInterchange)
                        {
                            Away row = away;
                            section.AddField(Names(row), () => When(row), UIPanelBuilder.Frequency.Periodic);
                        }
                    });
                }
            });
        }

        // Tenders ride with their engine; name the engine only, as the game's own notices do.
        private static IEnumerable<Car> Shown(Away away) => away.Cars.Where(c => !c.CarType.IsTender);

        private static int CarCount(Away away) => Math.Max(1, Shown(away).Count());

        private static string Names(Away away)
        {
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
            return $"{away.What}\nBack {when}{held}";
        }

        private static string Day(GameDateTime time, GameDateTime now)
        {
            int days = time.Day - now.Day;
            return days == 0 ? "today" : days == 1 ? "tomorrow" : time.DayString();
        }
    }
}
