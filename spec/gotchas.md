# Gotchas & load-bearing guards

Read this before writing repository, controller, or Angular code. Every item here is a bug that was
already hit or a guard that is easy to remove by accident.

## Schema: some tables key on a string, not `pkid`

`AppRole`, `AppUser` and `AppUserRole` each carry a `pkid int IDENTITY` that is **not** the primary
key and has **no PK or UNIQUE constraint** — the database never promises it is unique. The real keys
are the `nvarchar(200)` strings `AppRole.RoleId` / `AppUser.UserId`, and `AppUserRole` has a composite
PK of `(UserId, RoleId)`. So:

- Routes key on the string: `GET|DELETE /api/app-roles/{id}`, **no `:int` constraint**; the Angular
  service `encodeURIComponent`s it. `pkid` is SELECTed for display (主代碼) only.
- The string key is **immutable** — never in an UPDATE `SET` list. The FK has no `ON UPDATE CASCADE`,
  so a rename would orphan `AppUserRole` rows. This is why 角色代碼 / 使用者代碼 is disabled in edit forms.
- The string key is validated because it becomes a URL path segment and Kestrel does not round-trip
  `%2F`. `RoleId` uses `^[A-Za-z0-9_\-]+$`; **`UserId` is looser — `^[^\s/\\]+$`** — because real user
  ids are logins / emails (e.g. `miles@uuu.com.tw`), and only whitespace + slashes actually break the
  route.

**Do not generalise this.** Most other tables (Course, Partner, …) *do* use `pkid` as a genuine
IDENTITY primary key. Read the DDL per table.

## AppUser password handling (backend-only)

`AppUser.PasswordHash` (nvarchar(800)) **never crosses the API boundary** — it is absent from
`AppUserRequest` and every Angular model, and never SELECTed into the response.

- **Create**: read `SysConfig.configValue` where `configKey = 'appConfig'` (a JSON object), extract
  `defaultPassword`, SHA-256 it via `Infrastructure/PasswordHasher`, store; also stamp
  `PasswordUpdatedTime = now`. A missing config/property throws (server-config fault, not client error).
- **Update**: never touches `PasswordHash` / `PasswordUpdatedTime`.
- **Reset**: `POST /api/app-users/{id}/reset-password` re-hashes the same default and re-stamps the
  time. No password value is ever accepted from the client.

## Traps already hit — keep these guards

- **`Program.cs` deliberately omits `app.UseHttpsRedirection()`.** The template ships it; it 307s every
  `:5000` call to `https://localhost:5001` (not listening) and silently breaks CORS preflight. There is
  no dev proxy — the Angular app calls `http://localhost:5000/api` directly, so API-side CORS is
  load-bearing, not optional.
- **Route order:** `{entity}/new` must be declared **before** `{entity}/:id`, or `/{entity}/new`
  resolves to the detail page for a record named "new". A string PK removes the `:int` constraint that
  would otherwise mask this.
- **Forms: use `form.getRawValue()`, never `form.value`.** `value` omits *disabled* controls, so in
  edit mode the disabled key (roleId/userId) would drop out of the PUT body and every update would 404.
  A unit test guards it per feature.
- **LIKE filters escape `\ % _ [` and use `ESCAPE '\'`** — otherwise a keyword of `%` matches every row.
  The escape lives in one place, `Infrastructure/SqlLike.ToPattern`; call it, don't re-implement per repo.
- **A cascading FK does *not* raise 547 — it silently deletes.** `FK_Course_CourseGroup` is
  `ON DELETE CASCADE`, so deleting a `CourseGroup` wipes its `Course` rows without complaint; only the
  *non*-cascading `PartnerCourseGroup` FK trips 547 → 409. Where a delete can cascade, the DB will not
  protect the children — the front-end confirm dialog must spell out the count (`courseCount`) before it
  calls DELETE. Read each FK's cascade rule per table; "still referenced" does not always mean "blocked".
- **PrimeNG `p-table` sorts its bound array *in place*.** When the default `sortField`/`sortOrder`
  differs from the data's natural order, the rows — and the array you passed in — are reordered on first
  render. List tests that index into the input (`rows[0]`, `items[i]`) then point at the wrong record;
  reference records by identity instead. (Bit the CourseGroup list, default `pkid DESC`; and the AppUser
  list test, default `userId ASC`.)
- **FK violation (547) means opposite things by verb.** On INSERT/UPDATE it is *bad input* — a payload
  FK that doesn't exist (an unknown Partner/Certification, or a RoleId not in AppRole) → translate to
  **400**. On DELETE it is *still in use* — a child row pinning the parent → translate to **409**. The
  same `SqlErrorNumbers.ForeignKeyViolation` is caught in both places and mapped differently.
- **Serialise `date` with `date.util.ts` `toIso`, never `Date.toISOString()`.** `toISOString()` converts
  to UTC first, so a UTC+8 user's 2026-07-14 is sent as 2026-07-13. `toIso` builds the string from local
  components; `fromIso` parses back for `p-datepicker`. Course's 下架日期 = 上架日期 + 10y auto-default uses
  `addYears` on `scheduleOn`'s `valueChanges` with `{ emitEvent: false }`; in edit mode the loaded
  `scheduleOff` is patched right after `scheduleOn` so it wins over the auto value.
- **Display API `datetime` with a `'Z'` suffix.** Dapper returns `datetime` as `Kind = Unspecified`
  (no zone); append `'Z'` before parsing (`{{ item.dateTime + 'Z' | date:'…' }}`) or it renders in the
  wrong offset. (AppUser's 密碼更新時間 does this.)
- **`.Distinct()` the incoming n-n key list** before the delete-then-reinsert (UserIds for AppRole,
  RoleIds for AppUser); `PK_AppUserRole(UserId, RoleId)` throws 2627 on a duplicate in the payload.
