using System;
using System.IO;
using BepInEx.Logging;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Writes to the BepInEx log and to an append-only file that survives restarts
/// (LogOutput.log and Player.log are both overwritten on the next launch).
/// </summary>
internal sealed class DoctorLog
{
    private readonly ManualLogSource _bepinex;
    private readonly string _path;
    private readonly object _gate = new();

    public DoctorLog(ManualLogSource bepinex, string path)
    {
        _bepinex = bepinex;
        _path = path;
    }

    public void Write(string message)
    {
        _bepinex.LogInfo(message);

        lock (_gate)
        {
            try
            {
                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch (Exception)
            {
                // The BepInEx log already has the line; losing the file copy (locked, denied, disk full) is not worth crashing over.
            }
        }
    }
}
