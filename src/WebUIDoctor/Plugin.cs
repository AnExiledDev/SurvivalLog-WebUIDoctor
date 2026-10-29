using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using WebUIDoctor.Adapters;
using WebUIDoctor.Core;

namespace WebUIDoctor;

[BepInPlugin(Guid, "WebUI Doctor", Version)]
[BepInDependency(OptionalLive.ToolkitGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.webuidoctor";
    private const string Version = "0.6.1";

    public override void Load()
    {
        if (!OptionalLive.IsToolkitInstalled) Log.LogInfo(OptionalLive.MissingNote);

        var isHardRestartEnabled = Config.Bind("Recovery", "EnableHardRestart", true,
            "When the game's menus fail to load and the game gives up, restart the browser the menus run in and try again, instead of showing the error.\n当游戏菜单加载失败且游戏放弃恢复时，重启菜单所用的浏览器并重试，而不是显示错误。");
        var isExitLoggingEnabled = Config.Bind("Diagnostics", "LogChromiumExits", true,
            new ConfigDescription("Log every Chromium process start and exit (with exit code) to BepInEx/webui-doctor.log.\n把每个 Chromium 进程的启动和退出（含退出代码）记录到 BepInEx/webui-doctor.log。", null, "ModManager.Advanced"));
        var menuFrameRate = Config.Bind("Display", "MenuFrameRate", WebFrameRate.Default,
            new ConfigDescription("How many times a second the menus and HUD redraw. The game uses 30, which makes menus feel slow to answer the mouse; 60 makes them smooth. Takes effect the next time you start the game.\n菜单和 HUD 每秒重绘的次数。游戏自带 30，菜单对鼠标反应会显得迟钝；60 会流畅很多。下次启动游戏时生效。",
                new AcceptableValueRange<int>(WebFrameRate.Min, WebFrameRate.Max)));

        var log = new DoctorLog(Log, Path.Combine(Paths.BepInExRootPath, "webui-doctor.log"));
        var gamePid = Environment.ProcessId;
        // No give-up: the game's error dialog blocks every click, so retrying is never worse than stopping.
        var settings = new RetrySettings(
            SettleDelay: TimeSpan.FromSeconds(1.5),
            DirectInvokeAfter: TimeSpan.FromSeconds(3),
            RepairDelay: TimeSpan.FromSeconds(1),
            ForgiveAfter: TimeSpan.FromMinutes(2),
            Backoff: new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1) });
        var probe = new PageProbe(log.Write);
        var doctor = new Doctor(log, settings, gamePid,
            new RepairHotkey(KeyCode.F12, log.Write), new RepairHotkey(KeyCode.F11, log.Write), probe, new ClockWatch(log.Write),
            new ClickWatch(log.Write), new TimeLift(log.Write));

        var exitWatcher = new ChildExitWatcher(gamePid, TimeSpan.FromMilliseconds(500), log.Write);

        log.Write($"===== WebUI Doctor {Version} started, game pid {gamePid} " +
                  $"backoff=[{string.Join(", ", settings.Backoff.Select(wait => $"{wait.TotalSeconds:0}s"))}] forgiveAfter={settings.ForgiveAfter.TotalMinutes:0}min");
        var harmony = new Harmony(Guid);
        WebUILayerPatches.Apply(harmony, doctor, probe, log.Write);
        WebFrameRatePatch.Apply(harmony, menuFrameRate, log.Write);
        DoctorTicker.Tick = doctor.Tick;
        DoctorTicker.Log = log.Write;
        OptionalLive.Watch(isHardRestartEnabled, isOn =>
        {
            doctor.IsHardRestartEnabled = isOn;
            log.Write($"hardRestart={isOn}");
        });
        OptionalLive.Watch(isExitLoggingEnabled, isOn =>
        {
            exitWatcher.TurnOn(isOn);
            log.Write($"logChromiumExits={isOn}");
        });
        AddComponent<DoctorTicker>();
    }
}
