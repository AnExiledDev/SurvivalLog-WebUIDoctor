using System;
using UnityEngine;
using WebUIDoctor.Core;

namespace WebUIDoctor.Adapters;

/// <summary>
/// A Ctrl+key hotkey. Turns itself off if the game has the legacy input API disabled. A press while a text box has the
/// keyboard is typing, not a hotkey.
/// </summary>
internal sealed class RepairHotkey
{
    private static readonly Func<KeyCode, bool> IsCtrlKeyDown = key => (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(key);
    private static readonly Func<bool> IsTyping = OptionalLive.IsTyping;

    private readonly KeyCode _key;
    private readonly Action<string> _log;
    private bool _isDisabled;

    public RepairHotkey(KeyCode key, Action<string> log)
    {
        _key = key;
        _log = log;
    }

    public bool WasPressed()
    {
        if (_isDisabled) return false;

        try
        {
            return KeyGate.Fires(_key, KeyCode.None, IsCtrlKeyDown, IsTyping);
        }
        catch (Exception ex)
        {
            _isDisabled = true;
            _log($"Ctrl+{_key} hotkey unavailable, the game's input does not support it: " + ex.Message);

            return false;
        }
    }
}
