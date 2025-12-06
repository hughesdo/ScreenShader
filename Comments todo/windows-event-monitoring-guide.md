# Windows Event Monitoring with C#

## EVENT_OBJECT_LOCATIONCHANGE

EVENT_OBJECT_LOCATIONCHANGE is a WinEvent notification that fires whenever an accessible object's screen location changes, including top-level windows, child windows, and some UI elements.

### What it does

It is used with SetWinEventHook to get callbacks as a window is moved or resized, not just at the end of a move/size operation.

In your WinEventProc callback, the hwnd parameter identifies the window whose location changed, so you can filter to just the target window you are tracking.

### Why it fixes overlay "jumping"

Using EVENT_SYSTEM_MOVESIZEEND only tells you when the user releases the mouse, so overlays update in one jump at the end.

Switching to EVENT_OBJECT_LOCATIONCHANGE lets you update the overlay continuously as the window moves, so the overlay tracks in real time instead of lagging behind.

### Practical notes

You still hook it via `SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, ...)` with your WinEventProc.

Be aware that this event can also fire for mouse-related or other non-window objects; filtering by hwnd and object ID lets you ignore unrelated notifications.

## Basic Implementation - Location Tracking Only

A .NET app can use SetWinEventHook with EVENT_OBJECT_LOCATIONCHANGE to watch all top‑level windows and keep a dictionary of their rectangles updated in real time.

### Basic P/Invoke setup

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

class WindowTracker
{
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint OBJID_WINDOW = 0x00000000;

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess,
        uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    private static readonly Dictionary<IntPtr, RECT> _windowRects = new();
    private static WinEventDelegate _procDelegate;   // keep alive
    private static IntPtr _hook;

    public static void Start()
    {
        _procDelegate = WinEventProc;
        _hook = SetWinEventHook(
            EVENT_OBJECT_LOCATIONCHANGE,
            EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero,
            _procDelegate,
            0,          // all processes
            0,          // all threads
            WINEVENT_OUTOFCONTEXT);
    }

    private static void WinEventProc(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;
        if (idObject != (int)OBJID_WINDOW || idChild != 0) return;

        if (GetWindowRect(hwnd, out RECT rect))
        {
            _windowRects[hwnd] = rect;
            // rect now holds current position/size; dictionary is up to date.
        }
    }
}
```

This code hooks EVENT_OBJECT_LOCATIONCHANGE for all processes, filters to proper window objects, and calls GetWindowRect each time to update a `Dictionary<IntPtr, RECT>` with current positions. For a WinForms/WPF app, call `WindowTracker.Start()` during startup and keep the app message loop running so the callback continues to fire.

## Full Implementation - Creation, Destruction, and Position Tracking

Yes, you can monitor creation, destruction, and position changes of (top‑level) windows and maintain an up‑to‑date map of them with WinEvents.

### Events to use

- **EVENT_OBJECT_CREATE / EVENT_OBJECT_SHOW**: window appears (creation vs actually shown; many people prefer SHOW)
- **EVENT_OBJECT_DESTROY**: window is being destroyed
- **EVENT_OBJECT_LOCATIONCHANGE**: window moved or resized

Filter to OBJID_WINDOW and visible top‑level windows so you do not track every child control.

### Full C# example (single hook, full tracking)

```csharp
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

internal static class GlobalWindowMonitor
{
    private const uint EVENT_OBJECT_CREATE        = 0x8000;
    private const uint EVENT_OBJECT_DESTROY       = 0x8001;
    private const uint EVENT_OBJECT_SHOW          = 0x8002;
    private const uint EVENT_OBJECT_HIDE          = 0x8003;
    private const uint EVENT_OBJECT_LOCATIONCHANGE= 0x800B;

    private const uint WINEVENT_OUTOFCONTEXT      = 0x0000;
    private const int  OBJID_WINDOW               = 0x00000000;

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess,
        uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    private const uint GA_ROOT = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public override string ToString()
            => $"({Left},{Top})-({Right},{Bottom})";
    }

    private static readonly Dictionary<IntPtr, RECT> _windows = new();
    private static WinEventDelegate _proc;
    private static IntPtr _hook;

    public static IReadOnlyDictionary<IntPtr, RECT> Windows => _windows;

    public static void Start()
    {
        if (_hook != IntPtr.Zero) return;

        _proc = WinEventProc; // keep delegate rooted
        _hook = SetWinEventHook(
            EVENT_OBJECT_CREATE,              // min event
            EVENT_OBJECT_LOCATIONCHANGE,      // max event (covers range)
            IntPtr.Zero,
            _proc,
            0,                                // all processes
            0,                                // all threads
            WINEVENT_OUTOFCONTEXT);

        // optional: you could also do an initial EnumWindows pass here
        // to seed _windows with current top-level windows.
    }

    public static void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
        _windows.Clear();
        _proc = null;
    }

    private static void WinEventProc(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // basic sanity
        if (hwnd == IntPtr.Zero) return;
        if (idObject != OBJID_WINDOW || idChild != 0) return;
        if (!IsWindow(hwnd)) return;

        // only track true top-level windows
        if (GetAncestor(hwnd, GA_ROOT) != hwnd) return;

        switch (eventType)
        {
            case EVENT_OBJECT_CREATE:
            case EVENT_OBJECT_SHOW:
                OnWindowShownOrCreated(hwnd);
                break;

            case EVENT_OBJECT_HIDE:
            case EVENT_OBJECT_DESTROY:
                OnWindowHiddenOrDestroyed(hwnd);
                break;

            case EVENT_OBJECT_LOCATIONCHANGE:
                OnWindowMovedOrResized(hwnd);
                break;
        }
    }

    private static void OnWindowShownOrCreated(IntPtr hwnd)
    {
        if (!IsWindowVisible(hwnd)) return;
        if (GetWindowRect(hwnd, out RECT rect))
        {
            _windows[hwnd] = rect;
            // at this point you can raise your own event, log, etc.
        }
    }

    private static void OnWindowHiddenOrDestroyed(IntPtr hwnd)
    {
        _windows.Remove(hwnd);
        // raise event / log if needed
    }

    private static void OnWindowMovedOrResized(IntPtr hwnd)
    {
        if (!_windows.ContainsKey(hwnd)) return; // ignore ones we're not tracking
        if (!GetWindowRect(hwnd, out RECT rect)) return;
        _windows[hwnd] = rect;
        // raise event / log if needed
    }
}
```

### Key Features

This implementation:

- Uses one global hook that covers create/show/hide/destroy/location events in one SetWinEventHook call
- Filters to top‑level OBJID_WINDOW and visible windows, avoiding control spam
- Maintains an in‑memory `Dictionary<IntPtr, RECT>` that always reflects current known windows and their positions; your app can poll or expose events over this structure however you like

## Related Topics

- Show a concise C# program using SetWinEventHook for create and destroy events
- How to filter WinEvents for top level windows only
- Example WinEventProc callback that tracks window position changes
- Thread safety patterns for updating a shared window list in C# code
- How to get window process and thread IDs from a HWND in C# code
