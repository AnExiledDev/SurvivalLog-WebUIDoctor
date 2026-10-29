# WebUI Doctor settings

Settings live in `BepInEx\config\com.dev.survivallog.webuidoctor.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Delete the file to get the defaults back. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start instead.

## Recovery

| Setting | Default | What it does |
| --- | --- | --- |
| `EnableHardRestart` | `true` | When the game gives up recovering its UI, close its Chromium processes and make it try again instead of showing the error. |

With `false`, the mod logs that the game gave up and lets the game's error dialog show. The UI repair after the game's own recoveries, the clock check and both hotkeys keep working. The single browser restart that brings back the settings other UI mods need also still happens, whatever this setting says.

## Display

| Setting | Default | What it does |
| --- | --- | --- |
| `MenuFrameRate` | `60` | How many times a second the menus and HUD redraw, from 30 to 120. The game uses 30. |

The game's browser takes this when it starts, so a change takes effect the next time you start the game, even on the Mods screen. A higher rate makes menus smoother and quicker to answer the mouse, and costs a little more graphics card time while a menu is open.

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `LogChromiumExits` | `true` | Log every Chromium process start and exit, with its exit code, to `BepInEx\webui-doctor.log`. |

The processes are watched from a background thread twice a second. Turning this off only removes those log lines; recovery works the same.
