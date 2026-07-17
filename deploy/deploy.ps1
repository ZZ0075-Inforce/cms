#Requires -Version 5.1
<#
.SYNOPSIS
    Build and deploy the CMS API and/or Angular frontend to an IIS server.

.DESCRIPTION
    Publishes the .NET 9 API and builds the Angular SPA, then copies both to their IIS
    site folders and cycles the API app pool. Creates either app pool if it does not
    exist yet, so a wiped pool is not a broken deploy.

    Works against the LOCAL machine ($remote = "Localhost", the default) or a REMOTE IIS
    server over PowerShell Remoting + admin shares — see the CONFIG block.

    Run setup-iis.ps1 ONCE first: it installs the IIS modules, enables the ARR proxy, and
    creates the folders, app pools and sites this script deploys into.

    One-time setup for a REMOTE server:
      On the IIS server (as admin):  Enable-PSRemoting -Force
      On this machine (as admin):    Set-Item WSMan:\localhost\Client\TrustedHosts -Value "<SERVER>" -Force

.PARAMETER ApiOnly
    Deploy API only; skip the Angular build and copy.

.PARAMETER NgOnly
    Deploy Angular only; skip the API build and copy.

.PARAMETER SkipBuild
    Skip the build steps and re-copy the existing artifacts. Useful after a failed copy.

.PARAMETER Credential
    PSCredential for the remote server. Omit to use your current Windows identity.

.EXAMPLE
    .\deploy.ps1                   # full deploy
    .\deploy.ps1 -ApiOnly          # API only
    .\deploy.ps1 -NgOnly           # Angular only
    .\deploy.ps1 -SkipBuild        # re-copy the last build, no rebuild
#>
param(
    [switch]$ApiOnly,
    [switch]$NgOnly,
    [switch]$SkipBuild,
    [System.Management.Automation.PSCredential]$Credential
)

$ErrorActionPreference = 'Stop'

# ============================ CONFIG ========================================
# The IIS server. "Localhost" deploys to IIS on THIS machine (no PS Remoting, no file
# shares). Change it to your remote IIS server name to deploy over the network.
$remote      = "Localhost"                        # <-- e.g. "CMSWEB01"

# The CMS project repo (the app source — NOT this course repo).
$ProjectRoot = "C:\workspace\20260714\cms"

# IIS app pools. Created automatically if missing.
$apiPool     = "CMS.API.Pool"
$ngPool      = "CMS.NG.Pool"

# Physical paths ON THE IIS SERVER, and the site ports. Must match setup-iis.ps1.
$sitePathApi = "C:\VHome\CMS\API"
$sitePathNg  = "C:\VHome\CMS\NG"
$apiPort     = 5001                               # IIS site "CMS.API"
$ngPort      = 80                                 # IIS site "CMS"  <- open this in the browser

# Stamped into the API's web.config at deploy time (never committed to the CMS repo).
$aspnetEnv   = "Development"                      # see CMS.API\web.config.template for why
$connString  = 'Server=.\SQLEXPRESS;Database=CMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True'
# ===========================================================================

$root       = $PSScriptRoot
$publishDir = Join-Path $root 'publish\API'
$apiSrc     = Join-Path $ProjectRoot 'src\CMS.API'
$ngSrc      = Join-Path $ProjectRoot 'src\CMS.NG'
$ngDist     = Join-Path $ngSrc 'dist\CMS.NG\browser'
$apiTemplate = Join-Path $root 'CMS.API\web.config.template'
$ngTemplate  = Join-Path $root 'CMS.NG\web.config.template'

# Local or remote? Everything below branches on this one flag.
$isLocal = $remote -in @('Localhost', 'localhost', '.', '127.0.0.1', $env:COMPUTERNAME)

if ($isLocal) {
    # Deploy straight into the local site folders.
    $dstApi = $sitePathApi
    $dstNg  = $sitePathNg
} else {
    # Deploy over the admin share: C:\VHome\CMS\API -> \\SERVER\C$\VHome\CMS\API
    $dstApi = "\\$remote\" + ($sitePathApi -replace ':', '$')
    $dstNg  = "\\$remote\" + ($sitePathNg  -replace ':', '$')
}

