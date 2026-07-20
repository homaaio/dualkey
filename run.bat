@echo off
echo ========================================
echo  Starting DualKey
echo ========================================

:: Check if executable exists
if not exist "build\DualKey.exe" (
    echo [ERROR] DualKey.exe not found.
    echo Please run build.bat first.
    pause
    exit /b 1
)

:: Request administrator privileges for joystick hiding
net session >nul 2>&1
if errorlevel 1 (
    echo [INFO] Requesting administrator privileges...
    PowerShell -Command "Start-Process 'build\DualKey.exe' -Verb RunAs"
) else (
    start build\DualKey.exe
)

echo.
echo [INFO] DualKey is running.
echo [INFO] Web interface: http://localhost:8080
