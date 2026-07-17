#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    One-time IIS prep for the CMS demo: modules, ARR proxy, folders, app pools, sites, database.

.DESCRIPTION
    Everything you need before deploy.ps1 will work. Idempotent — safe to re-run.

      1. Enable the IIS role/feature (client OS and Server OS both handled)
      2. Install the ASP.NET Core 9 Hosting Bundle, URL Rewrite, and ARR if missing
      3. Enable the ARR reverse proxy at server level   <-- the step everyone forgets
      4. Create C:\VHome\CMS\{API,NG}
      5. Create app pools CMS.API.Pool + CMS.NG.Pool (No Managed Code)
      6. Create IIS sites CMS (:80 -> NG) and CMS.API (:5001 -> API), stopping "Default Web Site"
         if it is holding port 80
      7. Grant the app-pool identities read access to their folders
      8. Give the API app pool identity a SQL login on the existing CMS database

    The CMS database is assumed to EXIST already, with its schema and runtime data in place.
    In particular the API reads its JWT signing key from the SysConfig 'appConfig' row at
    startup — if that row is missing, login returns 500 no matter how well IIS is configured.

    Steps 1-7 run ON the IIS server (in-process when $remote is Localhost, over PS Remoting
    otherwise). Step 8 runs from THIS machine against $SqlServer.

.PARAMETER SkipSqlAccess
    Do NOT touch SQL Server. Only correct when the API connects with SQL authentication —
    which deploy.ps1 does not do: its connection string is hard-coded to Trusted_Connection=True,
    so the site connects as "IIS APPPOOL\CMS.API.Pool" and that principal MUST be granted on the
    CMS database. Skipping the grant leaves every API call failing with a bare 500 and
    "Cannot open database "CMS" ... Login failed for user 'IIS APPPOOL\CMS.API.Pool'" (error
    4060) buried in C:\VHome\CMS\API\logs\stdout*.log. Step 8 was opt-in until 2026-07-17 and
    this is exactly the hole people fell into, so it now runs by default.

.PARAMETER Credential
    PSCredential for a remote IIS server. Omit to use your current Windows identity.

.EXAMPLE
    .\setup-iis.ps1                   # full prep, including the SQL grant (what you want)
    .\setup-iis.ps1 -SkipSqlAccess    # IIS only — SQL auth, or the grant already exists
#>
param(
    [switch]$SkipSqlAccess,
    [System.Management.Automation.PSCredential]$Credential
)

$ErrorActionPreference = 'Stop'

# ============================ CONFIG ========================================
# Keep these in sync with deploy.ps1 — the two scripts share the same topology.
$remote      = "Localhost"                        # <-- e.g. "CMSWEB01"

$apiPool     = "CMS.API.Pool"
$ngPool      = "CMS.NG.Pool"
$apiSite     = "CMS.API"
$ngSite      = "CMS"
$sitePathApi = "C:\VHome\CMS\API"
$sitePathNg  = "C:\VHome\CMS\NG"
$apiPort     = 5001
$ngPort      = 80                                 # the SPA is the site people actually visit, so
                                                  # it gets the bare http://<host>/ . IIS ships
                                                  # "Default Web Site" on :80 — the script stops it.

# Step 8 (the SQL grant) only. The database itself is assumed to exist already.
$SqlServer   = ".\SQLEXPRESS"
$SqlDb       = "CMS"
# ===========================================================================

$isLocal = $remote -in @('Localhost', 'localhost', '.', '127.0.0.1', $env:COMPUTERNAME)

$remoteParams = @{}
if (-not $isLocal) {
    $remoteParams.ComputerName = $remote
    if ($Credential) { $remoteParams.Credential = $Credential }
}

