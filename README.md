# Anticipation

Source: https://github.com/jamesplotts/anticipation
Download: https://www.nexusmods.com/railroader/mods/2104 (or the GitHub releases)

A Railroader mod for the 0.6 "Maintenance of Way" built-in mod loader.

Send a car through an interchange to be loaded, such as coal from Carolina Fuel Co. or ties from Atlantic Locomotive Works, and it leaves the map until the railroad brings it back. Anticipation keeps track of those cars for you.

- **Click the hourglass** in the top-right button row (next to the Switch List) to open the window. Click it again to close.
- The window lists every car that's off the railroad, grouped by the interchange it will come back to. For each car it shows:
  - what it went for, such as "Coal from Carolina Fuel Co.";
  - when it's expected back: the day, the time, and how long until then. The time is the first interchange service after the car is ready, including any extra service you've scheduled;
  - a "held over" note if the car missed its service because the interchange was full. It's then expected at the next service.
- If you also run [Load Broker](https://www.nexusmods.com/railroader/mods/2376), the carloads you've ordered through it are listed too, as "On order (Load Broker)", with when they'll arrive. Neither mod needs the other.
- Equipment you've bought and are waiting for is listed too, with the price you paid.
- **Log** and **Table** buttons at the top switch views. Table lists one row per car with three columns: Car #, Arrival Time and Description; click a heading to sort by it, again to reverse. Log shows each group of cars with what it went for and when it's back. The window remembers your choice.
- The list updates by itself as cars leave and come back, and the countdowns tick down as the clock runs.

## Known conflict

A player reports that with Anticipation and an East Whittier yard mod (in particular BMR's MOW yard expansion) both installed, the track there flashes in and out, and removing one or the other stops it. The cause isn't known: Anticipation has no Harmony patches and doesn't touch track or map data. Reports with a mod list and Player.log are welcome.

## Install

1. In Railroader, turn on **Preferences > Features > Experimental > Allow Unsafe DLL Mods**, then restart the game.
2. Unzip `Anticipation_<version>.zip` into `Documents\Railroader\Mods\`. You should end up with `Mods\Anticipation\` holding `Info.json` and `Anticipation.dll`.
3. On the main menu, open **Mods** and enable Anticipation.

## Building

Copy `Paths.user.example` to `Paths.user` and set `GameDir` (and `ModDevDir` for test builds). Then:

- `dotnet build Anticipation/Anticipation.csproj` builds and copies the mod into `Documents/Railroader/ModDev/Anticipation`.
- `dotnet build Anticipation/Anticipation.csproj -c Release` writes `bin/Anticipation_<version>.zip`.

MIT licensed.

## Changelog

### 0.2.1 (2026-11-23)
- Click a column heading in the Table view (Car #, Arrival Time or Description) to sort by it; click it again to reverse. An arrow marks the sorted column. Car numbers sort in number order.

### 0.2.0 (2026-11-23)
- New **Table** view: one row per car, in three columns: Car #, Arrival Time (with a countdown) and Description. Still grouped by interchange.
- **Log** and **Table** buttons at the top of the window switch views. The window remembers your choice; Table is the default.
- README notes a reported conflict with East Whittier yard mods (track flashing); the cause isn't known yet.

### 0.1.2 (2026-11-21)
- Carloads ordered with [Load Broker](https://www.nexusmods.com/railroader/mods/2376) are listed too, as "On order (Load Broker)", with what was ordered, from whom, the price and when it arrives. They drop off the list when the car comes in. Load Broker isn't required.

### 0.1.1 (2026-11-16)
- The button shows its own hourglass icon, in the same cream as the game's icons. Before, it showed a copy of the Switch List icon.

### 0.1.0 (2026-11-07)
- First release: a window of the cars off the railroad, grouped by interchange, with what they went for and when they'll be back.
