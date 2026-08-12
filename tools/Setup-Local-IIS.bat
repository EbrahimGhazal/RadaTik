@echo off
chcp 65001 >nul
title RadaTik - Local IIS Setup
echo ========================================
echo  RadaTik - Local IIS Setup
echo ========================================
echo.
echo Site files: D:\SkyBeam\MyApp\RadTik\RadaTik_LocalIIS
echo URL after setup: http://localhost:8088
echo.
echo This will:
echo  - Install .NET 9 Hosting Bundle (if missing)
echo  - Create IIS App Pool + Site "RadaTik"
echo.
echo IMPORTANT: Click YES on the UAC prompt.
echo.
pause
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"D:\SkyBeam\MyApp\RadTik\RadTik_20260225_Full_01\tools\Deploy-RadaTik-LocalIIS.ps1\"' -Verb RunAs -Wait"
echo.
echo ----- Deploy log -----
type "D:\SkyBeam\MyApp\RadTik\RadTik_20260225_Full_01\tools\Deploy-RadaTik-LocalIIS.log" 2>nul
echo.
pause
