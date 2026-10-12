# WebUI Doctor

Keeps Survival Log playable through its "interface display error".

Survival Log draws its menus and HUD with an embedded Chromium browser. When that browser crashes, the game tries to recover a few times, then gives up and leaves you with an error dialog, a dead UI and often a stuck pause. WebUI Doctor takes over at that point: it restarts the browser, keeps trying until the UI is back, and cleans up what the crash left behind.

## Features

- **Hard restart instead of the error**: when the game gives up recovering its UI, the mod closes the game's Chromium processes, skips the "automatic recovery has failed" dialog and makes the game recover again on a fresh browser.
- **Never gives up**: the first retry runs 3 seconds after the game gives up. If the UI fails again, it waits 10 seconds, then 30 seconds, then 1 minute, then every 2 minutes, for as long as it takes. Once the UI has stayed up for 2 minutes, the next error starts from the short wait again.
- **Backup check**: if the game gives up without the mod's hook seeing it, a once-a-second check of the game's own give-up flag catches it. In that case the error dialog may show for a moment before the repair closes it.
- **UI repair after every recovery**: after any recovery, the mod's own or the game's, it gives mouse clicks back to the UI, closes the game's error dialog with the game's own close action, and a second later hides any leftover error panel (found by its text, in English or Chinese).
- **Stuck pause release**: a crash can leave the game paused with no window open. After a repair the mod checks the pause. If you paused with the speed control, it is left alone. If the game says an open window holds the pause, it is left alone too. Otherwise the pause is treated as left over from the crash and the game is resumed.
- **Clicks on the world come back**: clicking an object while the UI was gone could leave every object unclickable afterwards, with no menu to explore, sleep or use anything. After a repair the mod checks whether clicks still reach the world. If the game says an object menu or trap panel is open, it is left alone; otherwise clicks are given back. `Ctrl+F12` also closes an object menu or trap panel that is holding the clicks.
- **Manual repair**: `Ctrl+F12` runs the same repair by hand. It releases a stuck pause even when the game thinks a window is still open (the hidden error dialog itself can count as one), but never a pause you set with the speed control.
- **Keeps other UI mods working**: the game starts its recovered browser without the page-access flags that UI mods rely on. The mod hands those flags back as soon as a crash shows and before each of its own recoveries, so mods that add to the game's pages keep working after a recovery.
- **UI clock check**: once a minute (and 40 seconds after each repair) it compares the clock the UI shows with the game's own time. If they are more than 30 game minutes apart, it asks the game to send the time again; if the UI is still off 3 seconds later, it puts the time on the page itself, and if even that does not take, it logs it and tries again at the next check. It never changes the game's time, only what the UI shows.
- **Page probe**: `Ctrl+F11` checks whether other UI mods can still reach the game's page and writes the result to the log. It also runs once 75 seconds after the game starts and 10 seconds after each repair. It changes nothing.
- **Chromium log**: every Chromium process the game starts and every exit, with its exit code, goes to the log (this can be turned off in the config).
- **Log file**: everything the mod sees and does is written to `BepInEx\webui-doctor.log`, which survives restarts.

WebUI Doctor writes nothing into your save. It closes and restarts the game's own browser processes and resumes a pause it judges stuck, which only affects the running game. You can remove it at any time; the game then goes back to showing its error dialog when its recovery gives up.

## Hotkeys

| Key | What it does |
| --- | --- |
| `Ctrl+F12` | Repairs the UI by hand: gives clicks back (on the UI and on the world), closes the error dialog and releases a stuck pause. |
| `Ctrl+F11` | Checks whether other UI mods can reach the game's page and logs the result. Changes nothing. |

Either Ctrl key works. The keys are fixed and cannot be changed in the config. If the game's input does not support them, they turn themselves off and the log says so.

## How it works

When the game gives up, the mod closes every "Vuplex WebView" process started by the game, then waits (3 seconds, plus the back-off wait after repeated failures). It then resets the game's own recovery counter and asks the game to recover. If the game has not started recovering 5 seconds later, the mod starts the recovery itself. Two seconds after a recovery finishes, the repair runs. With the Chromium log on, the processes it closes show up as "killed by WebUIDoctor", so they are easy to tell apart from real crashes.

The page probe looks for these UI mods, if you have them: Project Cook, Better Trade and Item Totals. None of them is needed.

## Known issues and limits

- The mod cannot stop the browser from crashing. It only gets the UI back afterwards, and the UI is gone for a few seconds (longer after repeated failures) while it does.
- Releasing a stuck pause resumes the game. If a window really was holding the pause and the game did not know it, `Ctrl+F12` will still resume. After an automatic recovery the mod is more careful and leaves a pause the game says a window holds.
- With the backup check (when the hook did not fire), the error dialog can flash up before the repair closes it.
- The error panel is found by its text in English or Chinese. In another language, a leftover panel may stay until you close it yourself.
- The UI clock is only checked to within 30 game minutes, because the UI rounds its clock down to 10 minutes.
- `BepInEx\webui-doctor.log` is never trimmed and grows for as long as you play with the mod. Delete it while the game is closed if it gets large.

If the UI does not come back, a pause gets released when it should not, or anything else looks wrong, please tell me (see [Feedback](#feedback-and-bug-reports)).

## Requirements

- Survival Log 1.1.18317. Built and tested on this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- Windows. The mod finds and closes the game's browser processes through Windows itself.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Download `WebUIDoctor.dll` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-WebUIDoctor/releases/latest) and put it in the `BepInEx\plugins` folder inside the game folder (the folder with the game .exe). Full path example: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\WebUIDoctor.dll`

## Uninstall

Delete `WebUIDoctor.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.webuidoctor.cfg` and `BepInEx\webui-doctor.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.webuidoctor.cfg`, created the first time the game starts with the mod. Edit it while the game is closed; changes apply at the next game start. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Tested on Survival Log 1.1.18317 with BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for a line starting `===== WebUI Doctor 0.3.7 started`. It goes on to show the game's process id, whether the hard restart is on, and the retry waits. The same line, plus everything the mod does after it (browser starts and exits, recoveries, repairs, pause checks, click checks, clock checks and page probes), is in `BepInEx\webui-doctor.log`, which survives restarts.

If the error dialog still shows, look in `BepInEx\webui-doctor.log` for "game gave up recovering the UI". If `EnableHardRestart` is off, the line says so.

## Changelog

- 0.3.7: After the UI comes back, clicking objects opens their menu again. A click made while the UI was gone could leave every object unclickable, so you could not explore or sleep. The repair after a recovery now fixes that, and so does `Ctrl+F12`.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also leave a comment on the Nexus page or [open an issue](https://github.com/AnExiledDev/SurvivalLog-WebUIDoctor/issues). When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\webui-doctor.log`.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/WebUIDoctor -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. The output DLL is at `src\WebUIDoctor\bin\Release\WebUIDoctor.dll`.

The unit tests cover the pure rules (retry timing, pause release, world clicks, clock drift, error dialog text, process tree and exit codes) and need nothing from the game:

```
dotnet test tests/WebUIDoctor.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
