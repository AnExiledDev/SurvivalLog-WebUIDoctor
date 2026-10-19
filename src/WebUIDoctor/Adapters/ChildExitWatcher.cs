using System;
using System.Collections.Generic;
using System.Threading;
using WebUIDoctor.Core;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Polls the game's Chromium processes and logs how each one ends: its kind, exit code and lifetime.
/// The game and Vuplex never record why the UI renderer died; the exit code is the evidence.
/// A process that starts and dies inside one poll interval is missed.
/// </summary>
internal sealed class ChildExitWatcher
{
    private sealed record Tracked(IntPtr Handle, string Kind, DateTime StartedAt);

    private readonly int _gamePid;
    private readonly TimeSpan _interval;
    private readonly Action<string> _log;
    private readonly Dictionary<int, Tracked> _tracked = new();
    private Thread? _thread;
    private volatile bool _isOn;

    public ChildExitWatcher(int gamePid, TimeSpan interval, Action<string> log)
    {
        _gamePid = gamePid;
        _interval = interval;
        _log = log;
    }

    /// <summary>Starts the watch thread the first time it is turned on. While off, the thread only sleeps.</summary>
    public void TurnOn(bool isOn)
    {
        _isOn = isOn;

        if (isOn && _thread is null) Start();
    }

    private void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "WebUIDoctor.ChildExitWatcher" };
        _thread.Start();
    }

    private void Run()
    {
        while (true)
        {
            if (_isOn) Watch();

            Thread.Sleep(_interval);
        }
    }

    private void Watch()
    {
        try
        {
            TrackNewProcesses();
            ReportExitedProcesses();
        }
        catch (Exception ex)
        {
            _log("watcher error: " + ex.Message);
        }
    }

    private void TrackNewProcesses()
    {
        foreach (var pid in WindowsProcesses.ChromiumUnder(_gamePid))
        {
            if (_tracked.ContainsKey(pid)) continue;

            var handle = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION | Win32.SYNCHRONIZE, false, pid);

            if (handle == IntPtr.Zero) continue;

            var commandLine = WindowsProcesses.ReadCommandLine(handle);
            var kind = ChromiumProcess.KindOf(commandLine);
            var flags = kind == "browser" ? (ChromiumProcess.HasPageAccessFlags(commandLine) ? ", page-access flags present" : ", page-access flags MISSING") : "";

            _tracked[pid] = new Tracked(handle, kind, DateTime.Now);
            _log($"chromium started: {kind} pid={pid}{flags}");
        }
    }

    private void ReportExitedProcesses()
    {
        var exited = new List<int>();

        foreach (var (pid, process) in _tracked)
        {
            if (Win32.WaitForSingleObject(process.Handle, 0) != Win32.WAIT_OBJECT_0) continue;

            var exitText = Win32.GetExitCodeProcess(process.Handle, out var code) ? ExitCodes.Describe(code) : "exit code unreadable";
            var lifetime = DateTime.Now - process.StartedAt;

            _log($"chromium EXITED: {process.Kind} pid={pid} exit={exitText} after {lifetime:hh\\:mm\\:ss}");
            Win32.CloseHandle(process.Handle);
            exited.Add(pid);
        }

        foreach (var pid in exited) _tracked.Remove(pid);
    }
}
