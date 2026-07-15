# CMS

Full-stack CMS over an existing, already-populated SQL Server database. Two apps under `src/`:

| Project | Stack | Port |
|---|---|---|
| `CMS.API` | .NET 9 Web API, controllers, Dapper (no EF), Swagger at `/swagger` | 5000 |
| `CMS.API.Tests` | xUnit 2.9 + NSubstitute | — |
| `CMS.NG` | Angular 20 standalone + PrimeNG 20, Karma/Jasmine | 4200 |

Request flow is `Controller → I{T}Repository → Dapper → SQL Server`: no service layer, no EF — the
repository owns its SQL. Angular's `app.ts` *is* the shell (no separate layout component); feature
components are standalone and lazy-routed. Live tables: AppRole, AppUser, Partner, CourseGroup, Course,
FeaturedPromoItem (a **custom** scheduling board under 首頁 Home, not the standard list/detail/form —
see `spec/architecture.md` § Beyond the standard CRUD slice).

## Golden rules (apply on every task)

- **Never run `database/*.sql`.** It is reference DDL only; the `CMS` database already exists and is
  populated, and the scripts re-declare overlapping tables across files and will fail.
- **`pkid` is not always the primary key.** The auth tables (AppRole/AppUser/AppUserRole) key on an
  `nvarchar` string, not `pkid`; most other tables do use `pkid`. Read the DDL per table — `spec/gotchas.md`.
- **The toolchain is pinned; bare scaffolding produces the wrong versions.** Don't delete `global.json`,
  don't `ng new` / `ng generate` with the global CLI, don't bump PrimeNG/theme packages. See
  `spec/toolchain.md` first.
- **Integration tests hit the *real* CMS database.** Never mutate/delete the seeded `Admin`/`User`
  rows; assert containment, not exact counts. See `spec/testing.md`.

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

## Reference docs — open the matching file before that kind of work

| When you are… | Read |
|---|---|
| Adding a table (file layout, routes, PrimeNG patterns) | `spec/code-gen.convention.md` — the authority |
| Navigating the codebase, picking a slice to copy, or the QR/inline-edit/shared extras | `spec/architecture.md` |
| Writing repository, controller, or Angular code | `spec/gotchas.md` — load-bearing guards |
| Writing or running tests | `spec/testing.md` |
| Scaffolding, installing deps, or upgrading | `spec/toolchain.md` |
| Building a **custom** (non-CRUD) feature | `spec/custom/{Feature}/{Feature}.spec.md` + its `ui-*.spec.png` |

`spec/ui-sample-*.png` are **style reference only, not content.** `/crud` scaffolds a new table's
full slice and follows the rules above.
