@echo off
title NUML FYP Match Tracker
cls
echo ===================================================================
echo     National University of Modern Languages (NUML) Islamabad
echo               FYP Match Tracker - Web Application
echo ===================================================================
echo.
echo Checking .NET SDK installation...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK is not installed or not in PATH.
    echo Please download and install .NET 10 SDK from:
    echo https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

echo .NET SDK detected.
echo Starting application server on http://localhost:5242 ...
echo.
echo [Tip] Press Ctrl+C in this window anytime to stop the server.
echo.

start http://localhost:5242
dotnet run

pause
