using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UlanziAdapter.Core.Input;
using UlanziAdapter.Windows.Native;

namespace UlanziAdapter.Windows.Input;

public sealed class KeyboardHookInputSource : IInputSource
{
    private NativeMethods.LowLevelKeyboardProc? _hookCallback;
    private IntPtr _hookHandle;
    private Func<InputEvent, bool>? _handler;
    private ModifierState _modifiers = ModifierState.Empty;

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public void Start(Func<InputEvent, bool> handler)
    {
        if (IsRunning)
        {
            return;
        }

        _handler = handler;
        _hookCallback = HookCallback;

        using var currentProcess = Process.GetCurrentProcess();
        var moduleHandle = NativeMethods.GetModuleHandle(currentProcess.MainModule?.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookCallback, moduleHandle, 0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to install low-level keyboard hook.");
        }
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _handler = null;
        _hookCallback = null;
        _modifiers = ModifierState.Empty;
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var isDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        var isUp = message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
        if (!isDown && !isUp)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var keyboardData = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
        var keyName = VirtualKeyNames.FromVirtualKey((int)keyboardData.vkCode);
        var injected = (keyboardData.flags & NativeMethods.LLKHF_INJECTED) != 0;

        if (isDown && KeyName.IsModifier(keyName))
        {
            _modifiers = UpdateModifierState(keyName, true);
        }

        var input = new InputEvent(
            keyName,
            isDown,
            isUp,
            _modifiers,
            DateTimeOffset.UtcNow,
            injected);

        var suppress = !injected && (_handler?.Invoke(input) ?? false);

        if (isUp && KeyName.IsModifier(keyName))
        {
            _modifiers = UpdateModifierState(keyName, false);
        }

        return suppress
            ? new IntPtr(1)
            : NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private ModifierState UpdateModifierState(string keyName, bool pressed)
    {
        return KeyName.Normalize(keyName) switch
        {
            "Ctrl" => _modifiers with { Ctrl = pressed },
            "Shift" => _modifiers with { Shift = pressed },
            "Alt" => _modifiers with { Alt = pressed },
            "Win" => _modifiers with { Win = pressed },
            _ => _modifiers
        };
    }
}
