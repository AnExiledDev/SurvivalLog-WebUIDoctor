# WebUI Doctor settings

Settings live in `BepInEx\config\com.dev.survivallog.webuidoctor.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Delete the file to get the defaults back.

## Recovery

| Setting | Default | What it does |
| --- | --- | --- |
| `EnableHardRestart` | `true` | When the game's menus crash and the game gives up trying to bring them back, restart the browser the menus run in and keep trying, instead of showing the error. |

With `false`, the mod writes in its log that the game gave up, and the game's error dialog shows. The UI repair after the game recovers the UI by itself, the clock check and both hotkeys still work. If other UI mods lose access to the game's pages after a recovery, the mod still restarts the browser once to fix that, whatever this setting says.

## Display

| Setting | Default | What it does |
| --- | --- | --- |
| `MenuFrameRate` | `60` | How many times a second the menus and HUD redraw, from 30 to 120. Without the mod, the game uses 30. |

The game's browser reads this when it starts, so a change takes effect the next time you start the game, even when you change it on the Mods screen. A higher rate makes menus smoother and quicker to react to the mouse, and uses a little more graphics card time while a menu is open.

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `LogChromiumExits` | `true` | Log every Chromium process start and exit, with its exit code, to `BepInEx\webui-doctor.log`. |

Turning this off only removes those log lines. Recovery works the same either way.