$remoteParams = @{}
if (-not $isLocal) {
    $remoteParams.ComputerName = $remote
    if ($Credential) { $remoteParams.Credential = $Credential }
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()

function Write-Step { param($msg) Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-OK   { param($msg) Write-Host "   OK $msg"   -ForegroundColor Green }
function Write-Fail { param($msg) Write-Host "   FAIL $msg" -ForegroundColor Red }

# Run a scriptblock on the IIS server — in-process when local, over PS Remoting when not.
function Invoke-OnServer {
    param([scriptblock]$ScriptBlock, [object[]]$ArgumentList = @())
    if ($isLocal) {
        & $ScriptBlock @ArgumentList
    } else {
        Invoke-Command @remoteParams -ScriptBlock $ScriptBlock -ArgumentList $ArgumentList
    }
}

# Stamp a web.config template and write it into the build output.
# XML-escape the values: a connection string with '&' or '<' would otherwise corrupt the file.
function Write-WebConfig {
    param([string]$Template, [string]$Destination, [hashtable]$Tokens)
    if (-not (Test-Path $Template)) { throw "Template not found: $Template" }
    $xml = Get-Content $Template -Raw
    foreach ($key in $Tokens.Keys) {
        $xml = $xml.Replace("{{$key}}", [System.Security.SecurityElement]::Escape([string]$Tokens[$key]))
    }
    Set-Content -Path $Destination -Value $xml -Encoding UTF8
}

# Create the app pool if it is not there yet. No Managed Code is REQUIRED for ASP.NET Core —
# the app runs in AspNetCoreModuleV2, not the .NET Framework CLR.
#
# Stick to the *-WebConfiguration* cmdlets: under PowerShell 7 WebAdministration loads through
# WinPSCompatSession, and anything IIS: PSDrive-backed does not survive the trip. The drive stays
# in the hidden 5.1 session ("A drive with the name 'IIS' does not exist"), Test-Path against it
# silently returns $false, and Get-WebAppPool is not even proxied into the session.
$EnsureAppPool = {
    param($pool)
    Import-Module WebAdministration -ErrorAction Stop
    $filter = "system.applicationHost/applicationPools/add[@name='$pool']"
    if (Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter $filter -ErrorAction SilentlyContinue) {
        return 'exists'
    }
    New-WebAppPool -Name $pool | Out-Null
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter $filter `
        -Name managedRuntimeVersion -Value ''                                       # "No Managed Code"
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter $filter `
        -Name enable32BitAppOnWin64 -Value $false
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter $filter `
        -Name startMode -Value 'AlwaysRunning'
    return 'created'
}

# Make sure the deploy target folder exists on the server (first deploy, or someone deleted it).
$EnsureFolder = {
    param($path)
    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
}

Write-Host "Target: $remote  ($(if ($isLocal) { 'local IIS' } else { 'remote IIS over PS Remoting' }))" -ForegroundColor DarkGray
Write-Host "Source: $ProjectRoot" -ForegroundColor DarkGray

# ─────────────────────────────────────────────────────────────────────
# API
# ─────────────────────────────────────────────────────────────────────
if (-not $NgOnly) {

    # 1. Build
    if (-not $SkipBuild) {
        Write-Step "Building API (Release)..."
        if (-not (Test-Path $apiSrc)) { Write-Fail "API source not found: $apiSrc (check `$ProjectRoot)"; exit 1 }
        Push-Location $apiSrc
        try {
            dotnet publish -c Release -o $publishDir --nologo
            if ($LASTEXITCODE -ne 0) { Write-Fail "dotnet publish failed (exit $LASTEXITCODE)"; exit 1 }
        } finally {
            Pop-Location
        }
        Write-OK "API build complete -> $publishDir"
    } else {
        Write-Step "Skipping API build (using existing artifacts in $publishDir)"
        if (-not (Test-Path "$publishDir\CMS.API.dll")) {
            Write-Fail "No published API found at $publishDir — run once without -SkipBuild first"
            exit 1
        }
    }

    # 2. Stamp web.config over the one dotnet publish generated (adds the env vars)
    Write-Step "Stamping API web.config (env=$aspnetEnv)..."
    Write-WebConfig -Template $apiTemplate -Destination "$publishDir\web.config" -Tokens @{
        ASPNETCORE_ENVIRONMENT = $aspnetEnv
        CONNECTION_STRING      = $connString
    }
    # ANCM writes stdout here but will not create the folder itself.
    New-Item -ItemType Directory -Path "$publishDir\logs" -Force | Out-Null
    Write-OK "web.config written (ConnectionStrings__CMS injected)"

    # 3. App pool: create if missing, then stop it so the DLLs unlock
    Write-Step "Ensuring app pool $apiPool on $remote..."
    $state = Invoke-OnServer -ScriptBlock $EnsureAppPool -ArgumentList $apiPool
    Write-OK "$apiPool $state"

    Invoke-OnServer -ScriptBlock $EnsureFolder -ArgumentList $sitePathApi

    Write-Step "Stopping $apiPool on $remote..."
    Invoke-OnServer -ScriptBlock {
        param($pool)
        Import-Module WebAdministration
        if ((Get-WebAppPoolState -Name $pool).Value -ne 'Stopped') {
            Stop-WebAppPool -Name $pool
            $timeout = 30
            $elapsed = 0
            while ((Get-WebAppPoolState -Name $pool).Value -ne 'Stopped') {
                if ($elapsed -ge $timeout) { throw "Timed out waiting for $pool to stop" }
                Start-Sleep -Seconds 1
                $elapsed++
            }
        }
    } -ArgumentList $apiPool
    Write-OK "$apiPool stopped"

    # 4. Clear + copy (always restart the pool, even if the copy blows up)
    try {
        Write-Step "Clearing $dstApi ..."
        Remove-Item "$dstApi\*" -Recurse -Force -ErrorAction SilentlyContinue
        Write-OK "Cleared"

        Write-Step "Copying API files to $dstApi ..."
        Copy-Item "$publishDir\*" -Destination $dstApi -Recurse -Force
        Write-OK "API files deployed"
    } catch {
        Write-Fail "API copy failed: $_"
        throw          # fall through to finally so the pool always restarts
    } finally {
        Write-Step "Starting $apiPool on $remote..."
        Invoke-OnServer -ScriptBlock {
            param($pool)
            Import-Module WebAdministration
            Start-WebAppPool -Name $pool
        } -ArgumentList $apiPool
        Write-OK "$apiPool started"
    }
}

