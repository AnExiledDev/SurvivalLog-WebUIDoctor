namespace WebUIDoctor.Core;

/// <summary>The game's own recovery bookkeeping on its WebUI layer, read once per frame and logged when it changes.</summary>
public readonly record struct RecoveryFlags(bool IsCrashed, bool NeedsRecover, bool IsRecovering, int Attempts, bool IsAbandoned)
{
    public override string ToString() =>
        $"crashed={IsCrashed} needsRecover={NeedsRecover} recovering={IsRecovering} attempts={Attempts} abandoned={IsAbandoned}";
}
