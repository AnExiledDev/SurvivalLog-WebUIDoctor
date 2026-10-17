using System;
using System.IO;
using System.Text;
using BepInEx.Logging;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Writes to the BepInEx log and to an append-only file that survives restarts
/// (LogOutput.log and Player.log are both overwritten on the next launch).
/// The file stays open for the launch, so a burst of lines does not reopen it once per line. A file past
/// <see cref="MaxBytes"/> at startup becomes webui-doctor.old.log, replacing the one before it.
/// </summary>
internal sealed class DoctorLog
{
    private const long MaxBytes = 1024 * 1024;

    private readonly ManualLogSource _bepinex;
    private readonly object _gate = new();
    private readonly StreamWriter? _file;

    public DoctorLog(ManualLogSource bepinex, string path)
    {
        _bepinex = bepinex;
        _file = Open(path);
    }

    public void Write(string message)
    {
        _bepinex.LogInfo(message);

        if (_file is null) return;

        lock (_gate)
        {
            try
            {
                _file.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
            }
            catch (Exception)
            {
                // The BepInEx log already has the line; losing the file copy (denied, disk full) is not worth crashing over.
            }
        }
    }

    private static StreamWriter? Open(string path)
    {
        try
        {
            RotateIfLarge(path);

            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            return new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch (Exception)
        {
            // Without the file the BepInEx log still has every line.
            return null;
        }
    }

    private static void RotateIfLarge(string path)
    {
        var file = new FileInfo(path);

        if (!file.Exists || file.Length <= MaxBytes) return;

        var old = Path.ChangeExtension(path, ".old.log");

        File.Delete(old);
        file.MoveTo(old);
    }
}
