# CMS

Full-stack CMS over an existing SQL Server database. Two apps under `src/`:

| Project | Stack | Port |
|---|---|---|
| `CMS.API` | .NET 9 Web API, controllers, Dapper (no EF), Swagger at `/swagger` | 5000 |
| `CMS.API.Tests` | xUnit 2.9 + NSubstitute | — |
| `CMS.NG` | Angular 20 standalone + PrimeNG 20, Karma/Jasmine | 4200 |

`database/*.sql` is **reference DDL only** — the `CMS` database already exists and is populated.
Do not run those scripts; they re-declare overlapping tables across files and will fail.

`spec/code-gen.convention.md` is the authority on file layout, route shapes, and PrimeNG patterns.
Follow it when adding a table. `spec/ui-sample-*.png` are **style reference only, not content.**

## Commands

```powershell
dotnet build CMS.sln
dotnet test                                   # full suite (needs SQLEXPRESS)
dotnet test --filter "Category!=Integration"  # DB-free; passes with SQL Server stopped
dotnet run --project src/CMS.API              # http://localhost:5000/swagger

cd src/CMS.NG
npm start                                     # http://localhost:4200
npx ng test --watch=false --browsers=ChromeHeadless
npx ng build                                  # proves the prod environment.ts replacement compiles
```

## Architecture

One vertical slice exists end to end — **AppRole CRUD** (list / view / add / edit / delete, plus the
AppRole↔AppUser n-n). Every further table should copy its shape.

**API — request flows `Controller → I{T}Repository → Dapper → SQL Server`.** There is no service layer
and no EF; the repository *is* the data layer and owns its SQL.

```
CMS.API/
  Program.cs                    DI, CORS, Swagger. DapperConfig.Register() runs before any query.
  Data/                         IDbConnectionFactory (returns DbConnection, so OpenAsync exists),
                                SqlConnectionFactory, DapperConfig, TypeHandlers/{DateOnly,TimeOnly}
  Models/                       AppRole (response) · AppRoleRequest (write) · AppRoleQuery (search)
  Models/Lookups/               slim {Id,Name} shapes for FK dropdowns
  Repositories/                 I{T}Repository + {T}Repository — all SQL lives here
  Controllers/                  AppRolesController, LookupsController
  Infrastructure/               SqlErrorNumbers (2627/2601 duplicate key, 547 FK violation)
```

`DateOnly`/`TimeOnly` handlers are registered even though AppRole uses neither —
`Microsoft.Data.SqlClient` cannot bind those types natively, so the next table with a `date` column
(e.g. `Course.ScheduleOn`) would throw at runtime without them.

**Angular — `app.ts` *is* the shell** (sidebar nav model in the class, markup in `app.html`); there is
no separate layout component, per the convention. Feature components are standalone and lazy-routed.

```
CMS.NG/src/
  environments/                 environment.ts → '/api' (prod) · .development.ts → localhost:5000
  app/core/models/              typed API shapes
  app/core/services/            AppRoleService, LookupService — the only HttpClient callers
  app/features/app-roles/       {app-role-list, app-role-detail, app-role-form}/
```

Path aliases in `tsconfig.json`: `@env`, `@core/*`, `@features/*`. The `@env` import resolves per
build via `fileReplacements` in `angular.json` — that is what makes "no dev proxy" work.

## Toolchain is pinned — bare scaffolding commands produce the wrong thing

- **`global.json` pins the SDK to 9.0.314.** The machine default `dotnet` is **SDK 10**; without the
  pin, `dotnet new webapi` emits a *net10.0 Minimal API* project — wrong framework, wrong style.
  Do not delete `global.json`.
- **The global Angular CLI is v22.** Scaffold only via `npx @angular/cli@20.3.32`; a bare `ng new`
  or `ng generate` from the global CLI targets Angular 22.
- **PrimeNG 20 pairs with `@primeuix/themes@1.x`.** Installing `@primeuix/themes@latest` silently
  pulls the PrimeNG 21 theme line. PrimeNG 20 also has **no** `primeng/resources/**` CSS — theming is
  the styled-theme engine (`providePrimeNG({ theme: { preset: Aura } })` in `app.config.ts`) plus
  `primeicons`.