function Write-Step { param($msg) Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-OK   { param($msg) Write-Host "   OK $msg"   -ForegroundColor Green }

function Invoke-OnServer {
    param([scriptblock]$ScriptBlock, [object[]]$ArgumentList = @())
    if ($isLocal) {
        & $ScriptBlock @ArgumentList
    } else {
        Invoke-Command @remoteParams -ScriptBlock $ScriptBlock -ArgumentList $ArgumentList
    }
}

Write-Host "IIS server: $remote  ($(if ($isLocal) { 'local' } else { 'remote' }))" -ForegroundColor DarkGray

# ─────────────────────────────────────────────────────────────────────
# 1-7. Everything that happens ON the IIS server
# ─────────────────────────────────────────────────────────────────────
$serverSetup = {
    param($cfg)

    $ErrorActionPreference = 'Stop'
    function Say  { param($m) Write-Host "   $m" }
    function Good { param($m) Write-Host "   OK $m" -ForegroundColor Green }

    # ---- 1. IIS role/features ------------------------------------------------
    # ProductType 1 = workstation (Windows 10/11), 2/3 = Server.
    $isWorkstation = (Get-CimInstance Win32_OperatingSystem).ProductType -eq 1
    if (-not (Test-Path "$env:windir\system32\inetsrv\w3wp.exe")) {
        Say "installing IIS..."
        if ($isWorkstation) {
            $features = @(
                'IIS-WebServerRole', 'IIS-WebServer', 'IIS-CommonHttpFeatures', 'IIS-StaticContent',
                'IIS-DefaultDocument', 'IIS-HttpErrors', 'IIS-HttpLogging', 'IIS-RequestFiltering',
                'IIS-Security', 'IIS-ManagementConsole', 'IIS-IIS6ManagementCompatibility',
                'IIS-Metabase'
            )
            Enable-WindowsOptionalFeature -Online -FeatureName $features -All -NoRestart | Out-Null
        } else {
            Install-WindowsFeature -Name Web-Server, Web-Static-Content, Web-Default-Doc, `
                Web-Http-Errors, Web-Http-Logging, Web-Filtering, Web-Mgmt-Console, `
                Web-Scripting-Tools | Out-Null
        }
        Good "IIS installed"
    } else {
        Good "IIS already installed"
    }
    Import-Module WebAdministration -ErrorAction Stop

    # ---- 2. The three module prereqs ----------------------------------------
    function Install-Msi {
        param($Name, $Url, $Test)
        if (& $Test) { Good "$Name already installed"; return }
        Say "installing $Name ..."
        $ext = if ($Url -match '\.exe($|\?)') { 'exe' } else { 'msi' }
        $file = Join-Path $env:TEMP ("cms-prereq-" + [IO.Path]::GetFileNameWithoutExtension($Url) + ".$ext")
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri $Url -OutFile $file -UseBasicParsing
        } catch {
            throw "$Name is missing and the download failed ($Url). Install it by hand, then re-run.`n$_"
        }
        if ($ext -eq 'exe') {
            $p = Start-Process $file -ArgumentList '/quiet', '/norestart' -Wait -PassThru
        } else {
            $p = Start-Process msiexec.exe -ArgumentList '/i', "`"$file`"", '/quiet', '/norestart' -Wait -PassThru
        }
        Remove-Item $file -Force -ErrorAction SilentlyContinue
        # 3010 = success, reboot required
        if ($p.ExitCode -notin @(0, 3010)) { throw "$Name installer exited $($p.ExitCode)" }
        if (-not (& $Test)) { throw "$Name installed but still not detected — reboot and re-run." }
        Good "$Name installed"
    }

    # ASP.NET Core Hosting Bundle — without it IIS cannot run the API at all (500.19 / 502.5).
    # Detection: the bundle registers AspNetCoreModuleV2 as an IIS *global module*. Do NOT probe
    # %windir%\system32\inetsrv\aspnetcorev2.dll — current bundles install the DLL under
    # "%ProgramFiles%\IIS\Asp.Net Core Module\V2\", so that check reports a perfectly good
    # install as missing and sends you chasing a phantom reboot.
    Install-Msi -Name 'ASP.NET Core 9 Hosting Bundle' `
        -Url 'https://aka.ms/dotnet/9.0/dotnet-hosting-win.exe' `
        -Test {
            [bool](Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' `
                       -Filter 'system.webServer/globalModules/add' |
                   Where-Object { $_.name -eq 'AspNetCoreModuleV2' })
        }

    # URL Rewrite — needed for BOTH the /api proxy rule and the SPA deep-link fallback.
    Install-Msi -Name 'IIS URL Rewrite 2.1' `
        -Url 'https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi' `
        -Test { Test-Path 'HKLM:\SOFTWARE\Microsoft\IIS Extensions\URL Rewrite' }

    # ARR — the piece that lets a rewrite rule target an ABSOLUTE url (i.e. actually proxy).
    # Must be installed AFTER URL Rewrite.
    Install-Msi -Name 'Application Request Routing 3.0' `
        -Url 'https://download.microsoft.com/download/E/9/8/E9849D6A-020E-47E4-9FD0-A023E99B54EB/requestRouter_amd64.msi' `
        -Test { Test-Path 'HKLM:\SOFTWARE\Microsoft\IIS Extensions\Application Request Routing' }

    # ---- 3. Enable the ARR proxy at SERVER level ----------------------------
    # Installing ARR is not enough: the proxy is off by default, and while it is off every
    # /api/* request 404s with no useful error. This is the classic 30-minute bug.
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' `
        -Filter 'system.webServer/proxy' -Name 'enabled' -Value 'True'
    Good "ARR reverse proxy enabled at server level"

    # ---- 4. Folders ----------------------------------------------------------
    foreach ($p in @($cfg.SitePathApi, $cfg.SitePathNg, (Join-Path $cfg.SitePathApi 'logs'))) {
        if (-not (Test-Path $p)) { New-Item -ItemType Directory -Path $p -Force | Out-Null }
    }
    Good "folders ready: $($cfg.SitePathNg), $($cfg.SitePathApi)"

    # ---- 5. App pools --------------------------------------------------------
    # managedRuntimeVersion '' == "No Managed Code". ASP.NET Core runs inside
    # AspNetCoreModuleV2, not the .NET Framework CLR, so loading the CLR here is wrong.
    #
    # Stick to the *-WebConfiguration* cmdlets here. Under PowerShell 7 WebAdministration loads
    # through WinPSCompatSession, and anything that depends on the IIS: PSDrive does not survive
    # the trip: the drive stays in the hidden 5.1 session ("A drive with the name 'IIS' does not
    # exist"), Test-Path against it silently returns $false, and provider-backed cmdlets like
    # Get-WebAppPool are not even proxied into the session.
    $poolFilter = { param($Pool) "system.applicationHost/applicationPools/add[@name='$Pool']" }
    function Set-PoolProperty {
        param($Pool, $Name, $Value)
        Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' `
            -Filter "system.applicationHost/applicationPools/add[@name='$Pool']" `
            -Name $Name -Value $Value
    }
    foreach ($pool in @($cfg.ApiPool, $cfg.NgPool)) {
        $existed = [bool](Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' `
                              -Filter (& $poolFilter $pool) -ErrorAction SilentlyContinue)
        if (-not $existed) { New-WebAppPool -Name $pool | Out-Null }

        # Applied unconditionally: a pool created by an earlier, half-failed run exists with the
        # IIS defaults (.NET CLR v4.0), which is exactly what we must not have.
        Set-PoolProperty $pool 'managedRuntimeVersion' ''
        Set-PoolProperty $pool 'enable32BitAppOnWin64' $false
        Set-PoolProperty $pool 'startMode'             'AlwaysRunning'

        Good "app pool $pool $(if ($existed) { 'reconfigured' } else { 'created' }) (No Managed Code)"
    }

    # ---- 6. Sites ------------------------------------------------------------
    $sites = @(
        @{ Name = $cfg.ApiSite; Port = $cfg.ApiPort; Path = $cfg.SitePathApi; Pool = $cfg.ApiPool },
        @{ Name = $cfg.NgSite;  Port = $cfg.NgPort;  Path = $cfg.SitePathNg;  Pool = $cfg.NgPool }
    )
    foreach ($s in $sites) {
        if (Get-Website -Name $s.Name -ErrorAction SilentlyContinue) {
            Good "site $($s.Name) already exists (:$($s.Port))"
            continue
        }

        # Two IIS sites may hold the same binding, but only one can RUN on it. Anything already
        # listening on our port has to be stopped or the new site won't start.
        $clash = Get-Website | Where-Object {
            $_.Name -ne $s.Name -and
            ($_.bindings.Collection.bindingInformation -match ":$($s.Port):")
        }
        foreach ($c in $clash) {
            if ($c.Name -eq 'Default Web Site') {
                # The IIS-out-of-the-box placeholder site. Stopping it is safe and reversible;
                # we do NOT delete it. Restore with: Start-Website -Name 'Default Web Site'
                if ($c.State -ne 'Stopped') { Stop-Website -Name $c.Name }
                Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' `
                    -Filter "system.applicationHost/sites/site[@name='$($c.Name)']" `
                    -Name serverAutoStart -Value $false
                Good "stopped 'Default Web Site' so :$($s.Port) is free (Start-Website to undo)"
            } else {
                throw "Port $($s.Port) is already bound by site '$($c.Name)'. Stop that site, or change the port in BOTH setup-iis.ps1 and deploy.ps1."
            }
        }

        New-Website -Name $s.Name -Port $s.Port -PhysicalPath $s.Path -ApplicationPool $s.Pool | Out-Null
        Good "site $($s.Name) created (:$($s.Port) -> $($s.Path))"
    }

    # ---- 7. Filesystem rights for the pool identities ------------------------
    # The site runs as "IIS APPPOOL\<pool>", not as you. Without this the API can read
    # nothing and you get a 500.19 that says almost nothing useful.
    foreach ($s in $sites) {
        icacls $s.Path /grant "IIS APPPOOL\$($s.Pool):(OI)(CI)(RX)" /T /Q | Out-Null
    }
    # The API needs WRITE on its logs folder (stdoutLogEnabled="true").
    icacls (Join-Path $cfg.SitePathApi 'logs') /grant "IIS APPPOOL\$($cfg.ApiPool):(OI)(CI)(M)" /T /Q | Out-Null
    Good "app-pool identities granted read (+ write on logs)"
}

$cfg = @{
    ApiPool = $apiPool; NgPool = $ngPool
    ApiSite = $apiSite; NgSite = $ngSite
    SitePathApi = $sitePathApi; SitePathNg = $sitePathNg
    ApiPort = $apiPort; NgPort = $ngPort
}

Write-Step "Preparing IIS on $remote ..."
Invoke-OnServer -ScriptBlock $serverSetup -ArgumentList $cfg
Write-OK "IIS ready"

# ─────────────────────────────────────────────────────────────────────
# 8. SQL login for the app pool identity — runs from THIS machine, not on the IIS server
# ─────────────────────────────────────────────────────────────────────
if (-not $SkipSqlAccess) {
    Write-Step "Granting the API app pool access to [$SqlDb] on $SqlServer ..."

    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        throw "sqlcmd not found. Install the SQL Server command line tools, or run the GRANT in SSMS by hand."
    }

    sqlcmd -S $SqlServer -E -C -b -Q "IF DB_ID(N'$SqlDb') IS NULL RAISERROR('Database [$SqlDb] does not exist on $SqlServer', 16, 1);"
    if ($LASTEXITCODE -ne 0) {
        throw "Database [$SqlDb] not found on $SqlServer. This script assumes it already exists — create it, apply the schema, and make sure the SysConfig 'appConfig' row is there."
    }

    # With a Windows-auth connection string the API connects as its app pool identity, NOT as you.
    # On a LOCAL SQL Server that identity is "IIS APPPOOL\CMS.API.Pool". If SQL lives on ANOTHER
    # machine, the pool authenticates as the IIS machine account instead — e.g. "DOMAIN\CMSWEB01$"
    # — so set $poolLogin accordingly.
    $poolLogin = "IIS APPPOOL\$apiPool"

    # db_datareader + db_datawriter, NOT db_owner: the API only ever runs DML (every repository is
    # hand-written SELECT/INSERT/UPDATE/DELETE through Dapper — no DDL, no stored procedures), and
    # the CMS database already exists with its schema in place. db_owner would additionally let a
    # compromised site drop the very tables it reads.
    $grant = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$poolLogin')
    CREATE LOGIN [$poolLogin] FROM WINDOWS;
USE [$SqlDb];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$poolLogin')
    CREATE USER [$poolLogin] FOR LOGIN [$poolLogin];
ALTER ROLE db_datareader ADD MEMBER [$poolLogin];
ALTER ROLE db_datawriter ADD MEMBER [$poolLogin];
"@
    sqlcmd -S $SqlServer -E -C -b -Q $grant
    if ($LASTEXITCODE -ne 0) { throw "Could not grant SQL access to $poolLogin" }
    Write-OK "$poolLogin is db_datareader + db_datawriter on [$SqlDb]"
}

$hostName = if ($isLocal) { 'localhost' } else { $remote }
$ngUrl    = if ($ngPort -eq 80) { "http://$hostName/" } else { "http://${hostName}:$ngPort/" }
Write-Host ""
Write-Host "==================== DONE ====================" -ForegroundColor Green
Write-Host "  Angular site : $ngUrl  -> $sitePathNg"
Write-Host "  API site     : http://${hostName}:$apiPort/swagger  -> $sitePathApi"
Write-Host "  App pools    : $ngPool, $apiPool"
if ($SkipSqlAccess) {
    Write-Host "  SQL login    : SKIPPED by -SkipSqlAccess. deploy.ps1 uses Trusted_Connection=True," -ForegroundColor Yellow
    Write-Host "                 so unless 'IIS APPPOOL\$apiPool' is already granted on [$SqlDb]," -ForegroundColor Yellow
    Write-Host "                 every API call will fail with a bare 500 (SQL error 4060)." -ForegroundColor Yellow
} else {
    Write-Host "  SQL login    : IIS APPPOOL\$apiPool -> db_datareader + db_datawriter on [$SqlDb]"
}
Write-Host "============================================="
Write-Host "Both folders are empty until you run:  .\deploy.ps1"
