@echo off
title Localcel Launcher
echo Launching Localcel WinUI 3 Application...
if exist "%~dp0dist\Localcel.exe" (
    start "" "%~dp0dist\Localcel.exe"
) else if exist "%~dp0dist\Localcel.dll" (
    dotnet "%~dp0dist\Localcel.dll"
) else (
    echo Error: Published files not found in dist directory.
    pause
)
