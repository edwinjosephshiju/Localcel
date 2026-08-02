@echo off
title Localcel Launcher
echo Launching Localcel WinUI 3 Application...
if exist "%~dp0dist\Localcel_WinUI3.exe" (
    start "" "%~dp0dist\Localcel_WinUI3.exe"
) else if exist "%~dp0dist\Localcel_WinUI3.dll" (
    dotnet "%~dp0dist\Localcel_WinUI3.dll"
) else (
    echo Error: Published files not found in dist directory.
    pause
)