- **Node v26 is "Unsupported"** per the Angular CLI (it wants 20.19 / 22.12 / 24.x). Everything builds
  and tests green today; if the build starts misbehaving, Node 24 LTS is the fix.

## Schema: `RoleId` is the key, `pkid` is not

`AppRole`, `AppUser` and `AppUserRole` each carry a `pkid int IDENTITY` that is **not** the primary
key and has **no PK or UNIQUE constraint** — the database never promises it is unique. The real keys
are the `nvarchar(200)` strings `AppRole.RoleId` / `AppUser.UserId`, and `AppUserRole` has a composite
PK of `(UserId, RoleId)`. So:

- Routes key on the string: `GET|DELETE /api/app-roles/{id}`, **no `:int` constraint**; the Angular
  service `encodeURIComponent`s it. `pkid` is SELECTed for display (主代碼) only.
- `RoleId` is **immutable** — never in an UPDATE `SET` list. The FK has no `ON UPDATE CASCADE`, so a
  rename would orphan `AppUserRole` rows. This is why 角色代碼 is disabled in the edit form.
- `RoleId` is validated against `^[A-Za-z0-9_\-]+$` on both ends, because it becomes a URL path
  segment and Kestrel does not round-trip `%2F`.

**Do not generalise this.** Most other tables (Course, Partner, …) *do* use `pkid` as a genuine
IDENTITY primary key. Read the DDL per table.

## Traps already hit — keep these guards

- **`Program.cs` deliberately omits `app.UseHttpsRedirection()`.** The template ships it; it 307s every
  `:5000` call to `https://localhost:5001` (not listening) and silently breaks CORS preflight. There is
  no dev proxy — the Angular app calls `http://localhost:5000/api` directly, so API-side CORS is
  load-bearing, not optional.
- **Route order:** `app-roles/new` must be declared **before** `app-roles/:id`, or `/app-roles/new`
  resolves to the detail page for a role named "new". A string PK removes the `:int` constraint that
  would otherwise mask this.
- **Forms: use `form.getRawValue()`, never `form.value`.** `value` omits *disabled* controls, so in
  edit mode `roleId` would drop out of the PUT body and every update would 404. A unit test guards it.
- **LIKE filters escape `\ % _ [` and use `ESCAPE '\'`** — otherwise a keyword of `%` matches every row.
- **`.Distinct()` the incoming `UserIds`** before the n-n reinsert; `PK_AppUserRole(UserId, RoleId)`
  throws 2627 on a duplicate in the payload.
- **xUnit here is v2** — there is no `Assert.Skip` / `SkipUnless` (that is v3). Use the
  `[IntegrationFact]` / `[IntegrationTheory]` attributes in `CMS.API.Tests/Infrastructure/`, which set
  `Skip` at discovery time from a probed `DatabaseProbe`.

## Deviations from `code-gen.convention.md` (intentional)

- n-n lists are `List<string>`, not the convention's `List<int>` — `AppUserRole` keys are `nvarchar(200)`.
- `Description` stays optional (`string?`): the DB column is `nvarchar(400) NULL` and the PNGs are
  style-only, even though the mockup paints a red `*` on 描述.

## Deferred, by decision — not oversight

**No authentication and no `RowAudit` writes.** `RowAudit` requires a `UserName`; with no login in
scope there is no real current user, and a placeholder like `"system"` would put fake data in an audit
table. `spec/sample1.spec.md` *does* describe RowAudit logging, so this is a chosen deviation.
When auth lands, inject an `IAuditWriter` into the repositories and call it **inside** the transaction
`AppRoleRepository` already opens for the AppUserRole n-n sync.

## Tests hit the real database

Integration tests run real SQL against the real `CMS` database — mocking Dapper would not catch a wrong
join column or a broken `ESCAPE` clause. They are `[Trait("Category","Integration")]`, seed their own
`TEST_`-prefixed rows, and the fixture sweeps `WHERE RoleId LIKE 'TEST\_%' ESCAPE '\'` at both start
and end so a killed run self-heals.

Because `CMS` is the *real* database: **never mutate or delete the seeded `Admin` / `User` rows, and
assert containment — never an exact row count.** Isolation is deliberately *not* `TransactionScope`:
the repository owns its own `SqlTransaction` for n-n atomicity, and an ambient transaction around it
either throws or escalates to MSDTC.
