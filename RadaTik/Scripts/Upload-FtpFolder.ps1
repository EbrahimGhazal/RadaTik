param(
    [Parameter(Mandatory = $true)][string]$LocalPath,
    [Parameter(Mandatory = $true)][string]$FtpBaseUri,
    [Parameter(Mandatory = $true)][string]$FtpUser,
    [Parameter(Mandatory = $true)][string]$FtpPassword,
    [string[]]$ExcludeNames = @('appsettings.Development.json')
)

$ErrorActionPreference = 'Stop'
$cred = New-Object System.Net.NetworkCredential($FtpUser, $FtpPassword)

function Invoke-Ftp([string]$uri, [string]$method) {
    $req = [System.Net.FtpWebRequest]::Create($uri)
    $req.Credentials = $cred
    $req.Method = $method
    $req.UseBinary = $true
    $req.UsePassive = $true
    $req.KeepAlive = $false
    try {
        $resp = $req.GetResponse()
        $resp.Close()
        return $true
    }
    catch {
        return $false
    }
}

function Ensure-FtpDirectory([string]$uri) {
    if (-not $uri.EndsWith('/')) { $uri += '/' }
    if (Invoke-Ftp -uri $uri -method ([System.Net.WebRequestMethods+Ftp]::MakeDirectory)) {
        Write-Host "MKDIR $uri"
    }
}

function Upload-File([string]$localFile, [string]$ftpUri) {
    $bytes = [System.IO.File]::ReadAllBytes($localFile)
    $req = [System.Net.FtpWebRequest]::Create($ftpUri)
    $req.Credentials = $cred
    $req.Method = [System.Net.WebRequestMethods+Ftp]::UploadFile
    $req.UseBinary = $true
    $req.UsePassive = $true
    $req.KeepAlive = $false
    $req.ContentLength = $bytes.Length
    $stream = $req.GetRequestStream()
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()
    $resp = $req.GetResponse()
    $resp.Close()
}

$localRoot = (Resolve-Path $LocalPath).Path.TrimEnd('\')
$ftpRoot = $FtpBaseUri.TrimEnd('/')

$files = Get-ChildItem -Path $localRoot -Recurse -File | Where-Object {
    $name = $_.Name
    -not ($ExcludeNames | Where-Object { $name -ieq $_ })
}

$dirs = Get-ChildItem -Path $localRoot -Recurse -Directory | Sort-Object { $_.FullName.Length }
Ensure-FtpDirectory $ftpRoot
foreach ($dir in $dirs) {
    $rel = $dir.FullName.Substring($localRoot.Length).TrimStart('\').Replace('\', '/')
    Ensure-FtpDirectory "$ftpRoot/$rel"
}

$i = 0
$total = $files.Count
foreach ($file in $files) {
    $i++
    $rel = $file.FullName.Substring($localRoot.Length).TrimStart('\').Replace('\', '/')
    $ftpUri = "$ftpRoot/$rel"
    Write-Host "[$i/$total] $rel"
    Upload-File -localFile $file.FullName -ftpUri $ftpUri
}

Write-Host "Done. Uploaded $total files."
