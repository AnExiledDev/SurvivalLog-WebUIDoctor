using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WebUIDoctor.Core;

namespace WebUIDoctor.Adapters;

internal static class WindowsProcesses
{
    public const string ChromiumExe = "Vuplex WebView.vuplex";

    public static List<ProcessEntry> Snapshot()
    {
        var table = new List<ProcessEntry>();
        var snapshot = Win32.CreateToolhelp32Snapshot(Win32.TH32CS_SNAPPROCESS, 0);

        if (snapshot == Win32.InvalidHandle) return table;

        try
        {
            var entry = new Win32.PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<Win32.PROCESSENTRY32W>() };

            for (var ok = Win32.Process32FirstW(snapshot, ref entry); ok; ok = Win32.Process32NextW(snapshot, ref entry))
            {
                table.Add(new ProcessEntry((int)entry.th32ProcessID, (int)entry.th32ParentProcessID, entry.szExeFile));
            }
        }
        finally
        {
            Win32.CloseHandle(snapshot);
        }

        return table;
    }

    public static IReadOnlyList<int> ChromiumUnder(int rootPid) =>
        ProcessTree.DescendantsNamed(Snapshot(), rootPid, ChromiumExe);

    /// <summary>Reads another process's command line; null when it can't be read.</summary>
    public static string? ReadCommandLine(IntPtr process)
    {
        Win32.NtQueryInformationProcess(process, Win32.ProcessCommandLineInformation, IntPtr.Zero, 0, out var needed);

        if (needed <= 0) return null;

        var buffer = Marshal.AllocHGlobal(needed);

        try
        {
            var status = Win32.NtQueryInformationProcess(process, Win32.ProcessCommandLineInformation, buffer, needed, out _);

            if (status != 0) return null;

            // The buffer starts with a UNICODE_STRING: ushort Length, ushort MaximumLength, pointer Buffer.
            var byteLength = (ushort)Marshal.ReadInt16(buffer);
            var text = Marshal.ReadIntPtr(buffer, IntPtr.Size);

            return Marshal.PtrToStringUni(text, byteLength / 2);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Kills every Chromium process under the game. Returns the pids it managed to kill.</summary>
    public static List<int> KillChromiumUnder(int rootPid, Action<string> log)
    {
        var killed = new List<int>();

        foreach (var pid in ChromiumUnder(rootPid))
        {
            var handle = Win32.OpenProcess(Win32.PROCESS_TERMINATE, false, pid);

            if (handle == IntPtr.Zero)
            {
                log($"could not open Chromium pid {pid} to kill it (win32 error {Marshal.GetLastWin32Error()})");
                continue;
            }

            try
            {
                if (Win32.TerminateProcess(handle, ExitCodes.KilledByDoctor)) killed.Add(pid);
                else log($"TerminateProcess failed for Chromium pid {pid} (win32 error {Marshal.GetLastWin32Error()})");
            }
            finally
            {
                Win32.CloseHandle(handle);
            }
        }

        return killed;
    }
}
