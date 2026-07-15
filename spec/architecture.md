# Architecture & layout

Read this when navigating the codebase or adding a table. `spec/code-gen.convention.md` is the
authority on file layout, route shapes, and PrimeNG patterns; this file is the map of what already
exists and why.

## Reference slices

Five vertical slices exist end to end; each is a reference for a different shape. Copy whichever
matches the table you are adding (`/crud` scaffolds it):

- **AppRole** — a *string-keyed* table (PK is `nvarchar`, not `pkid`) with an n-n (AppRole↔AppUser).
  The canonical string-key reference.
- **AppUser** — the same string-key + n-n shape, plus a **backend-only** `PasswordHash` column and a
  `reset-password` endpoint (see `spec/gotchas.md`).
- **Partner** / **CourseGroup** — the simple case: a genuine `pkid` IDENTITY primary key, `:int`
  routes, no FK, no n-n.
- **Course** — the full case: three FK dropdowns (resolved to JOIN labels), two n-n multi-selects,
  `date` columns, and `nvarchar(max)` text.

## API — `CMS.API/`

Request flow is `Controller → I{T}Repository → Dapper → SQL Server`. No service layer, no EF — the
repository *is* the data layer and owns its SQL.

```
Program.cs        DI, CORS, Swagger. DapperConfig.Register() runs before any query.
Data/             IDbConnectionFactory (returns DbConnection, so OpenAsync exists),
                  SqlConnectionFactory, DapperConfig, TypeHandlers/{DateOnly,TimeOnly}
Models/           per table: {T} (response) · {T}Request (write) · {T}Query (search).
                  Live: AppRole, AppUser, Partner, CourseGroup, Course.
Models/Lookups/   slim shapes for FK dropdowns — AppUser, AppRole, Partner, CourseGroup,
                  PublishStatus, Certification, JobCategory
Repositories/     I{T}Repository + {T}Repository — all SQL lives here
Controllers/      AppRoles, AppUsers, Partners, CourseGroups, Courses, Lookups
Infrastructure/   SqlErrorNumbers (2627/2601 duplicate key, 547 FK violation) ·
                  SqlLike.ToPattern — shared LIKE-escape (\ % _ [); every repo calls it ·
                  PasswordHasher — SHA-256 → lowercase hex (AppUser only)
```

`DateOnly`/`TimeOnly` handlers are registered in `DapperConfig` because `Microsoft.Data.SqlClient`
cannot bind those types natively. **Course is the first table to actually use them** (`ScheduleOn` /
`ScheduleOff` are `date` → `DateOnly`); a `time` column would need `TimeOnly` the same way.

## Angular — `CMS.NG/src/`

`app.ts` *is* the shell (sidebar nav model in the class, markup in `app.html`); there is no separate
layout component, per the convention. Feature components are standalone and lazy-routed.

```
environments/       environment.ts → '/api' (prod) · .development.ts → localhost:5000
app/core/models/    typed API shapes
app/core/services/  {T}Service + LookupService — the only HttpClient callers
                    (AppRole, AppUser, Partner, CourseGroup, Course)
app/core/utils/     date.util.ts — toIso / fromIso / addYears (local components, not UTC)
app/features/{app-roles,app-users,partners,course-groups,courses}/   {*-list, *-detail, *-form}/
```

Path aliases in `tsconfig.json`: `@env`, `@core/*`, `@features/*`. The `@env` import resolves per
build via `fileReplacements` in `angular.json` — that is what makes "no dev proxy" work.

## Intentional deviations from `code-gen.convention.md`

- **n-n lists are `List<string>`, not the convention's `List<int>`** — `AppUserRole` keys are
  `nvarchar(200)`. (Course's two n-n are `List<int>` / `List<short>`, matching their pkid types; the
  generic `SyncJunctionAsync` interpolates the junction table/column names — safe only because they
  are compile-time constants, never user input. The pkid values are always parameterised.)
- **FK nav is flat `{Fk}Name` label columns via JOIN, not the convention's multi-map nav objects.**
  Course's SELECT `INNER JOIN`s Partner/PublishStatus and `LEFT JOIN`s CourseGroup (nullable →
  `CourseGroupName` comes back null), returning `PartnerName` / `CourseGroupName` / `PublishStatusName`.
  Simpler to bind and test than nested objects, and there was no multi-map precedent to copy.
- **`Description` stays optional (`string?`)**: the DB column is `nvarchar(400) NULL` and the PNGs are
  style-only, even though the mockup paints a red `*` on 描述.

## Deferred by decision — not oversight

**No authentication and no `RowAudit` writes.** `RowAudit` requires a `UserName`; with no login in
scope there is no real current user, and a placeholder like `"system"` would put fake data in an audit
table. `spec/sample1.spec.md` *does* describe RowAudit logging, so this is a chosen deviation. When
auth lands, inject an `IAuditWriter` into the repositories and call it **inside** the transaction
`AppRoleRepository` already opens for the n-n sync. (AppUser's `PasswordHash` is set from a config
default precisely because there is no real user to supply a password — see `spec/gotchas.md`.)
