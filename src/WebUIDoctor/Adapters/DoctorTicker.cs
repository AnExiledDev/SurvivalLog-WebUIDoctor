using System;
using UnityEngine;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Drives the doctor every frame from our own GameObject, so the retry still advances when the
/// game's WebUILayer has stopped updating after giving up.
/// </summary>
public sealed class DoctorTicker : MonoBehaviour
{
    internal static Action Tick = () => { };
    internal static Action<string> Log = _ => { };
    private static bool _hasReportedFailure;

    public DoctorTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            if (_hasReportedFailure) return;

            _hasReportedFailure = true;
            Log("tick failed (reported once): " + ex);
        }
    }
}
