# Changelog

## 0.6.5

- Clearer wording in the settings.
- Made for Survival Log 1.1.20719.

## 0.6.4

- If the game crashes, `BepInEx\webui-doctor.log` now keeps the last lines from before the crash.
- Now needs Exiled's Toolkit 0.20.0 or newer.
- Made for Survival Log 1.1.20719.

## 0.6.3

- Now needs Exiled's Toolkit 0.19.0 or newer, because some of the mod's code moved into it.
- The log file `BepInEx\webui-doctor.log` now moves to `webui-doctor.log.1` once it passes 1 MB, instead of `webui-doctor.old.log`.
- Made for Survival Log 1.1.20719.

## 0.6.2

- Ctrl+F12 and Ctrl+F11 no longer go off while you type in a text box, such as a search box in another mod's window. This needs Exiled's Toolkit 0.12.0 or newer.
- Made for Survival Log 1.1.20151.

## 0.6.1

- Clearer description for the `EnableHardRestart` setting.
- Made for Survival Log 1.1.20151.

## 0.6.0

- Menus and the HUD now redraw 60 times a second instead of 30, so they feel smoother and react to the mouse sooner.
- The new `MenuFrameRate` setting sets how often they redraw, from 30 to 120. A change takes effect the next time you start the game.
- Made for Survival Log 1.1.19611.

## 0.5.1

- After a game update, Mod Manager can tell you if this mod may need an update.
- Made for Survival Log 1.1.19131.

## 0.5.0

- Mod Manager's Mods screen links to this changelog.
- On the Mods screen, the diagnostics settings are in the Advanced group, which starts folded.
- Settings changed on the Mod Manager's Mods screen take effect at once.
- Mod Manager's Mods screen shows the mod's name, description, category and links.
- Exiled's Toolkit 0.4.0 or newer is optional. With it, settings you change on the Mods screen take effect right away. Without it, they take effect the next time you start the game.

## 0.4.1

Changes since 0.3.5:

- The UI comes back when it breaks while the game is paused.
- Clicking objects in the world works again after the UI recovers.
- Other UI mods keep working after the UI recovers.
- The clock on screen stays closer to the game's time.
- The UI recovers faster, and the log file stays smaller.
- The mod no longer breaks the game's crash report.
- Every setting is explained in English and Simplified Chinese.
- The mod waits for the game to finish loading its parts before it reads them.
