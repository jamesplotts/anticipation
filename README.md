# Anticipation

A Railroader mod for the 0.6 "Maintenance of Way" built-in mod loader.

Send a car through an interchange to be loaded, at an off-map coal mine or oil terminal for example, and it leaves the map until the railroad brings it back. Anticipation keeps track of those cars for you.

- **Click the hourglass** in the top-right button row (next to the Switch List) to open the window. Click it again to close.
- The window lists every car that's off the railroad, grouped by the interchange it will come back to. For each car it shows:
  - what it went for, such as "Coal from Sylva Mine";
  - when it's expected back: the day, the time, and how long until then. The time is the first interchange service after the car is ready, including any extra service you've scheduled;
  - a "held over" note if the car missed its service because the interchange was full. It's then expected at the next service.
- Equipment you've bought and are waiting for is listed too, with the price you paid.
- The list updates by itself as cars leave and come back, and the countdowns tick down as the clock runs.

## Install

1. In Railroader, turn on **Preferences > Features > Experimental > Allow Unsafe DLL Mods**, then restart the game.
2. Unzip `Anticipation_<version>.zip` into `Documents\Railroader\Mods\`. You should end up with `Mods\Anticipation\` holding `Info.json` and `Anticipation.dll`.
3. On the main menu, open **Mods** and enable Anticipation.

## Building

Copy `Paths.user.example` to `Paths.user` and set `GameDir` (and `ModDevDir` for test builds). Then:

- `dotnet build Anticipation/Anticipation.csproj` builds and copies the mod into `Documents/Railroader/ModDev/Anticipation`.
- `dotnet build Anticipation/Anticipation.csproj -c Release` writes `bin/Anticipation_<version>.zip`.

MIT licensed.
