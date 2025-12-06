# Windows 11 24H2/25H2 Wallpaper Rendering Fix

## Overview

Starting with **Windows 11 version 24H2** (Build 26100+) and **25H2** (Build 26200+), Microsoft fundamentally changed how the desktop window hierarchy works. This broke all existing wallpaper injection techniques used by applications like Wallpaper Engine, Lively Wallpaper, and this ScreenShader application.

**This document explains the issue and the technical solution implemented.**

---

## The Problem

### Previous Windows Versions (Windows 10, Windows 11 23H2 and earlier)

The classic technique to render behind desktop icons involved:

1. Send `0x052C` message to `Progman` window to spawn a `WorkerW` sibling window
2. Find the `WorkerW` that sits behind `SHELLDLL_DefView`
3. Parent your rendering window to that `WorkerW`
4. Icons appear on top, your content renders behind

**Window Hierarchy (Old):**
```
Progman
├── SHELLDLL_DefView (icons)
WorkerW (sibling - wallpaper goes here)
```

### Windows 11 24H2/25H2 (Build 26100+)

Microsoft introduced the **"Raised Desktop with Layered ShellView"** architecture:

- `Progman` now has `WS_EX_NOREDIRECTIONBITMAP` style (no GDI content)
- `SHELLDLL_DefView` is now a `WS_EX_LAYERED` child window (draws icons with transparency)
- `WorkerW` is created as a **child of Progman**, not a sibling
- The old message (`0x052C`) no longer creates a usable WorkerW for wallpaper injection

**Window Hierarchy (New):**
```
Progman (WS_EX_NOREDIRECTIONBITMAP)
├── SHELLDLL_DefView (WS_EX_LAYERED - draws icons transparently)
├── WorkerW (child - renders actual wallpaper)
```

---

## The Solution

Based on Microsoft's guidance and Lively Wallpaper's implementation, the fix requires:

### 1. Detect the New Desktop Architecture

Check if `Progman` has the `WS_EX_NOREDIRECTIONBITMAP` extended style:

```csharp
const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

var progman = FindWindow("Progman", null);
var exStyle = GetWindowLong(progman, GWL_EXSTYLE);
bool isRaisedDesktop = (exStyle & WS_EX_NOREDIRECTIONBITMAP) != 0;
```

### 2. Create a Layered Child Window

Instead of parenting to WorkerW, create a **layered child window of Progman**:

```csharp
// Set as child window
SetWindowLong(handle, GWL_STYLE, 
    (GetWindowLong(handle, GWL_STYLE) | WS_CHILD) & ~WS_POPUP);

// Set parent to Progman
SetParent(handle, progman);

// Add WS_EX_LAYERED style
SetWindowLong(handle, GWL_EXSTYLE, 
    GetWindowLong(handle, GWL_EXSTYLE) | WS_EX_LAYERED);

// Set alpha to fully opaque (required for DX/GL presents)
SetLayeredWindowAttributes(handle, 0, 255, LWA_ALPHA);
```

### 3. Z-Order the Window Correctly

The window must be positioned:
- **Below** `SHELLDLL_DefView` (so icons appear on top)
- **Above** `WorkerW` (so it covers the static wallpaper)

```csharp
// Find SHELLDLL_DefView
var shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

// Position our window below it
SetWindowPos(handle, shellView, 0, 0, width, height, 
    SWP_NOACTIVATE | SWP_SHOWWINDOW);

// Find WorkerW child and push it below us
var workerW = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
if (workerW != IntPtr.Zero)
{
    SetWindowPos(workerW, handle, 0, 0, 0, 0, 
        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
}
```

---

## Technical Details

### Why `WS_EX_LAYERED` with Alpha = 255?

Microsoft's documentation states:
> "This window should likely be a `SetLayeredWindowAttributes(bAlpha=0xFF)` window so that you can do DX blt presents to it and not suffer performance issues."

Setting `LWA_ALPHA` with `bAlpha=255` (fully opaque) allows:
- DirectX and OpenGL rendering without compositing overhead
- Proper frame presentation without flickering
- Integration with the layered window compositor

### Why Not Use WorkerW Anymore?

In the new architecture, the child `WorkerW` under Progman is used by Windows itself to render the static wallpaper. Attempting to parent your window to it no longer works because:
1. It's positioned incorrectly in the z-order
2. The layered `SHELLDLL_DefView` draws on top of everything in Progman anyway

---

## Environment Tested

| Property | Value |
|----------|-------|
| **OS** | Windows 11 |
| **Version** | 25H2 |
| **Build** | 26200.7171 |
| **Graphics** | NVIDIA GeForce GTX 1080 Ti |
| **Framework** | .NET 8.0 |
| **OpenGL** | OpenTK 4.9.3 |

---

## References

- [Lively Wallpaper GitHub](https://github.com/rocksdanister/lively) - Windows 11 24H2 fix implementation
- Microsoft internal documentation (via Lively source code comments)
- [Wallpaper Engine Community](https://steamcommunity.com/app/431960/discussions/) - User reports of the issue

---

## Files Modified

- `WallpaperHelper.cs` - Contains `IsRaisedDesktopWithLayeredShellView()` detection and `SetWallpaperLayered()` implementation
- `wallpaperForm.cs` - Calls the appropriate wallpaper setup based on Windows version detection

