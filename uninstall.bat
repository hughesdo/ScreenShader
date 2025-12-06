@echo off
setlocal

echo ========================================
echo   ScreenShader Uninstaller
echo ========================================
echo.

set "INSTALL_DIR=%LOCALAPPDATA%\ScreenShader"
set "STARTUP_SHORTCUT=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\ScreenShader.lnk"

echo This will remove ScreenShader from your system.
echo.
set /p confirm="Are you sure? (Y/N): "
if /i not "%confirm%"=="Y" (
    echo Uninstall cancelled.
    pause
    exit /b 0
)

echo.
echo [1/3] Stopping ScreenShader...
taskkill /F /IM ScreenShader.exe >nul 2>&1
echo       Done!

echo [2/3] Removing startup shortcut...
if exist "%STARTUP_SHORTCUT%" (
    del "%STARTUP_SHORTCUT%"
    echo       Shortcut removed!
) else (
    echo       No shortcut found.
)

echo [3/3] Removing install directory...
if exist "%INSTALL_DIR%" (
    rmdir /S /Q "%INSTALL_DIR%"
    echo       Directory removed!
) else (
    echo       No install directory found.
)

echo.
echo ========================================
echo   Uninstall Complete!
echo ========================================
echo.
echo   ScreenShader has been removed from your system.
echo.
pause

