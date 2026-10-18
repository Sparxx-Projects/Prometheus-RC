@echo off
title Prometheus Taskforce GCS v2.0 - NieR 9S Edition
echo ========================================================
echo    PROMETHEUS TASKFORCE - GROUND CONTROL STATION
echo    Iniciando GCS v2.0 [NieR: Automata 9S Protocol]...
echo ========================================================

:: 1. Desbloquear archivos por si acaso
powershell -NoProfile -Command "Unblock-File -Path '%~dp0*.exe' -ErrorAction SilentlyContinue; Unblock-File -Path '%~dp0Taskforce_GCS\bin\Release\net8.0-windows\win-x64\*' -ErrorAction SilentlyContinue" >nul 2>&1

:: 2. Iniciar mediante el motor oficial Microsoft .NET (Bypasses Windows 11 Smart App Control)
if exist "%~dp0Taskforce_GCS\bin\Release\net8.0-windows\win-x64\Taskforce_GCS.dll" (
    cd /d "%~dp0Taskforce_GCS\bin\Release\net8.0-windows\win-x64"
    start "" dotnet Taskforce_GCS.dll
    exit
)

:: 3. Fallback en caso de no tener el SDK instalado
if exist "%~dp0Taskforce_GCS_v2.0_NieR_9S.exe" (
    cd /d "%~dp0"
    start "" "%~dp0Taskforce_GCS_v2.0_NieR_9S.exe"
    exit
)
