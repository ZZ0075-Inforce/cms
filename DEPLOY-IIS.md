# CMS — IIS Deployment Guide

How the CMS app is deployed to an on-prem IIS server. Copied into the project by
`copy-to-project.ps1`; the scripts live in `deploy\`.

## Environment

| Item | Value |
|------|-------|
| Host | `Localhost` by default — set `$remote` in both scripts to your IIS server |
| Web Server | IIS 10 |
| Angular site | `CMS` — port **80**, physical path `C:\VHome\CMS\NG` |
| API site | `CMS.API` — port **5001**, physical path `C:\VHome\CMS\API` |
| Angular app pool | `CMS.NG.Pool` (No Managed Code) |
| API app pool | `CMS.API.Pool` (No Managed Code) |
| Angular URL | http://localhost/ |
| API URL (Swagger) | http://localhost:5001/swagger |
| Database | `CMS` on `.\SQLEXPRESS` — **must already exist** (Windows auth, as the API app pool identity) |

## Topology — why two sites and a proxy

```
Browser ──▶ IIS site "CMS" :80    (C:\VHome\CMS\NG — the Angular build)
                │
                ├─ /api/*  ──[URL Rewrite + ARR proxy]──▶ http://localhost:5001/api/*
                │                                              │
                │                                    IIS site "CMS.API" :5001
                │                                    (AspNetCoreModuleV2, in-process)
                │                                              │
                │                                              ▼
                └─ anything else ──▶ index.html            SQL Server [CMS]
                   (Angular deep-link fallback)
```

The Angular production build hardcodes `apiBaseUrl: '/api'` (`src/CMS.NG/src/environments/environment.ts`),
and `Program.cs` only registers a CORS policy in Development. Both are deliberate: production is
meant to be **same-origin**. The ARR proxy is what makes that true on IIS — the browser only ever
talks to port 80, so there is no cross-origin request to allow. It is the same arrangement as the
nginx `/api` proxy in the Azure demo, and it means **the CMS source needs no changes to deploy**.

## Prerequisite: the database

The `CMS` database is **assumed to exist already**, with its schema and runtime data in place.
This kit deploys the app; it does not build the database.

One row is worth checking before you blame IIS for a failed login: the API reads its **JWT signing
key from `SysConfig.appConfig`** at startup. If that row is missing, `/api/Auth/login` returns 500
no matter how well the sites are configured.

## One-Time Setup

Run from an **elevated** PowerShell. `setup-iis.ps1` is idempotent — re-running it is safe.

```powershell
cd C:\dev\cms\deploy
.\setup-iis.ps1
```

It installs IIS, the **ASP.NET Core 9 Hosting Bundle**, **URL Rewrite** and **ARR**; enables the
ARR proxy at server level; creates the folders, app pools and both sites; grants the pool
identities filesystem rights; and grants the API pool identity access to the `CMS` database.

Because the `CMS` site takes **port 80**, the script **stops IIS's stock `Default Web Site`**,
which ships bound to that port. It is stopped, not deleted — `Start-Website -Name 'Default Web Site'`
brings it back (though the two will then compete for port 80). If any *other* site holds port 80,
the script stops with an error rather than guessing.

The last step creates a SQL login for `IIS APPPOOL\CMS.API.Pool` and adds it to `db_datareader` +
`db_datawriter` on `CMS` (the API only ever runs DML, so it never needs `db_owner`). This is **not
optional**: `deploy.ps1` hard-codes `Trusted_Connection=True`, so the site always connects as its
app pool identity rather than as you. `-SkipSqlAccess` opts out, and is only correct if the API
connects with SQL auth or the grant already exists.

> If SQL Server lives on a **different machine** from IIS, the pool authenticates as the IIS
> *machine account* (e.g. `DOMAIN\CMSWEB01$`), not `IIS APPPOOL\...`. Adjust `$poolLogin` in
> `setup-iis.ps1`.

### For a remote IIS server

Set `$remote` in both scripts, then:

```powershell
# on the IIS server, as admin:
Enable-PSRemoting -Force

# on your dev machine, as admin, once:
Set-Item WSMan:\localhost\Client\TrustedHosts -Value "CMSWEB01" -Force
```

Your Windows identity needs **administrator rights on the IIS server** — the scripts control app
pools and copy over the `C$` admin share. If it doesn't, pass `-Credential (Get-Credential)`.

## Deploying

```powershell
cd C:\dev\cms\deploy

.\deploy.ps1              # full deploy — API + Angular
.\deploy.ps1 -ApiOnly     # API only
.\deploy.ps1 -NgOnly      # Angular only
.\deploy.ps1 -SkipBuild   # re-copy the last build artifacts without rebuilding
```

## What the Script Does

### API

1. `dotnet publish -c Release -o deploy\publish\API`
2. **Stamps `web.config`** over the one the SDK generated, injecting `ASPNETCORE_ENVIRONMENT` and
   `ConnectionStrings__CMS` as `<environmentVariables>` (from `CMS.API\web.config.template`)
3. **Creates `CMS.API.Pool` if it doesn't exist** (No Managed Code)
4. Stops the pool and waits up to 30 s for it to actually stop — otherwise the DLLs are locked
5. Clears `C:\VHome\CMS\API\*`, copies the publish output in
6. Restarts the pool — in a `finally`, so a failed copy never leaves the site down

### Angular

1. `npm run build` (`angular.json` defaults to the production configuration) → `dist\CMS.NG\browser`
2. **Stamps `web.config`** into the dist with the ARR proxy rule and SPA fallback
   (from `CMS.NG\web.config.template`)
3. **Creates `CMS.NG.Pool` if it doesn't exist**
4. Clears `C:\VHome\CMS\NG\*`, copies the dist in

The Angular pool is **not** stopped — IIS serves static files with no DLL lock.

## Configuration is stamped, not committed

Neither `web.config` lives in the source tree. `deploy.ps1` fills the placeholders in the two
templates at deploy time:

| Template | Placeholder | Filled with |
|---|---|---|
| `CMS.API\web.config.template` | `{{ASPNETCORE_ENVIRONMENT}}` | `$aspnetEnv` |
| | `{{CONNECTION_STRING}}` | `$connString` |
| `CMS.NG\web.config.template` | `{{API_ORIGIN}}` | `http://localhost:$apiPort` |

So the connection string is never in the repo, and repointing the SPA at a different API is a
config edit, not a rebuild.

> **`ASPNETCORE_ENVIRONMENT` is `Development`, on purpose.** `Program.cs` calls `UseHsts()` +
> `UseHttpsRedirection()` **only** when `IsProduction()` — on an HTTP-only IIS binding a
> Production API would 307-redirect every call to `https://` and the SPA would break. Development
> also keeps Swagger available as a smoke test. A real staging box should use **`Staging`**
> (neither the HTTPS redirect nor Swagger/dev-CORS) **plus an HTTPS binding**.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `/api/*` returns **404**, SPA loads fine | The ARR server proxy is off. `Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter system.webServer/proxy -Name enabled -Value True` — this is what `setup-iis.ps1` step 3 does. |
| `/api/*` returns **502.3** | The `CMS.API` site is down. Check http://localhost:5001/swagger directly, and `C:\VHome\CMS\API\logs\stdout*.log`. |
| API returns **500.19** | Config error — usually URL Rewrite not installed, or the pool identity can't read `C:\VHome\CMS\API`. |
| API returns **500.30 / 502.5** | ASP.NET Core Hosting Bundle missing, or `arguments=".\CMS.API.dll"` doesn't match the published DLL name. |
| Every API call **307-redirects to https** | `ASPNETCORE_ENVIRONMENT` is `Production` on an HTTP-only site. Set it to `Development` or `Staging`. |
| Login returns **500** | The database has no `SysConfig.appConfig` row — the JWT signing key is read from it at runtime. |
| API **500** on any data call, incl. login | The app pool identity has no SQL access. The site runs as `IIS APPPOOL\CMS.API.Pool`, not as you. Look in `C:\VHome\CMS\API\logs\stdout*.log` for `Cannot open database "CMS" ... Login failed for user 'IIS APPPOOL\CMS.API.Pool'` (error 4060) — re-run `setup-iis.ps1` (step 8 grants it; it was opt-in via `-GrantSqlAccess` before 2026-07-17, which is how a box ends up in this state). |
| **F5 on a deep link → 404** | The SPA fallback rewrite is missing. Confirm `web.config` reached `C:\VHome\CMS\NG\` and URL Rewrite is installed. |
| Deployed, but the browser shows the **old app** | Hard-refresh. `index.html` is served no-cache by the stamped `web.config`; a stale copy pins the old hashed bundle names. |
| `dotnet publish` fails | Run it by hand in `src\CMS.API`. |
| `npm run build` fails | Run it by hand in `src\CMS.NG`. `deploy.ps1` runs `npm ci` automatically only when `node_modules` is absent. |
| Angular build output not found | Don't use `-SkipBuild` before a successful build has run. |
| `Access is denied` on `Invoke-Command` | Run PowerShell as admin, or pass `-Credential`. |
| `The client cannot connect to the destination` | Do the remote one-time setup above (`Enable-PSRemoting` / `TrustedHosts`). |
| Site `CMS` won't start / port 80 in use | Another site or process owns port 80. `setup-iis.ps1` stops `Default Web Site` automatically, but not anything else — find it with `Get-Website`, or `netstat -ano \| findstr :80`. |
| Port 5001 already bound | Change the port in **both** `setup-iis.ps1` and `deploy.ps1`. |
| Need `Default Web Site` back | `Start-Website -Name 'Default Web Site'` (it was stopped, not deleted — but it will fight `CMS` for port 80). |
