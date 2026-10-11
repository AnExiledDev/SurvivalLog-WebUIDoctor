> **AI-written from operator direction.** The intent is the operator's; the wording and scope are an agent's.
> `session: 127889d4-f8d8-4d17-8ded-f6f46da456fc | 2026-10-11 | source: op:2026-10-11-0745-b8ce`

# WebUI Doctor

A BepInEx 6 (IL2CPP) plugin for Survival Log that keeps the game playable through its recurring "interface error", where the embedded Chromium browser that draws the UI crashes and the game's own recovery gives up.

## What it does

- When the game gives up recovering its UI, it kills the game's Chromium processes, skips the blocking "automatic recovery has failed" dialog and makes the game recover again.
- It never gives up. The first retry runs after 3 seconds; if that fails it waits 10 s, 30 s, 1 min, then every 2 min. Once the UI has stayed up for 2 minutes, the next error starts from 3 seconds again.
- After any recovery (its own or the game's) it closes a leftover error dialog, gives mouse clicks back to the UI and releases a pause left stuck by the crash.
- It hands Chromium the game's page-access flags again after a restart, so other UI mods keep working on the recovered page.
- Once a minute it checks the clock the UI draws against the game's time and fixes it when they drift.

## Hotkeys

- `Ctrl+F12`: repair the UI by hand (close the error dialog, restore clicks, release a stuck pause).
- `Ctrl+F11`: diagnostic probe of the page the other UI mods draw on. Changes nothing.

## Config

`BepInEx/config/com.dev.survivallog.webuidoctor.cfg`:

- `Recovery/EnableHardRestart` (default `true`): turn the kill-and-retry off and let the game's error dialog show.
- `Diagnostics/LogChromiumExits` (default `true`): log every Chromium start and exit code.

Everything it does is logged to `BepInEx/webui-doctor.log`.

## Install

Download `WebUIDoctor.dll` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-WebUIDoctor/releases/latest) and copy it into `BepInEx/plugins`. Needs BepInEx 6 (IL2CPP build).

## Build and test

```sh
dotnet build src/WebUIDoctor -c Release -p:GameDir="D:\path\to\Survival Log"
dotnet test tests/WebUIDoctor.Tests
```

The build references BepInEx and the game's interop assemblies under `GameDir`, so BepInEx must be installed and the game launched once. The tests compile only the pure `Core` code and need nothing from the game.

## License

MIT, see [LICENSE](LICENSE).

## Provenance

> Alright, put all of these mods in separate GitHub repos, public is fine.
