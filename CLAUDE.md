# CMS

Full-stack CMS over an existing, already-populated SQL Server database. Two apps under `src/`.

| Project | Stack | Port |
|---|---|---|
| `CMS.API` | .NET 9 Web API, controllers, Dapper (no EF) | 5000 |
| `CMS.API.Tests` | xUnit 2.9 + NSubstitute | — |
| `CMS.NG` | Angular 20 standalone + PrimeNG 20, Karma/Jasmine | 4200 |

`Controller → I{T}Repository → Dapper → SQL Server`. No service layer, no EF — the repository owns its
SQL. Angular's `app.ts` *is* the shell (no separate layout component); feature components are
standalone and lazy-routed. Live tables: AppRole, AppUser, Partner, CourseGroup, Course,
FeaturedPromoItem (a **custom** scheduling board, not the standard list/detail/form).

## Golden rules (apply on every task)

- **Never run `database/*.sql`.** It is reference DDL only; the `CMS` database already exists and is
  populated, and the scripts re-declare overlapping tables across files and will fail.
- **`pkid` is not always the primary key.** The auth tables (AppRole/AppUser/AppUserRole) key on an
  `nvarchar` string; most other tables do use `pkid`. Read the DDL per table — `spec/gotchas.md`.
- **The toolchain is pinned; bare scaffolding produces the wrong versions.** Don't delete
  `global.json`, don't `ng new` / `ng generate` with the global CLI, don't bump PrimeNG/theme
  packages — `spec/toolchain.md`.
- **Integration tests hit the *real* CMS database.** Never mutate/delete the seeded `Admin`/`User`
  rows; assert containment, not exact counts — `spec/testing.md`.
- **Row Audit and safe errors are mandatory on every feature.** Every repository write logs via the
  shared `IRowAuditWriter` on the **same conn/tx**; every detail/form page carries
  `<app-row-audit-badge>` (`pkid` = the numeric IDENTITY, not the route key; omitted on create).
  `GlobalExceptionMiddleware` returns one safe generic 500 — no per-controller try/catch for
  unexpected errors. Exact rules: `spec/cross-cutting.md` (authority).
- **Auth gates are invisible to controller unit tests** — `[Authorize]` is a pipeline filter. Test
  gate changes through `WebApplicationFactory` — `spec/testing.md`.

## Commands

```powershell
dotnet build CMS.sln
dotnet test                                   # full suite (needs SQLEXPRESS)
dotnet test --filter "Category!=Integration"  # DB-free; passes with SQL Server stopped
dotnet run --project src/CMS.API              # http://localhost:5000/swagger (Development only)
dotnet run --project src/CMS.API -lp http-docker  # same, but against the Docker DB — spec/docker-db.md

cd src/CMS.NG
npm start                                     # http://localhost:4200
npx ng test --watch=false --browsers=ChromeHeadless
npx ng build                                  # proves the prod environment.ts replacement compiles
```

## Reference docs — open the matching file before that kind of work

| When you are… | Read |
|---|---|
| Adding a table (file layout, routes, PrimeNG patterns) | `spec/code-gen.convention.md` — the authority |
| Navigating the codebase, picking a slice to copy, or the QR/inline-edit/shared extras | `spec/architecture.md` |
| Writing repository, controller, or Angular code | `spec/gotchas.md` — load-bearing guards |
| Following the Row Audit / exception-handling conventions | `spec/cross-cutting.md` — the authority |
| Writing or running tests | `spec/testing.md` |
| Scaffolding, installing deps, or upgrading | `spec/toolchain.md` |
| Running the DB in Docker instead of native SQLEXPRESS (opt-in) | `spec/docker-db.md` |
| Deploying to IIS | `DEPLOY-IIS.md` |
| Building a **custom** (non-CRUD) feature | `spec/custom/{Feature}/{Feature}.spec.md` + its `ui-*.spec.png` |
| Picking up known security debt | `TODOS.md` |

`spec/ui-sample-*.png` are **style reference only, not content.** `/crud` scaffolds a new table's
full slice and follows the rules above.
