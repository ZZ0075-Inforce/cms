# Local database via Docker (opt-in)

**This is an alternative, not the default.** `appsettings.json` still points at the native
`.\SQLEXPRESS`, and every existing workflow is untouched. The point is that a fresh machine can run
the whole environment without installing SQL Server.

## Why it exists

`CMS.bak` was taken on SQL Server 2025, and **SQL backups are forward-compatible only** — a machine
with 2022 or older can never restore it. Pinning SQL to 2025 in a container sidesteps that: the new
machine only needs Docker.

## Usage

```powershell
copy C:\workspace\bk\CMS.bak database\backup\    # data source — see database/backup/README.md
copy .env.example .env                           # sets the SA password
docker compose up -d                             # starts SQL; first run restores CMS automatically

dotnet run --project src/CMS.API -lp http-docker  # API against the container (-lp http stays native)
```

Point the tests at the container:

```powershell
$env:ConnectionStrings__CMS = "Server=localhost,1433;Database=CMS;User Id=sa;Password=LocalDev#Cms2026;TrustServerCertificate=True;Encrypt=False"
dotnet test
```

(`IntegrationFactAttribute` reads `ConnectionStrings__CMS`; unset, it falls back to the native default.)

Start over from the backup:

```powershell
docker compose down -v      # -v drops the volume, so the next up re-restores
docker compose up -d
```

## Services

| Service | Role |
|---|---|
| `sql` | SQL Server 2025 Express. Data lives in the named volume `mssql-data`; exposed on 1433. |
| `sql-init` | One-shot: waits for `sql` to be healthy, runs `database/docker/init-restore.sh` to restore `CMS`, then exits. |

`sql-init` skips the restore when `CMS` already exists, so repeating `docker compose up` is safe and
never wipes data.

## Load-bearing details (each one was hit for real)

- **Do not add `MSSQL_COLLATION`.** Matching the source instance's `Chinese_Taiwan_Stroke_CI_AS` is
  the obvious move and it **hangs the container forever**: startup stalls at
  `index restored for master.syspriorities`, `sa` logins fail with State 115, and the container stays
  unhealthy indefinitely. It is also unnecessary — collation travels inside the backup, so `CMS`
  comes back as `Chinese_Taiwan_Stroke_CI_AS` even though the instance is
  `SQL_Latin1_General_CP1_CI_AS` (verified: all 327 tests pass).
  The only side effect is that tempdb follows the instance and is Latin1. Nothing hits this today,
  but a query comparing a `#temp` string column directly against a `CMS` column would need an
  explicit `COLLATE`.

- **The logical file names are not the database name.** The backup carries `UComWeb` /
  `UComWeb_log`, not `CMS`, and `RESTORE ... MOVE` must use the logical names.
  `init-restore.sh` reads them from `RESTORE FILELISTONLY` rather than hard-coding them.

- **`sqlcmd` is not on the container's PATH** — use `/opt/mssql-tools18/bin/sqlcmd`, and **`-C` is
  mandatory** (tools18 forces encryption; the container serves a self-signed certificate).

- **Do not set the healthcheck `timeout` to 5s.** sqlcmd's cold start exceeds it and the container is
  wrongly marked unhealthy. It is 15s.

- **`.sh` files must stay LF.** This repo has `core.autocrlf=true`, so checkout would rewrite them to
  CRLF and the Linux container fails with `bad interpreter: No such file or directory` — an error
  that gives no hint of the real cause. Pinned by `*.sh text eol=lf` in `.gitattributes`.

- **`.gitignore` line 264 `Backup*/`** (from the dotnet template, meant for SSMS backup folders)
  is case-insensitive on Windows and swallows the whole `database/backup/` directory, README
  included. Git cannot re-include a file whose parent directory is excluded, so `!database/backup/`
  puts the directory back before `database/backup/*.bak` excludes just the backups.

- **The SA password here is a fixture, not a secret** — and it is literal on purpose.
  `LocalDev#Cms2026` guards a container whose contents are test data restored from `CMS.bak`, so
  there is nothing behind it worth guarding; this was decided 2026-07-17 rather than left to
  drift, and `.env.example` carries the full reasoning. **Note what the reason is NOT:** the
  compose file publishes the port with no bind address, so the container listens on `0.0.0.0` and
  **is** reachable from the LAN and any tailnet this machine joins (verified 2026-07-17). The test
  data is the only thing that makes that acceptable — put real data in here and the decision is
  void. The literal is copied to `.env.example` (its definition — compose reads the variable from
  there), the manual connection string above in this file, and the `http-docker` profile in
  `src/CMS.API/Properties/launchSettings.json`; `git grep LocalDev#Cms2026` before changing it
  rather than trusting this list or a count, and change every hit. The `launchSettings.json` copy
  is the one that has no choice: it cannot expand `${...}`, and it cannot be annotated in place
  either —
  **the SDK rejects JSON comments in `launchSettings.json` by silently discarding the entire
  profile** (A/B verified on SDK 9.0.314 and 10.0.300 — the run still starts, so `-lp http-docker`
  would quietly fall back to the native SQLEXPRESS with no hint in the error text). A `"//"`
  *property* is fine — valid JSON, the SDK ignores it, and schemastore sets no
  `additionalProperties: false`. Anything that guards something real belongs in `.env`, which is
  gitignored.

## Scope

SQL only. **The API and the frontend still run natively** — deliberately, so the existing workflow
does not change. IIS deployment is unaffected; it still follows `DEPLOY-IIS.md`.
