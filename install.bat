@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   ScreenShader Installer
echo   Windows 11 24H2/25H2 Compatible
echo ========================================
echo.

:: Check for .NET SDK
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK not found. Please install .NET 8.0 SDK.
    echo Download from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

:: Set install directory
set "INSTALL_DIR=%LOCALAPPDATA%\ScreenShader"
set "STARTUP_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"

echo [1/4] Building Release version...
dotnet build -c Release
if %errorlevel% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)
echo       Build successful!
echo.

echo [2/4] Creating install directory...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
echo       Install directory: %INSTALL_DIR%
echo.

echo [3/4] Copying files...
:: Copy main app files
xcopy /Y /Q "bin\Release\net8.0-windows\*.dll" "%INSTALL_DIR%\" >nul
xcopy /Y /Q "bin\Release\net8.0-windows\*.exe" "%INSTALL_DIR%\" >nul
xcopy /Y /Q "bin\Release\net8.0-windows\*.json" "%INSTALL_DIR%\" >nul
xcopy /Y /Q "bin\Release\net8.0-windows\*.deps.json" "%INSTALL_DIR%\" >nul
xcopy /Y /Q "bin\Release\net8.0-windows\*.runtimeconfig.json" "%INSTALL_DIR%\" >nul

:: Copy shader files
if not exist "%INSTALL_DIR%\diatribes_ShadersV2" mkdir "%INSTALL_DIR%\diatribes_ShadersV2"
xcopy /Y /Q "bin\Release\net8.0-windows\diatribes_ShadersV2\*" "%INSTALL_DIR%\diatribes_ShadersV2\" >nul

:: Copy reviewed shaders list
if exist "bin\Release\net8.0-windows\reviewed_working.txt" (
    xcopy /Y /Q "bin\Release\net8.0-windows\reviewed_working.txt" "%INSTALL_DIR%\" >nul
)
echo       Files copied!
echo.

echo [4/4] Creating startup shortcut...
:: Create VBS script to make shortcut (batch can't create shortcuts directly)
set "VBS_FILE=%TEMP%\create_shortcut.vbs"
(
echo Set oWS = WScript.CreateObject^("WScript.Shell"^)
echo sLinkFile = "%STARTUP_DIR%\ScreenShader.lnk"
echo Set oLink = oWS.CreateShortcut^(sLinkFile^)
echo oLink.TargetPath = "%INSTALL_DIR%\ScreenShader.exe"
echo oLink.Arguments = "--bottom"
echo oLink.WorkingDirectory = "%INSTALL_DIR%"
echo oLink.Description = "ScreenShader - Animated Shader Wallpaper"
echo oLink.Save
) > "%VBS_FILE%"
cscript //nologo "%VBS_FILE%"
del "%VBS_FILE%"
echo       Startup shortcut created!
echo.

:: Set environment variable for NVIDIA
echo [Optional] Setting NVIDIA threading optimization...
setx __GL_THREADED_OPTIMIZATIONS 0 >nul 2>&1
echo       Environment variable set!
echo.

echo ========================================
echo   Installation Complete!
echo ========================================
echo.
echo   Install location: %INSTALL_DIR%
echo   Startup shortcut: %STARTUP_DIR%\ScreenShader.lnk
echo.
echo   ScreenShader will now start automatically when Windows starts.
echo.
echo   To run now, press any key...
pause >nul

:: Launch the app
start "" "%INSTALL_DIR%\ScreenShader.exe" --bottom

echo.
echo   ScreenShader is now running! Check your system tray.
echo   Right-click tray icon for Pause/Exit options.
echo.
pause

