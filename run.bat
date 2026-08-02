@echo off
dotnet "%~dp0dist\Localcel_WinUI3.dll"
if errorlevel 1 pause
