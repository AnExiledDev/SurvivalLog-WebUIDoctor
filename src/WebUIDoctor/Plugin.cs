using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using WebUIDoctor.Adapters;
using WebUIDoctor.Core;

namespace WebUIDoctor;

[BepInPlugin(Guid, "WebUI Doctor", Version)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.webuidoctor";
    private const string Version = "0.4.1";

    public override void Load()
    {
        var isHardRestartEnabled = Config.Bind("Recovery", "EnableHardRestart", true,
            "When the game gives up recovering its UI, kill its Chromium processes and make it try again instead of showing the error.\n当游戏放弃恢复界面时，关闭它的 Chromium 进程并让它重试，而不是显示错误。").Value;
        var isExitLoggingEnabled = Config.Bind("Diagnostics", "LogChromiumExits", true,
            "Log every Chromium process start and exit (with exit code) to BepInEx/webui-doctor.log.\n把每个 Chromium 进程的启动和退出（含退出代码）记录到 BepInEx/webui-doctor.log。").Value;

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
        var doctor = new Doctor(log, settings, isHardRestartEnabled, gamePid,
            new RepairHotkey(KeyCode.F12, log.Write), new RepairHotkey(KeyCode.F11, log.Write), probe, new ClockWatch(log.Write),
            new ClickWatch(log.Write), new TimeLift(log.Write));

        log.Write($"===== WebUI Doctor {Version} started, game pid {gamePid}, hardRestart={isHardRestartEnabled} " +
                  $"backoff=[{string.Join(", ", settings.Backoff.Select(wait => $"{wait.TotalSeconds:0}s"))}] forgiveAfter={settings.ForgiveAfter.TotalMinutes:0}min");
        WebUILayerPatches.Apply(new Harmony(Guid), doctor, probe, log.Write);
        DoctorTicker.Tick = doctor.Tick;
        DoctorTicker.Log = log.Write;
        AddComponent<DoctorTicker>();

        if (isExitLoggingEnabled) new ChildExitWatcher(gamePid, TimeSpan.FromMilliseconds(500), log.Write).Start();
    }
}
