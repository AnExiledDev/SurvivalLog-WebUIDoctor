using System;
using WebUIDoctor.Core;

namespace WebUIDoctor.Adapters;

/// <summary>Applies <see cref="RecoveryTimeLift"/> to Unity's time scale. Main thread only.</summary>
internal sealed class TimeLift
{
    private readonly Action<string> _log;
    private LiftState _state = LiftState.Initial;

    public TimeLift(Action<string> log) => _log = log;

    public void Tick(bool isRecovering)
    {
        var timeScale = UnityEngine.Time.timeScale;
        var step = RecoveryTimeLift.Tick(_state, isRecovering, timeScale, UnityEngine.Time.deltaTime);

        _state = step.Next;

        switch (step.Action)
        {
            case LiftAction.None:
                return;
            case LiftAction.Lift:
                UnityEngine.Time.timeScale = step.Scale;
                _log($"the game is paused (time scale {timeScale}) while it recovers the UI, and its recovery waits one second of game time; " +
                     "running time at normal speed for that second");
                return;
            case LiftAction.Restore:
                UnityEngine.Time.timeScale = step.Scale;
                _log($"recovery wait is over; put the pause back (time scale {step.Scale})");
                return;
            case LiftAction.LeaveSpeedAlone:
                _log($"the game speed changed to time scale {step.Scale} during the recovery wait; leaving it as it is");
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(step.Action), step.Action, null);
        }
    }
}
