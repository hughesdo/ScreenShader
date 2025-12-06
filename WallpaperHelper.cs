using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;   // Needed for Rectangle
using Microsoft.Win32;

namespace ScreenShader
{
    internal static class WallpaperHelper
    {
        private const int WM_SPAWN_WORKERW = 0x052C;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam,
            int fuFlags, int uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? lclassName, string? windowTitle);

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        private const int SW_SHOW = 5;
        private const int SW_HIDE = 0;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint GW_HWNDPREV = 3;
        private const uint GW_HWNDNEXT = 2;
        private const uint GW_CHILD = 5;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // ListView messages for transparent background
        private const int LVM_SETBKCOLOR = 0x1001;
        private const int LVM_SETTEXTBKCOLOR = 0x1026;
        private const int LVM_SETEXTENDEDLISTVIEWSTYLE = 0x1036;
        private const int LVS_EX_TRANSPARENTBKGND = 0x00400000;
        private static readonly IntPtr CLR_NONE = new IntPtr(-1);

        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
        private const int WS_CHILD = 0x40000000;

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        private const uint LWA_ALPHA = 0x2;

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_ERASE = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        // Store original wallpaper path so we can restore it
        private static string? _originalWallpaper = null;

        /// <summary>
        /// Hide the Windows wallpaper by setting it to blank/solid color
        /// </summary>
        public static void HideWindowsWallpaper()
        {
            try
            {
                // Save the current wallpaper path
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                _originalWallpaper = key?.GetValue("Wallpaper") as string;
                Logger.Log($"Saved original wallpaper: {_originalWallpaper}");

                // Set wallpaper to empty string (solid color background)
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, "", SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                Logger.Log("Set wallpaper to solid color (hidden)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to hide wallpaper: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore the original Windows wallpaper
        /// </summary>
        public static void RestoreWindowsWallpaper()
        {
            try
            {
                if (!string.IsNullOrEmpty(_originalWallpaper))
                {
                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, _originalWallpaper, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    Logger.Log($"Restored wallpaper: {_originalWallpaper}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to restore wallpaper: {ex.Message}");
            }
        }

        /// <summary>
        /// Make the desktop icons ListView have a transparent background
        /// </summary>
        public static void MakeDesktopIconsTransparent()
        {
            try
            {
                var progman = FindWindow("Progman", "Program Manager");
                if (progman == IntPtr.Zero) return;

                // Find SHELLDLL_DefView
                IntPtr shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView == IntPtr.Zero)
                {
                    EnumWindows((hwnd, lParam) =>
                    {
                        IntPtr sv = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (sv != IntPtr.Zero) { shellView = sv; return false; }
                        return true;
                    }, IntPtr.Zero);
                }
                if (shellView == IntPtr.Zero) return;

                // Find the ListView (SysListView32) inside SHELLDLL_DefView
                IntPtr listView = FindWindowEx(shellView, IntPtr.Zero, "SysListView32", null);
                if (listView == IntPtr.Zero)
                {
                    Logger.LogWarning("Could not find desktop icons ListView");
                    return;
                }
                Logger.Log($"Found desktop ListView: {listView}");

                // Set ListView to have transparent background
                SendMessage(listView, LVM_SETBKCOLOR, IntPtr.Zero, CLR_NONE);
                SendMessage(listView, LVM_SETTEXTBKCOLOR, IntPtr.Zero, CLR_NONE);
                Logger.Log("Set ListView background to transparent");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to make icons transparent: {ex.Message}");
            }
        }

        /// <summary>
        /// Attach a child window to the WorkerW behind the desktop icons.
        /// Windows 11 24H2/25H2 uses "raised desktop with layered ShellView" which requires
        /// special handling based on Lively Wallpaper's implementation.
        /// </summary>
        public static void AttachToWallpaper(IntPtr childWindow)
        {
            Logger.Log($"AttachToWallpaper: childWindow={childWindow}");

            var progman = FindWindow("Progman", "Program Manager");
            Logger.Log($"Found Progman: {progman}");

            if (progman == IntPtr.Zero)
            {
                Logger.LogError("Progman not found!");
                throw new InvalidOperationException("Could not find Progman.");
            }

            // ============================================================
            // WINDOWS 11 24H2/25H2 DETECTION (from Lively Wallpaper)
            // Check if Progman has WS_EX_NOREDIRECTIONBITMAP style
            // This indicates "raised desktop with layered ShellView" mode
            // ============================================================
            int progmanExStyle = GetWindowLong(progman, GWL_EXSTYLE);
            bool isRaisedDesktopWithLayeredShellView = (progmanExStyle & WS_EX_NOREDIRECTIONBITMAP) != 0;
            Logger.Log($"Progman ExStyle: 0x{progmanExStyle:X8}, IsRaisedDesktopWithLayeredShellView: {isRaisedDesktopWithLayeredShellView}");

            // Send message to spawn WorkerW behind desktop icons
            SendMessageTimeout(progman, WM_SPAWN_WORKERW, IntPtr.Zero, (IntPtr)1, 0, 1000, out _);
            SendMessageTimeout(progman, WM_SPAWN_WORKERW, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
            Logger.Log("Sent WM_SPAWN_WORKERW messages");

            IntPtr workerw = IntPtr.Zero;
            IntPtr defViewParent = IntPtr.Zero;
            IntPtr shellView = IntPtr.Zero;

            // Find SHELLDLL_DefView - it could be in Progman or in a WorkerW
            EnumWindows((hWnd, lParam) =>
            {
                IntPtr shellViewWin = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellViewWin != IntPtr.Zero)
                {
                    defViewParent = hWnd;
                    shellView = shellViewWin;
                    var className = new System.Text.StringBuilder(256);
                    GetClassName(hWnd, className, className.Capacity);
                    Logger.Log($"Found SHELLDLL_DefView inside {className} (handle={hWnd})");
                    workerw = FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null);
                    Logger.Log($"Sibling WorkerW: {workerw}");
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            // Check for WorkerW as child of Progman (Win11 24H2/25H2)
            if (workerw == IntPtr.Zero)
            {
                workerw = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
                if (workerw != IntPtr.Zero)
                    Logger.Log($"Found WorkerW as child of Progman: {workerw}");
            }

            var bounds = GetVirtualScreenBounds();

            // ============================================================
            // WINDOWS 11 24H2/25H2 LAYERED DESKTOP FIX (from Lively Wallpaper)
            // Microsoft says: "your application will now need to create its own
            // WS_EX_LAYERED child HWND that is z-ordered under the DefView window
            // but above the WorkerW window. This window should likely be a
            // SetLayeredWindowAttributes(bAlpha=0xFF) window"
            // ============================================================
            if (isRaisedDesktopWithLayeredShellView && shellView != IntPtr.Zero)
            {
                Logger.Log("Using Windows 11 24H2/25H2 layered desktop approach");

                // 1. Set WS_CHILD style
                int currentStyle = GetWindowLong(childWindow, GWL_STYLE);
                SetWindowLong(childWindow, GWL_STYLE, currentStyle | WS_CHILD);
                Logger.Log($"Set WS_CHILD style: 0x{(currentStyle | WS_CHILD):X8}");

                // 2. Add WS_EX_LAYERED style
                int currentExStyle = GetWindowLong(childWindow, GWL_EXSTYLE);
                SetWindowLong(childWindow, GWL_EXSTYLE, currentExStyle | WS_EX_LAYERED);
                Logger.Log($"Set WS_EX_LAYERED style: 0x{(currentExStyle | WS_EX_LAYERED):X8}");

                // 3. Set LayeredWindowAttributes with bAlpha=255 (fully opaque)
                SetLayeredWindowAttributes(childWindow, 0, 255, LWA_ALPHA);
                Logger.Log("Set LayeredWindowAttributes(alpha=255)");

                // 4. Set parent to Progman
                SetParent(childWindow, progman);
                Logger.Log($"SetParent to Progman: {progman}");

                // 5. Z-order BELOW ShellDLL_DefView but ABOVE WorkerW
                // Use shellView as the insertAfter parameter - this puts us just below it
                SetWindowPos(childWindow, shellView, 0, 0, bounds.Width, bounds.Height,
                    SWP_SHOWWINDOW | SWP_NOACTIVATE);
                Logger.Log($"Positioned below ShellDLL_DefView: {shellView}");

                // Ensure WorkerW is below us if it exists
                if (workerw != IntPtr.Zero)
                {
                    SetWindowPos(workerw, childWindow, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    Logger.Log($"Ensured WorkerW {workerw} is below us");
                }

                ShowWindow(childWindow, SW_SHOW);
            }
            else if (workerw != IntPtr.Zero)
            {
                // Classic approach: parent to WorkerW
                Logger.Log($"Using classic WorkerW approach: {workerw}");
                SetParent(childWindow, workerw);
                SetWindowPos(childWindow, IntPtr.Zero, 0, 0, bounds.Width, bounds.Height, SWP_SHOWWINDOW);
                ShowWindow(childWindow, SW_SHOW);
            }
            else if (shellView != IntPtr.Zero && defViewParent != IntPtr.Zero)
            {
                // Fallback: parent to DefView's parent, behind ShellDLL_DefView
                Logger.Log("Using fallback: parent to DefView parent");
                SetParent(childWindow, defViewParent);
                SetWindowPos(childWindow, HWND_BOTTOM, 0, 0, bounds.Width, bounds.Height, SWP_SHOWWINDOW);
                SetWindowPos(shellView, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                ShowWindow(childWindow, SW_SHOW);

                IntPtr listView = FindWindowEx(shellView, IntPtr.Zero, "SysListView32", null);
                if (listView != IntPtr.Zero)
                {
                    SendMessage(listView, LVM_SETBKCOLOR, IntPtr.Zero, CLR_NONE);
                    SendMessage(listView, LVM_SETTEXTBKCOLOR, IntPtr.Zero, CLR_NONE);
                    Logger.Log($"Set ListView {listView} background to transparent");
                }
            }
            else
            {
                Logger.LogError("Could not find suitable parent window!");
                throw new InvalidOperationException("Could not find WorkerW or Progman wallpaper layer.");
            }

            GetWindowRect(childWindow, out RECT childRect);
            bool childVisible = IsWindowVisible(childWindow);
            Logger.Log($"Child window after setup: Visible={childVisible}, Rect=({childRect.Left},{childRect.Top})-({childRect.Right},{childRect.Bottom})");
        }

        /// <summary>
        /// Alternative: Make window bottom-most (behind all other windows but visible)
        /// Use this when WorkerW technique doesn't work
        /// </summary>
        public static void MakeBottomMost(IntPtr window)
        {
            Logger.Log($"MakeBottomMost: window={window}");
            var bounds = GetVirtualScreenBounds();

            // Put window at bottom of Z-order and size it to cover all monitors
            SetWindowPos(window, HWND_BOTTOM, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                SWP_SHOWWINDOW | SWP_NOACTIVATE);
            ShowWindow(window, SW_SHOW);

            GetWindowRect(window, out RECT rect);
            bool visible = IsWindowVisible(window);
            Logger.Log($"After MakeBottomMost: Visible={visible}, Rect=({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom})");
        }

        // Store original parent of SHELLDLL_DefView for restoration
        private static IntPtr _originalShellViewParent = IntPtr.Zero;
        private static IntPtr _shellViewHandle = IntPtr.Zero;

        /// <summary>
        /// Reparent the desktop icons (SHELLDLL_DefView) to be a child of our window
        /// so they render ON TOP of our shader
        /// </summary>
        public static bool ReparentDesktopIcons(IntPtr newParent)
        {
            Logger.Log($"ReparentDesktopIcons: newParent={newParent}");

            var progman = FindWindow("Progman", "Program Manager");
            if (progman == IntPtr.Zero)
            {
                Logger.LogError("Progman not found!");
                return false;
            }

            // Find SHELLDLL_DefView
            IntPtr shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            IntPtr originalParent = progman;

            if (shellView == IntPtr.Zero)
            {
                // Try looking in WorkerW windows
                EnumWindows((hwnd, lParam) =>
                {
                    IntPtr sv = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (sv != IntPtr.Zero)
                    {
                        shellView = sv;
                        originalParent = hwnd;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }

            if (shellView == IntPtr.Zero)
            {
                Logger.LogWarning("SHELLDLL_DefView not found");
                return false;
            }

            Logger.Log($"Found SHELLDLL_DefView: {shellView}, original parent: {originalParent}");

            // Store for later restoration
            _originalShellViewParent = originalParent;
            _shellViewHandle = shellView;

            // Reparent SHELLDLL_DefView to our window
            SetParent(shellView, newParent);

            // Position it to fill our window
            var bounds = GetVirtualScreenBounds();
            SetWindowPos(shellView, HWND_TOP, 0, 0, bounds.Width, bounds.Height, SWP_SHOWWINDOW);

            Logger.Log($"Reparented SHELLDLL_DefView to {newParent}");
            return true;
        }

        /// <summary>
        /// Restore the desktop icons to their original parent
        /// </summary>
        public static void RestoreDesktopIcons()
        {
            if (_shellViewHandle != IntPtr.Zero && _originalShellViewParent != IntPtr.Zero)
            {
                Logger.Log($"Restoring SHELLDLL_DefView to original parent {_originalShellViewParent}");
                SetParent(_shellViewHandle, _originalShellViewParent);
                SetWindowPos(_shellViewHandle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                _shellViewHandle = IntPtr.Zero;
                _originalShellViewParent = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Attach window as a child of Progman, positioned behind SHELLDLL_DefView (icons)
        /// </summary>
        public static bool AttachBehindIcons(IntPtr childWindow)
        {
            Logger.Log($"AttachBehindIcons: childWindow={childWindow}");

            var progman = FindWindow("Progman", "Program Manager");
            if (progman == IntPtr.Zero)
            {
                Logger.LogError("Progman not found!");
                return false;
            }
            Logger.Log($"Found Progman: {progman}");

            // Find SHELLDLL_DefView (the desktop icons container)
            IntPtr shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            IntPtr defViewParent = progman;
            if (shellView == IntPtr.Zero)
            {
                // Try looking in WorkerW windows
                EnumWindows((hwnd, lParam) =>
                {
                    IntPtr sv = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (sv != IntPtr.Zero)
                    {
                        shellView = sv;
                        defViewParent = hwnd;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }

            if (shellView == IntPtr.Zero)
            {
                Logger.LogWarning("SHELLDLL_DefView not found");
                return false;
            }
            Logger.Log($"Found SHELLDLL_DefView: {shellView}, Parent: {defViewParent}");

            // Find the ListView inside SHELLDLL_DefView
            IntPtr listView = FindWindowEx(shellView, IntPtr.Zero, "SysListView32", null);
            if (listView == IntPtr.Zero)
            {
                Logger.LogWarning("Desktop ListView not found");
                return false;
            }
            Logger.Log($"Found desktop ListView: {listView}");

            var bounds = GetVirtualScreenBounds();

            // Make our window a child of SHELLDLL_DefView (same parent as ListView)
            SetParent(childWindow, shellView);
            Logger.Log($"SetParent to SHELLDLL_DefView {shellView} completed");

            // Position our window at the bottom within SHELLDLL_DefView
            // The ListView should be on top of us
            SetWindowPos(childWindow, HWND_BOTTOM, 0, 0, bounds.Width, bounds.Height,
                SWP_SHOWWINDOW | SWP_NOACTIVATE);

            // Bring the ListView to the top
            SetWindowPos(listView, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            // Make ListView background transparent
            SendMessage(listView, LVM_SETBKCOLOR, IntPtr.Zero, CLR_NONE);
            SendMessage(listView, LVM_SETTEXTBKCOLOR, IntPtr.Zero, CLR_NONE);

            ShowWindow(childWindow, SW_SHOW);

            // Force a repaint
            RedrawWindow(shellView, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_UPDATENOW | RDW_ERASE);

            GetWindowRect(childWindow, out RECT rect);
            bool visible = IsWindowVisible(childWindow);
            Logger.Log($"After AttachBehindIcons: Visible={visible}, Rect=({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom})");

            return true;
        }

        /// <summary>
        /// Attach window as a child of SHELLDLL_DefView (the desktop icons layer)
        /// This places our window INSIDE the desktop icons container, behind the icons themselves
        /// </summary>
        public static bool AttachToDesktop(IntPtr childWindow)
        {
            Logger.Log($"AttachToDesktop: childWindow={childWindow}");

            var progman = FindWindow("Progman", "Program Manager");
            if (progman == IntPtr.Zero)
            {
                Logger.LogError("Progman not found!");
                return false;
            }
            Logger.Log($"Found Progman: {progman}");

            // Find SHELLDLL_DefView (the desktop icons container)
            IntPtr shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView == IntPtr.Zero)
            {
                // Try looking in WorkerW windows
                EnumWindows((hwnd, lParam) =>
                {
                    IntPtr sv = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (sv != IntPtr.Zero)
                    {
                        shellView = sv;
                        return false; // Stop enumeration
                    }
                    return true;
                }, IntPtr.Zero);
            }

            if (shellView == IntPtr.Zero)
            {
                Logger.LogWarning("SHELLDLL_DefView not found anywhere");
                return false;
            }
            Logger.Log($"Found SHELLDLL_DefView: {shellView}");

            // Make our window a child of SHELLDLL_DefView
            SetParent(childWindow, shellView);
            Logger.Log("SetParent to SHELLDLL_DefView completed");

            // Size the window to cover the screen
            var bounds = GetVirtualScreenBounds();

            // Position at bottom of z-order within SHELLDLL_DefView (behind the icons listview)
            SetWindowPos(childWindow, HWND_BOTTOM, 0, 0, bounds.Width, bounds.Height,
                SWP_SHOWWINDOW | SWP_NOACTIVATE);
            ShowWindow(childWindow, SW_SHOW);

            GetWindowRect(childWindow, out RECT rect);
            bool visible = IsWindowVisible(childWindow);
            Logger.Log($"After AttachToDesktop: Visible={visible}, Rect=({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom})");

            return true;
        }

        /// <summary>
        /// Get combined bounds of all monitors (virtual desktop).
        /// </summary>
        public static Rectangle GetVirtualScreenBounds()
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Bounds.Left < minX) minX = screen.Bounds.Left;
                if (screen.Bounds.Top < minY) minY = screen.Bounds.Top;
                if (screen.Bounds.Right > maxX) maxX = screen.Bounds.Right;
                if (screen.Bounds.Bottom > maxY) maxY = screen.Bounds.Bottom;
            }

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Get working area of primary screen (excludes taskbar)
        /// </summary>
        public static Rectangle GetPrimaryWorkingArea()
        {
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            Logger.Log($"Primary working area: {area.X},{area.Y} {area.Width}x{area.Height}");
            return area;
        }

        /// <summary>
        /// Bring the Windows taskbar to the top of the z-order
        /// </summary>
        public static void BringTaskbarToTop()
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar != IntPtr.Zero)
            {
                SetWindowPos(taskbar, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                Logger.Log($"Brought taskbar {taskbar} to top");
            }
            else
            {
                Logger.LogWarning("Taskbar not found");
            }
        }

        /// <summary>
        /// Position our window behind Progman (the desktop) by setting z-order
        /// This is safer than reparenting - no orphaned windows if we crash
        /// </summary>
        public static bool PositionBehindDesktop(IntPtr ourWindow)
        {
            Logger.Log($"PositionBehindDesktop: ourWindow={ourWindow}");

            var progman = FindWindow("Progman", "Program Manager");
            if (progman == IntPtr.Zero)
            {
                Logger.LogWarning("Progman not found");
                return false;
            }
            Logger.Log($"Found Progman: {progman}");

            // Position our window directly behind Progman
            // HWND_BOTTOM puts us at the very bottom, but Progman is also at the bottom
            // So we use SetWindowPos with Progman as the "insert after" - puts us behind Progman
            var bounds = GetVirtualScreenBounds();

            // First, show our window
            SetWindowPos(ourWindow, HWND_BOTTOM, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                SWP_SHOWWINDOW | SWP_NOACTIVATE);

            // Then bring Progman in front of us
            SetWindowPos(progman, ourWindow, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            Logger.Log("Positioned our window behind Progman");
            return true;
        }
    }
}
