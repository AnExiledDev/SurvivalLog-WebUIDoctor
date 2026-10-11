using System.Collections.Generic;

namespace WebUIDoctor.Core;

public static class ExitCodes
{
    /// <summary>Exit code WebUIDoctor uses when it kills Chromium itself, so its own kills are recognisable in the log.</summary>
    public const uint KilledByDoctor = 0xD0C7;

    // Chromium result codes come from content/public/common/result_codes.h; the rest are Windows NTSTATUS values.
    private static readonly Dictionary<uint, string> Known = new()
    {
        [0] = "normal exit",
        [1] = "RESULT_CODE_KILLED (terminated by Chromium or another process)",
        [2] = "RESULT_CODE_HUNG (Chromium killed it for hanging)",
        [3] = "RESULT_CODE_KILLED_BAD_MESSAGE (browser killed it over a bad IPC message)",
        [4] = "RESULT_CODE_GPU_DEAD_ON_ARRIVAL",
        [KilledByDoctor] = "killed by WebUIDoctor",
        [0x80000003] = "breakpoint: a Chromium CHECK or IMMEDIATE_CRASH fired",
        [0xC000001D] = "illegal instruction: Chromium IMMEDIATE_CRASH",
        [0xC0000005] = "access violation (native crash)",
        [0xC0000017] = "out of memory (STATUS_NO_MEMORY)",
        [0xC00000FD] = "stack overflow",
        [0xC0000135] = "a required DLL was not found",
        [0xC0000142] = "DLL initialisation failed",
        [0xC000013A] = "terminated by Ctrl+C / console close",
        [0xC0000374] = "heap corruption",
        [0xC0000409] = "fail-fast / stack buffer overrun (often a Chromium CHECK)",
        [0xC0000602] = "fail-fast exception",
        [0xE0000008] = "Chromium out-of-memory (partition alloc or V8 heap)",
        [0xE06D7363] = "unhandled C++ exception",
        [0x40010004] = "DBG_TERMINATE_PROCESS (killed by a debugger or tool)",
        [0x887A0005] = "DXGI_ERROR_DEVICE_REMOVED (GPU device lost)",
        [0x887A0006] = "DXGI_ERROR_DEVICE_HUNG (GPU device hung)",
        [0x887A0007] = "DXGI_ERROR_DEVICE_RESET (GPU device reset)",
    };

    public static string Describe(uint code)
    {
        var hex = $"0x{code:X8}";

        return Known.TryGetValue(code, out var meaning) ? $"{hex} {meaning}" : $"{hex} (unrecognised, decimal {code})";
    }
}
