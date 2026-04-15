# Counter Spy plugin for Umbra — Nightblossom fork

Adds world markers to other players that are currently targeting you, plus
a **history of the last 10 players** who targeted you (persisted across sessions).

Fork of [una-xiv/Umbra.CounterSpyPlugin](https://github.com/una-xiv/Umbra.CounterSpyPlugin).

## What's new in this fork

- **Target history**: the widget popup has a new **Recent** section listing the
  last 10 players who targeted you, with how long ago. Persisted to disk, so it
  survives Umbra reloads and game restarts.
- **Always-visible widget**: the toolbar widget stays visible even when nobody
  is targeting you — it just shows `0`. You can re-enable the old
  "hide when empty" behaviour in the widget's settings.

NPC targets are intentionally excluded from the history.

## How to Install

1. Open Umbra Settings
2. Navigate to Plugins
3. Enter `nightblossom-ffxiv` as repo owner and `Umbra.CounterSpyPlugin` as repo name, click install.

## Enable the World Markers

Once the plugin has been enabled in Umbra, a world marker type named **Counter Spy Markers** should show up in your World Markers list. Click on it and tick the "Show markers of this type in the Game World" checkbox.

## Where is the history stored?

`%APPDATA%\XIVLauncher\pluginConfigs\Umbra.CounterSpy\history.json`

Delete the file to reset the history.
