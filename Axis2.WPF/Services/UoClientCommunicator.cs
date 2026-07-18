using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Axis2.WPF.Services
{
    public class UoClientCommunicator
    {
        private const int kDelayKeystrokes = 0;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion U; }

        // The union must be sized to its largest member (MOUSEINPUT) so INPUT marshals to the exact
        // size Windows expects (40 bytes on x64). Sizing it to KEYBDINPUT alone makes SendInput fail
        // with ERROR_INVALID_PARAMETER (87) and inject nothing.
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        // UO client executables we recognise directly (process name, without ".exe"), so commands
        // reach the running client even when its window title isn't one of the classic markers.
        private static readonly string[] KnownClientProcesses =
            { "fwuo", "classicuo", "orion", "uosa", "uog", "uoclassic" };

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_MINIMIZE = 6;
        private const int VK_RETURN = 0x0D;

        // Messages Windows sp�cifiques
        private const uint WM_CHAR = 0x0102;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;

        private IntPtr hwndUOClient;
        private readonly string _commandPrefix;
        private readonly string _uoTitle;

        public UoClientCommunicator(string commandPrefix, string uoTitle)
        {
            _commandPrefix = commandPrefix;
            _uoTitle = uoTitle;
        }

        private bool ForceSetForegroundWindow(IntPtr hWnd)
        {
            try
            {
                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }
                else
                {
                    ShowWindow(hWnd, SW_SHOW);
                }

                uint foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), out _);
                uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);
                uint currentThreadId = GetCurrentThreadId();

                if (foregroundThreadId != currentThreadId)
                {
                    AttachThreadInput(currentThreadId, foregroundThreadId, true);
                }

                if (targetThreadId != currentThreadId)
                {
                    AttachThreadInput(currentThreadId, targetThreadId, true);
                }

                BringWindowToTop(hWnd);
                bool result = SetForegroundWindow(hWnd);

                if (foregroundThreadId != currentThreadId)
                {
                    AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }

                if (targetThreadId != currentThreadId)
                {
                    AttachThreadInput(currentThreadId, targetThreadId, false);
                }

                return result;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> SendToOrionDirectMessageAsync(string command)
        {
            try
            {
                string finalCommand = string.IsNullOrEmpty(_commandPrefix) ? command : _commandPrefix + command;

                // Cette m�thode envoie directement � la fen�tre, m�me si elle n'a pas le focus
                // Ouvrir le chat avec Enter
                SendMessage(hwndUOClient, WM_KEYDOWN, (IntPtr)VK_RETURN, (IntPtr)(1 | (28 << 16)));
                await Task.Delay(10);
                SendMessage(hwndUOClient, WM_KEYUP, (IntPtr)VK_RETURN, (IntPtr)(1 | (28 << 16) | (1 << 30) | (1 << 31)));

                await Task.Delay(50);

                // Envoyer chaque caract�re directement � la fen�tre
                foreach (char c in finalCommand)
                {
                    SendMessage(hwndUOClient, WM_CHAR, (IntPtr)c, (IntPtr)1);
                    await Task.Delay(kDelayKeystrokes);
                }

                // Enter final
                SendMessage(hwndUOClient, WM_KEYDOWN, (IntPtr)VK_RETURN, (IntPtr)(1 | (28 << 16)));
                await Task.Delay(10);
                SendMessage(hwndUOClient, WM_KEYUP, (IntPtr)VK_RETURN, (IntPtr)(1 | (28 << 16) | (1 << 30) | (1 << 31)));

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Finds the best-matching UO client top-level window by title OR owning process name.
        private IntPtr FindUoWindow()
        {
            IntPtr found = IntPtr.Zero;
            string configTitle = _uoTitle ?? string.Empty;
            // Allow the configured title to be an exe/process name too (e.g. "FWUO.exe").
            string configProc = configTitle;
            if (configProc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                configProc = configProc[..^4];

            var candidates = new System.Collections.Generic.List<string>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                string title = sb.ToString();

                string procName = GetProcessName(hWnd);

                if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrEmpty(procName))
                    candidates.Add($"'{title}' [{procName}]");

                bool titleMatch =
                    title.Contains("Orion", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("Ultima Online", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("UOSA", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("FWUO", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(configTitle) && title.Contains(configTitle, StringComparison.OrdinalIgnoreCase));

                bool procMatch =
                    (!string.IsNullOrEmpty(procName) &&
                        (KnownClientProcesses.Any(p => procName.Equals(p, StringComparison.OrdinalIgnoreCase)) ||
                         (configProc.Length >= 3 && procName.Contains(configProc, StringComparison.OrdinalIgnoreCase))));

                if (titleMatch || procMatch)
                {
                    found = hWnd;
                    Logger.Log($"UoClient: matched window '{title}' [process {procName}] handle {hWnd}.");
                    return false; // stop enumerating
                }
                return true;
            }, IntPtr.Zero);

            if (found == IntPtr.Zero)
            {
                Logger.Log("UoClient: no UO client window matched. Visible windows seen: " +
                           string.Join(", ", candidates.Take(40)));
            }
            return found;
        }

        // Types text into the currently-focused window via real keyboard input. Unlike WM_CHAR
        // messages, SendInput is picked up by SDL2/DirectX clients (ClassicUO, FWUO, …).
        private async Task<bool> SendToClientViaSendInputAsync(string command)
        {
            try
            {
                string finalCommand = string.IsNullOrEmpty(_commandPrefix) ? command : _commandPrefix + command;

                PressReturn();
                await Task.Delay(60);

                var chars = new System.Collections.Generic.List<INPUT>(finalCommand.Length * 2);
                foreach (char c in finalCommand)
                {
                    chars.Add(MakeUnicode(c, false));
                    chars.Add(MakeUnicode(c, true));
                }
                uint sent = 0;
                int err = 0;
                if (chars.Count > 0)
                {
                    sent = SendInput((uint)chars.Count, chars.ToArray(), Marshal.SizeOf<INPUT>());
                    err = Marshal.GetLastWin32Error();
                }
                await Task.Delay(40);

                PressReturn();
                Logger.Log($"UoClient: sent via SendInput: '{finalCommand}' (events injected {sent}/{chars.Count}, lastError={err}).");
                if (chars.Count > 0 && sent == 0)
                    Logger.Log("UoClient: SendInput injected 0 events — the client is very likely running elevated (as admin) while Axis is not. Run Axis as administrator, or start the client un-elevated.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"UoClient: SendInput send failed: {ex.Message}");
                return false;
            }
        }

        private static void PressReturn()
        {
            var down = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_RETURN } } };
            var up = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_RETURN, dwFlags = KEYEVENTF_KEYUP } } };
            SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
        }

        private static INPUT MakeUnicode(char c, bool keyUp) => new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0),
                }
            }
        };

        private static string GetProcessName(IntPtr hWnd)
        {
            try
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0)
                    return string.Empty;
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                return proc.ProcessName; // e.g. "FWUO" (no extension)
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<bool> SendToUOAsync(string command)
        {

            // Locate the running UO client window (Orion / Ultima Online / UOSA / FWUO / configured title,
            // or any known client executable such as FWUO.exe), re-searching only when the cached handle is gone.
            if (hwndUOClient == IntPtr.Zero || !IsWindow(hwndUOClient))
            {
                hwndUOClient = FindUoWindow();
            }

            if (hwndUOClient != IntPtr.Zero)
            {
                Logger.Log($"UoClient: sending '{command}' to handle {hwndUOClient} [process {GetProcessName(hwndUOClient)}].");
                bool focusResult = ForceSetForegroundWindow(hwndUOClient);
                await Task.Delay(120);
                bool focused = GetForegroundWindow() == hwndUOClient;
                Logger.Log($"UoClient: focus requested={focusResult}, foreground-match={focused}.");

                if (focused)
                {
                    // Real keyboard input — works for SDL2/DirectX clients (FWUO, ClassicUO) and classic ones.
                    return await SendToClientViaSendInputAsync(command);
                }

                // Could not focus the window: fall back to posting WM_CHAR directly (classic clients).
                Logger.Log("UoClient: window not focused; falling back to WM_CHAR direct message.");
                return await SendToOrionDirectMessageAsync(command);
            }
            else
            {
            }

            return false;
        }
    }
}