# ─────────────────────────────────────────────────────────────────────
# Angular
# ─────────────────────────────────────────────────────────────────────
if (-not $ApiOnly) {

    # 1. Build
    if (-not $SkipBuild) {
        Write-Step "Building Angular (production)..."
        if (-not (Test-Path $ngSrc)) { Write-Fail "Angular source not found: $ngSrc (check `$ProjectRoot)"; exit 1 }
        Push-Location $ngSrc
        try {
            if (-not (Test-Path (Join-Path $ngSrc 'node_modules'))) {
                Write-Host "   node_modules missing — running npm ci (first build only)..." -ForegroundColor DarkGray
                npm ci
                if ($LASTEXITCODE -ne 0) { Write-Fail "npm ci failed (exit $LASTEXITCODE)"; exit 1 }
            }
            npm run build          # angular.json defaultConfiguration is "production"
            if ($LASTEXITCODE -ne 0) { Write-Fail "npm run build failed (exit $LASTEXITCODE)"; exit 1 }
        } finally {
            Pop-Location
        }
        Write-OK "Angular build complete"
    } else {
        Write-Step "Skipping Angular build (using existing dist)"
    }

    if (-not (Test-Path $ngDist)) {
        Write-Fail "Angular build output not found at $ngDist — run without -SkipBuild first"
        exit 1
    }

    # 2. Stamp web.config into the dist (ARR proxy target + SPA fallback)
    Write-Step "Stamping Angular web.config (/api -> http://localhost:$apiPort)..."
    Write-WebConfig -Template $ngTemplate -Destination "$ngDist\web.config" -Tokens @{
        API_ORIGIN = "http://localhost:$apiPort"
    }
    Write-OK "web.config written (reverse proxy + deep-link fallback)"

    # 3. App pool: create if missing. No stop needed — static files, no DLL lock.
    Write-Step "Ensuring app pool $ngPool on $remote..."
    $state = Invoke-OnServer -ScriptBlock $EnsureAppPool -ArgumentList $ngPool
    Write-OK "$ngPool $state"

    Invoke-OnServer -ScriptBlock $EnsureFolder -ArgumentList $sitePathNg

    # 4. Clear + copy
    Write-Step "Clearing $dstNg ..."
    Remove-Item "$dstNg\*" -Recurse -Force -ErrorAction SilentlyContinue
    Write-OK "Cleared"

    Write-Step "Copying Angular files to $dstNg ..."
    Copy-Item "$ngDist\*" -Destination $dstNg -Recurse -Force
    Write-OK "Angular files deployed"
}

# ─────────────────────────────────────────────────────────────────────
$sw.Stop()
$elapsed  = $sw.Elapsed.ToString('mm\:ss')
$hostName = if ($isLocal) { 'localhost' } else { $remote }
$ngUrl    = if ($ngPort -eq 80) { "http://$hostName/" } else { "http://${hostName}:$ngPort/" }
Write-Host "`n  DONE  Deployment complete in $elapsed" -ForegroundColor Magenta
Write-Host "  Angular: $ngUrl"                             -ForegroundColor DarkGray
Write-Host "  API:     http://${hostName}:$apiPort/swagger" -ForegroundColor DarkGray
Write-Host ""
