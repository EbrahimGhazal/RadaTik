#Requires -RunAsAdministrator
<#
  Deploy RadaTik to local IIS on this machine.
#>
$ErrorActionPreference = 'Stop'
$deployLog = 'D:\SkyBeam\MyApp\RadTik\RadTik_20260225_Full_01\tools\Deploy-RadaTik-LocalIIS.log'
Start-Transcript -Path $deployLog -Force | Out-Null
try {

$source = 'D:\SkyBeam\MyApp\RadTik\RadaTik_LocalIIS'
$siteName = 'RadaTik'
$appPoolName = 'RadaTik'
$sitePath = 'D:\SkyBeam\MyApp\RadTik\RadaTik_LocalIIS'
$port = 8088
$bindHost = '*'

Write-Host "=== RadaTik local IIS deploy ===" -ForegroundColor Cyan

if (-not (Test-Path "$source\RadaTik.dll")) {
  throw "Site folder missing: $source\RadaTik.dll — prepare publish files first."
}

# Ensure AspNetCore Module V2 exists (Hosting Bundle)
$ancm = Join-Path $env:windir 'System32\inetsrv\aspnetcorev2.dll'
if (-not (Test-Path $ancm)) {
  Write-Host "ASP.NET Core Module V2 not found. Installing .NET 9 Hosting Bundle..." -ForegroundColor Yellow
  $winget = Get-Command winget -ErrorAction SilentlyContinue
  $localInstaller = 'D:\SkyBeam\MyApp\RadTik\RadTik_20260225_Full_01\tools\dotnet-hosting-9.0.17-win.exe'
  if (Test-Path $localInstaller) {
    Write-Host "Installing from local Hosting Bundle: $localInstaller"
    Start-Process -FilePath $localInstaller -ArgumentList '/install','/quiet','/norestart' -Wait
  } elseif ($winget) {
    winget install --id Microsoft.DotNet.HostingBundle.9 -e --accept-package-agreements --accept-source-agreements
  } else {
    $tmp = Join-Path $env:TEMP 'dotnet-hosting-9.0.exe'
    $url = 'https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/9.0.17/dotnet-hosting-9.0.17-win.exe'
    Write-Host "Downloading Hosting Bundle from $url"
    Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing
    Start-Process -FilePath $tmp -ArgumentList '/install','/quiet','/norestart' -Wait
  }
  if (-not (Test-Path $ancm)) {
    Write-Host "WARNING: aspnetcorev2.dll still missing. Restart may be required after Hosting Bundle install." -ForegroundColor Yellow
  }
}

Import-Module WebAdministration

# Stop site/pool if exist
if (Test-Path "IIS:\Sites\$siteName") {
  Stop-Website -Name $siteName -ErrorAction SilentlyContinue
}
if (Test-Path "IIS:\AppPools\$appPoolName") {
  Stop-WebAppPool -Name $appPoolName -ErrorAction SilentlyContinue
}

# Site files are already prepared at $sitePath
Write-Host "Using site files at $sitePath"

# Local DB connection (overwrite Production for this laptop)
$prodJson = Join-Path $sitePath 'appsettings.Production.json'
$localProd = @{
  ConnectionStrings = @{
    MyDBConnection = 'Server=.;Database=RadaTikDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;'
  }
  RadaTik = @{ InsecureHttp = $true }
  Logging = @{
    LogLevel = @{
      Default = 'Information'
      'Microsoft.AspNetCore' = 'Warning'
    }
  }
  AllowedHosts = '*'
} | ConvertTo-Json -Depth 6
Set-Content -Path $prodJson -Value $localProd -Encoding UTF8

# Ensure writable folders
foreach ($d in @('logs','wwwroot\uploads')) {
  New-Item -ItemType Directory -Path (Join-Path $sitePath $d) -Force | Out-Null
}

# App pool
if (-not (Test-Path "IIS:\AppPools\$appPoolName")) {
  New-WebAppPool -Name $appPoolName | Out-Null
}
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name processModel.identityType -Value 'ApplicationPoolIdentity'

# Site
$binding = "*:${port}:"
if (-not (Test-Path "IIS:\Sites\$siteName")) {
  New-Website -Name $siteName -PhysicalPath $sitePath -ApplicationPool $appPoolName -Port $port -Force | Out-Null
} else {
  Set-ItemProperty "IIS:\Sites\$siteName" -Name physicalPath -Value $sitePath
  Set-ItemProperty "IIS:\Sites\$siteName" -Name applicationPool -Value $appPoolName
}

# Environment variable
$envPath = "IIS:\Sites\$siteName"
# Clear and set ASPNETCORE_ENVIRONMENT via applicationHost config
$siteConfig = Get-WebConfiguration -Filter "system.webServer/aspNetCore" -PSPath "IIS:\Sites\$siteName" -ErrorAction SilentlyContinue
# Use appcmd for env vars reliability
& "$env:windir\System32\inetsrv\appcmd.exe" set config "$siteName" -section:system.webServer/aspNetCore /+"environmentVariables.[name='ASPNETCORE_ENVIRONMENT',value='Production']" /commit:apphost 2>$null
& "$env:windir\System32\inetsrv\appcmd.exe" set config "$siteName" -section:system.webServer/aspNetCore /-environmentVariables.[name='ASPNETCORE_ENVIRONMENT'] /commit:apphost 2>$null
& "$env:windir\System32\inetsrv\appcmd.exe" set config "$siteName" -section:system.webServer/aspNetCore /+"environmentVariables.[name='ASPNETCORE_ENVIRONMENT',value='Production']" /commit:apphost

# Permissions for app pool identity
$aclUser = "IIS AppPool\$appPoolName"
icacls $sitePath /grant "${aclUser}:(OI)(CI)M" /T | Out-Null

# Start
Start-WebAppPool -Name $appPoolName
Start-Website -Name $siteName

# Optional: unlock URL rewrite not required
iisreset /noforce | Out-Null

Start-Sleep -Seconds 2
$state = (Get-Website -Name $siteName).State
Write-Host ""
Write-Host "Site:      $siteName ($state)" -ForegroundColor Green
Write-Host "Path:      $sitePath"
Write-Host "AppPool:   $appPoolName (No Managed Code)"
Write-Host "URL:       http://localhost:$port/"
Write-Host "Login:     http://localhost:$port/Account/Login"
Write-Host "Public:    http://localhost:$port/RadaTik"
Write-Host "Logs:      $sitePath\logs"
Write-Host ""
try {
  $r = Invoke-WebRequest -Uri "http://localhost:$port/" -UseBasicParsing -TimeoutSec 20
  Write-Host "HTTP $($r.StatusCode) from http://localhost:$port/" -ForegroundColor Green
} catch {
  Write-Host "Smoke request failed: $($_.Exception.Message)" -ForegroundColor Yellow
  Write-Host "Check $sitePath\logs\stdout_*.log if site does not load."
}

} catch {
  Write-Host "DEPLOY FAILED: $($_.Exception.Message)" -ForegroundColor Red
  throw
} finally {
  Stop-Transcript | Out-Null
}
