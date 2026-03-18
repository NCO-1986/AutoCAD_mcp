@echo off
setlocal

set "BUNDLE_DST=%APPDATA%\Autodesk\ApplicationPlugins\AutoCADMCPPlugin.bundle"

echo ============================================
echo  AutoCAD MCP Plugin - Uninstaller
echo ============================================
echo.

if exist "%BUNDLE_DST%" (
    rmdir /s /q "%BUNDLE_DST%"
    echo  Plugin removed from:
    echo    %BUNDLE_DST%
    echo.
    echo  Restart AutoCAD to complete uninstallation.
) else (
    echo  Plugin is not installed.
)

echo ============================================
echo.
pause
