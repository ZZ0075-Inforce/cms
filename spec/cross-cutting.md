# Cross-Cutting Conventions

Two conventions are wired into all six live tables (AppRole, AppUser, Partner, CourseGroup, Course,
FeaturedPromoItem) and MUST be followed by every new table/feature. This file is the authority;
`CLAUDE.md` carries only the one-line summary.

## Row Audit

Key files — writer: `Infrastructure/IRowAuditWriter` + `RowAuditWriter`; read side:
`Repositories/RowAuditRepository` + `Controllers/RowAuditController`; models: `Models/RowAudit` /
`RowAuditEntry`; badge: `shared/row-audit-badge/RowAuditBadge`. Copy a retrofitted repo
(e.g. `CourseRepository`) as the pattern.

Backend — every repository write logs via the shared `IRowAuditWriter`:

- Insert / Update / Delete each run **inside a transaction** and call `LogInsertAsync` /
  `LogUpdateAsync` / `LogDeleteAsync(conn, tx, tableName, …)` on the **same conn/tx** as the change —
  so a rolled-back change leaves no audit row.
- **Update**: load the existing row first with a private `LoadForAuditAsync` that selects ONLY
  base-table columns (no JOIN labels, derived counts or N-N sets), then pass `before`/`after`; the
  writer logs exactly the changed column names. A no-op update writes nothing.
- **Delete**: load the row first so its first string column is captured before it's gone.
- Let the writer derive the rest by reflection — don't hand-build rows:
  - `PrimaryKeyValues` = the entity's `pkid` as a string.
  - `ActionDesc` = Insert/Delete → the first string-type property's value; Update → comma-separated
    changed property names (capped 1000 chars).
  - `UserName` = the JWT `userName` claim, else `"system"`.
  - **Never insert `pkid`** (IDENTITY).
- Out of scope — the **complete** list of unaudited writes, not an illustrative one:
  `FeaturedPromoItemRepository.MoveSlotAsync` (a display-order nudge, not a data change). That is the
  whole list. Everything else is audited, including `AppUserRepository.ResetPasswordAsync` (admin
  reset — audited since 2026-07-17, `e47ab5b`) and `AuthRepository.UpdatePasswordAsync` /
  `UpdateUserNameAsync` (self-service change-password and rename, audited under `AppUser`). If you
  add a write, audit it or add it to this list — the list was previously read as "such as…" and two
  security-relevant writes quietly landed outside it.
- Deleting a row whose FK **cascades** must audit the cascaded children too: SQL Server fires no
  audit of its own, so load them before the delete and log one entry each on the same transaction
  (see `CourseGroupRepository.DeleteAsync`, which cascades to `Course`).

Frontend — every detail page and form page:

- Place `<app-row-audit-badge [tableName]="'…'" [pkid]="…" />` **first** in the page-header `.actions`
  row (the toolbar `#start` slot; the app has no `p-toolbar`). It fetches the record's history
  (`GET /api/rowaudit?tableName=&pkid=`), shows the latest change inline, and opens the full trail on
  click.
- `pkid` is the numeric IDENTITY (what the writer stored), **not** the route key — for AppRole/AppUser
  pass `entity.pkid`, not the string `RoleId`/`UserId`.
- Create/new forms have no record yet → gate the badge behind an `auditPkid` signal set only in edit
  mode (omit it on create).

Tests: a repo integration test (filter by tableName+pkid, newest-first) and an Angular badge spec
(latest inline / dialog trail / empty state). Adding the badge into a host component forces `HttpClient`
into that host's spec — stub `{ provide: RowAuditService, useValue: { history: () => of([]) } }`.

## Exception handling

Backend — `Infrastructure/GlobalExceptionMiddleware`, registered outermost in `Program.cs`:

- Catches **unhandled** errors: logs full detail server-side, returns one safe
  `500 { "message": "An unexpected error occurred." }` — never a stack trace or SQL.
- **Do not** add per-controller try/catch for *unexpected* errors. Keep meaningful, expected results as
  they are (FK → 400/409, 404, …).
- Leave 401 / 403 / validation-400 untouched — they are responses, not exceptions, and pass straight
  through.

Frontend — `core/interceptors/authInterceptor` centralises HTTP errors:

- status ≥ 500 → friendly error toast using the response body's safe `message`.
- 401 → clear session + redirect to `/login` (the login endpoint's own 401 passes through).
- Everything else (400 validation, 403, 404) passes through for the caller/form to handle.
