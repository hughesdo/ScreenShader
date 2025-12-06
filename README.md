# ScreenShader

A Windows desktop wallpaper application that renders animated GLSL shaders behind your desktop icons.

![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue)
![Windows 11](https://img.shields.io/badge/Windows%2011-24H2%2F25H2-green)
![OpenGL](https://img.shields.io/badge/OpenGL-OpenTK%204.9-orange)

## Features

- **Animated shader wallpapers** - Renders Shadertoy-style GLSL fragment shaders as your desktop background
- **Windows 11 24H2/25H2 compatible** - Implements the new layered desktop architecture fix
- **System tray integration** - Minimize to tray with pause/play and exit controls
- **Auto shader rotation** - Automatically switches shaders every minute
- **318 curated shaders** - Pre-reviewed collection from diatribes' Shadertoy shaders

## Requirements

- Windows 10/11 (tested on Windows 11 25H2 Build 26200)
- .NET 8.0 Runtime
- OpenGL 3.3+ compatible GPU

## Usage

### Run as Wallpaper

```powershell
# Set environment variable to avoid NVIDIA threading issues
$env:__GL_THREADED_OPTIMIZATIONS=0

# Run with logging
dotnet run -- --log --bottom
```

### Command Line Options

| Option | Description |
|--------|-------------|
| `--log` | Enable logging to `screenshader.log` |
| `--bottom` | Position window behind desktop icons (wallpaper mode) |
| `--shader <name>` | Load a specific shader by name |
| `--test` | Run batch shader testing |

### System Tray

When running, the app minimizes to the system tray with:
- **⏸ Pause / ▶ Play** - Toggle shader animation
- **❌ Exit** - Close the application

## Shader Reviewer

A separate utility for manually reviewing shaders:

```powershell
cd ShaderReviewer
$env:__GL_THREADED_OPTIMIZATIONS=0
dotnet run -- "path\to\shaders"
```

**Controls:**
- `G` - Mark shader as Good/Working
- `B` - Mark shader as Bad/Broken
- `→` - Skip to next
- `←` - Go back
- `ESC` - Save and exit

## Windows 11 24H2/25H2 Compatibility

This application implements a fix for the desktop window hierarchy changes introduced in Windows 11 24H2/25H2. See [WINDOWS11_WALLPAPER_FIX.md](WINDOWS11_WALLPAPER_FIX.md) for technical details.

**Key changes in Windows 11 24H2+:**
- Progman window now uses `WS_EX_NOREDIRECTIONBITMAP`
- SHELLDLL_DefView is a layered child window
- WorkerW is a child of Progman (not a sibling)

**Solution:**
- Create `WS_EX_LAYERED` child window of Progman
- Set `SetLayeredWindowAttributes(alpha=255)` for GPU rendering
- Z-order below SHELLDLL_DefView but above WorkerW

## Project Structure

```
ScreenShader/
├── program.cs              # Entry point
├── wallpaperForm.cs        # Main form with OpenGL rendering
├── WallpaperHelper.cs      # Windows 11 wallpaper fix
├── ShaderLoader.cs         # Shader loading and management
├── ShaderSources.cs        # Vertex/fragment shader templates
├── ShaderTester.cs         # Batch shader testing
├── Logger.cs               # Logging utility
├── reviewed_working.txt    # 318 curated working shaders
├── reviewed_broken.txt     # 208 broken shaders (excluded)
├── diatribes_ShadersV2/    # Shader files
├── ShaderReviewer/         # Shader review utility
└── WINDOWS11_WALLPAPER_FIX.md
```

## Installation

### Quick Install (Recommended)

Run the installer to build, install, and set up auto-start:

```powershell
install.bat
```

This will:
1. Build the Release version
2. Install to `%LOCALAPPDATA%\ScreenShader`
3. Create a startup shortcut (runs automatically on Windows login)
4. Set NVIDIA threading optimization
5. Launch ScreenShader

### Uninstall

```powershell
uninstall.bat
```

### Manual Build

```powershell
# Debug build
dotnet build

# Release build
dotnet build -c Release
```

## Credits

- Shaders from [diatribes on Shadertoy](https://www.shadertoy.com/user/diatribes)
- Windows 11 24H2 fix based on [Lively Wallpaper](https://github.com/rocksdanister/lively)
- Built with [OpenTK](https://opentk.net/)

## License

MIT License

