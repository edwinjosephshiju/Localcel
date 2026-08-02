@echo off
dotnet "%~dp0dist\Localcel.dll"
if errorlevel 1 pause
