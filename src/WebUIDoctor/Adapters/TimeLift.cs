using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using WebUIDoctor.Core;

namespace WebUIDoctor.Adapters;

/// <summary>Applies <see cref="RecoveryTimeLift"/> to Unity's time scale. Main thread only.</summary>
internal sealed class TimeLift
{
    private readonly Action<string> _log;
    private LiftState _state = LiftState.Initial;

    public TimeLift(Action<string> log) => _log = log;

    public void Tick(RecoveryFlags flags)
    {
        var timeScale = UnityEngine.Time.timeScale;
        var input = new LiftInput(flags.IsRecovering, flags.Attempts, IsSpeedPaused(), timeScale, UnityEngine.Time.deltaTime);
        var step = RecoveryTimeLift.Tick(_state, input);
        var wasLifted = _state is LiftState.Lifted;

        _state = step.Next;

        switch (step.Action)
        {
            case LiftAction.None:
                if (wasLifted && _state is LiftState.Lifted lifted && lifted.ScaledElapsed == 0f)
                    _log($"the game started recovery attempt {lifted.Attempt} while the lift was on; keeping normal speed for it");

                return;
            case LiftAction.Lift:
                UnityEngine.Time.timeScale = step.Scale;
                _log($"the game is paused (time scale {timeScale}) while it recovers the UI (attempt {flags.Attempts}), and the recovery waits on game time; " +
                     "running time at normal speed until it is done");
                return;
            case LiftAction.Restore:
                UnityEngine.Time.timeScale = step.Scale;
                _log(_state is LiftState.Spent
                    ? $"recovery attempt {flags.Attempts} is still not done after {RecoveryTimeLift.MaxLiftPerAttempt:0} seconds at normal speed; put the pause back (time scale {step.Scale})"
                    : $"recovery finished; put the pause back (time scale {step.Scale})");
                return;
            case LiftAction.LeaveSpeedAlone:
                _log($"the game speed changed during the recovery (time scale {step.Scale}); leaving it as it is");
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(step.Action), step.Action, null);
        }
    }

    /// <summary>The game's own speed setting. A lift only changes Unity's time scale, so this still reads paused during one.</summary>
    private static bool IsSpeedPaused() =>
        GameSingleton.Existing<BattleLogicWorld>()?._GameTimeManager?.IsPausedScale() ?? false;
}
