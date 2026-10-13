# WebUI Doctor settings

Settings live in `BepInEx\config\com.dev.survivallog.webuidoctor.cfg`, created the first time the game starts with the mod. Edit it while the game is closed; changes apply at the next game start. Delete the file to get the defaults back.

## Recovery

| Setting | Default | What it does |
| --- | --- | --- |
| `EnableHardRestart` | `true` | When the game gives up recovering its UI, close its Chromium processes and make it try again instead of showing the error. |

With `false`, the mod logs that the game gave up and lets the game's error dialog show. The UI repair after the game's own recoveries, the clock check and both hotkeys keep working. The single browser restart that brings back the settings other UI mods need also still happens, whatever this setting says.

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `LogChromiumExits` | `true` | Log every Chromium process start and exit, with its exit code, to `BepInEx\webui-doctor.log`. |

The processes are watched from a background thread twice a second. Turning this off only removes those log lines; recovery works the same.
