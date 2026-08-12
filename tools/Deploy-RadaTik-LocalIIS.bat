@echo off
:: Double-click this file and accept the UAC prompt to deploy RadaTik to local IIS.
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "Start-Process powershell.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"D:\SkyBeam\MyApp\RadTik\RadTik_20260225_Full_01\tools\Deploy-RadaTik-LocalIIS.ps1\"' -Verb RunAs -Wait"
echo.
echo Exit code: %ERRORLEVEL%
pause
