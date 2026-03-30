using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using RadialSek.Models;

namespace RadialSek.Services
{
    public sealed class GlobalInputHook : IDisposable
    {
        private const int WhMouseLl = 14;
        private const int WhKeyboardLl = 13;
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;
        private const int WmRButtonDown = 0x0204;
        private const int WmRButtonUp = 0x0205;
        private const int WmMButtonDown = 0x0207;
        private const int WmMButtonUp = 0x0208;
        private const int WmXButtonDown = 0x020B;
        private const int WmXButtonUp = 0x020C;
        private const int VkControl = 0x11;
        private const int VkMenu = 0x12;
        private const int VkShift = 0x10;
        private const int VkLWin = 0x5B;
        private const int VkRWin = 0x5C;

        private readonly HookProc _mouseProc;
        private readonly HookProc _keyboardProc;
        private List<ActivationShortcut> _shortcuts = new List<ActivationShortcut> { new ActivationShortcut() };
        private IntPtr _mouseHookHandle = IntPtr.Zero;
        private IntPtr _keyboardHookHandle = IntPtr.Zero;
        private DateTime _lastActivationUtc = DateTime.MinValue;
        private bool _programEnabled = true;
        private readonly HashSet<string> _suppressedMouseUpTriggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<ActivationEventArgs>? ActivationRequested;

        public GlobalInputHook()
        {
            _mouseProc = MouseHookCallback;
            _keyboardProc = KeyboardHookCallback;
        }

        public void UpdateShortcuts(IReadOnlyList<ActivationShortcut>? shortcuts)
        {
            _shortcuts = (shortcuts == null || shortcuts.Count == 0
                    ? new List<ActivationShortcut> { new ActivationShortcut() }
                    : shortcuts.Select(x => x.Clone()).ToList());
        }

        public void SetProgramEnabled(bool enabled)
        {
            _programEnabled = enabled;
        }

        public void Start()
        {
            if (_mouseHookHandle != IntPtr.Zero || _keyboardHookHandle != IntPtr.Zero)
            {
                return;
            }

            using var process = Process.GetCurrentProcess();
            using var module = process.MainModule;
            var moduleHandle = GetModuleHandle(module?.ModuleName);
            _mouseHookHandle = SetWindowsHookEx(WhMouseLl, _mouseProc, moduleHandle, 0);
            _keyboardHookHandle = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, moduleHandle, 0);
        }

        public void Dispose()
        {
            if (_mouseHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
            }

            if (_keyboardHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
            {
                return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
            }

            var mouseUpTrigger = ResolveMouseUpTrigger(wParam, lParam);
            if (mouseUpTrigger != null && _suppressedMouseUpTriggers.Remove(mouseUpTrigger))
            {
                return (IntPtr)1;
            }

            var trigger = ResolveMouseTrigger(wParam, lParam);
            if (trigger == null)
            {
                return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
            }

            var hookStruct = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            if (TryActivate(trigger, hookStruct.pt.x, hookStruct.pt.y))
            {
                _suppressedMouseUpTriggers.Add(trigger);
                return (IntPtr)1;
            }

            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0 || (wParam != (IntPtr)WmKeyDown && wParam != (IntPtr)WmSysKeyDown))
            {
                return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
            }

            var hookStruct = Marshal.PtrToStructure<KbdllHookStruct>(lParam);
            var trigger = ResolveKeyboardTrigger(hookStruct.vkCode);
            if (trigger == null)
            {
                return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
            }

            GetCursorPos(out var point);
            return TryActivate(trigger, point.x, point.y)
                ? (IntPtr)1
                : CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        private bool TryActivate(string trigger, double x, double y)
        {
            if ((DateTime.UtcNow - _lastActivationUtc).TotalMilliseconds < 220)
            {
                return false;
            }

            foreach (var shortcut in _shortcuts)
            {
                if (!string.Equals(shortcut.Trigger, trigger, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ModifiersMatch(shortcut))
                {
                    continue;
                }

                if (!_programEnabled && shortcut.ShortcutId != ActivationShortcut.ToggleProgramShortcutId)
                {
                    continue;
                }

                _lastActivationUtc = DateTime.UtcNow;
                ActivationRequested?.Invoke(this, new ActivationEventArgs(x, y, shortcut.ShortcutId));
                return true;
            }

            return false;
        }

        private static bool ModifiersMatch(ActivationShortcut shortcut)
        {
            var ctrl = IsPressed(VkControl);
            var alt = IsPressed(VkMenu);
            var shift = IsPressed(VkShift);
            var win = IsPressed(VkLWin) || IsPressed(VkRWin);

            return ctrl == shortcut.Ctrl &&
                   alt == shortcut.Alt &&
                   shift == shortcut.Shift &&
                   win == shortcut.Win;
        }

        private static string? ResolveMouseTrigger(IntPtr wParam, IntPtr lParam)
        {
            if (wParam == (IntPtr)WmMButtonDown)
            {
                return "MiddleMouse";
            }

            if (wParam == (IntPtr)WmRButtonDown)
            {
                return "RightMouse";
            }

            if (wParam != (IntPtr)WmXButtonDown)
            {
                return null;
            }

            var hookStruct = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            var buttonData = (hookStruct.mouseData >> 16) & 0xffff;
            return buttonData == 1 ? "XButton1" :
                   buttonData == 2 ? "XButton2" :
                   null;
        }

        private static string? ResolveMouseUpTrigger(IntPtr wParam, IntPtr lParam)
        {
            if (wParam == (IntPtr)WmMButtonUp)
            {
                return "MiddleMouse";
            }

            if (wParam == (IntPtr)WmRButtonUp)
            {
                return "RightMouse";
            }

            if (wParam != (IntPtr)WmXButtonUp)
            {
                return null;
            }

            var hookStruct = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            var buttonData = (hookStruct.mouseData >> 16) & 0xffff;
            return buttonData == 1 ? "XButton1" :
                   buttonData == 2 ? "XButton2" :
                   null;
        }

        private static string? ResolveKeyboardTrigger(int vkCode)
        {
            if (vkCode >= 0x70 && vkCode <= 0x7B)
            {
                return "F" + (vkCode - 0x6F);
            }

            if (vkCode >= 0x41 && vkCode <= 0x5A)
            {
                return ((char)vkCode).ToString();
            }

            if (vkCode >= 0x30 && vkCode <= 0x39)
            {
                return "D" + (vkCode - 0x30);
            }

            return null;
        }

        private static bool IsPressed(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MsllHookStruct
        {
            public Point pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdllHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point point);
    }

    public sealed class ActivationEventArgs : EventArgs
    {
        public ActivationEventArgs(double screenX, double screenY, string shortcutId)
        {
            ScreenX = screenX;
            ScreenY = screenY;
            ShortcutId = shortcutId;
        }

        public double ScreenX { get; }
        public double ScreenY { get; }
        public string ShortcutId { get; }
    }
}
