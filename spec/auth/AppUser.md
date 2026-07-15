# Build Spec for AppUser
- database schema: `.\database\auth.sql`

`AppUser` is the account table for the CMS. It is the **string-keyed** shape (same family as
`AppRole`): the real primary key is the `nvarchar(200)` `UserId`, and `pkid` is an IDENTITY column
that is **not** the PK (no PK/UNIQUE constraint) — display-only (主代碼). It has an n-n with `AppRole`
through `AppUserRole`, keyed on the strings `(UserId, RoleId)`. It also carries a `PasswordHash`
column that is **backend-only** and never crosses the API boundary.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `UserId` nvarchar(200) (the real PK). `pkid` int IDENTITY is display-only, NOT the key. |
| Foreign Keys | N/A on AppUser itself. |
| Required Fields | `UserId`, `UserName`, `IsActive`, `PasswordHash` (backend-managed) |
| N-N Relationships | `AppUserRole` (AppUser ↔ AppRole), keyed `(UserId, RoleId)` — both `nvarchar(200)` |
| Primary-Foreign Links | N/A — the only inbound reference is the `AppUserRole` junction (handled as the n-n) |
| Query Filters | keyword (UserId, UserName); IsActive tri-state |
| Default Sort | `UserId ASC` |

**Backend-only column — `PasswordHash`:**
- Never sent to or received from the frontend. Excluded from `AppUserRequest` and every Angular model.
- **CREATE**: read `SysConfig.configValue` where `configKey = 'appConfig'` (a JSON object), extract
  `defaultPassword`, SHA-256 hash it, store as `PasswordHash`. Also stamp `PasswordUpdatedTime = now`.
- **UPDATE**: never touch `PasswordHash` / `PasswordUpdatedTime`.
- **Reset**: a separate endpoint `POST /api/app-users/{id}/reset-password` re-hashes the default and
  re-stamps `PasswordUpdatedTime`. No password value is ever accepted from the client.

---

## Localization

### Chinese Table Name

- AppUser: 使用者
- Description: 系統登入帳號主資料

### Chinese Column Names

- pkid: 主代碼
- UserId: 使用者代碼
- UserName: 使用者名稱
- IsActive: 啟用
- PasswordHash: 密碼雜湊（後端專用，不顯示）
- PasswordUpdatedTime: 密碼更新時間

---

## Required Fields

Required (NOT NULL):
- `UserId` — the primary key; immutable after creation.
- `UserName`
- `IsActive` — DB default `1` (DF_AppUser_IsActive); form defaults to true.
- `PasswordHash` — NOT NULL but **backend-managed**; never a form field (see Summary).

Optional (nullable):
- `PasswordUpdatedTime` — set by CREATE / reset-password only; read-only in the response.

---

## Foreign Keys

**N/A** — AppUser has no outbound FK columns.

---

## Foreign-Primary Links

**N/A** — no outbound FKs.

---

## Primary-Foreign Links

The only table that references `AppUser` is the junction `AppUserRole` (`FK_AppUserRole_AppUser`),
which is the n-n relationship below — not a child list. So **N/A** for cross-entity navigation buttons.

---

## N-N Relationships

### AppUserRole — AppUser ↔ AppRole

Junction table: `AppUserRole` (`UserId`, `RoleId`), composite PK `(UserId, RoleId)`.

- In the AppUser form (edit + new): multi-select of roles.
- Load options via `GET /api/lookups/app-roles` (returns `RoleId` + `RoleName`, ordered by `RoleId`).
- Request field: `RoleIds` (`List<string>`) — the **string** deviation, mirroring AppRole's `UserIds`
  (per CLAUDE.md, `AppUserRole` keys are `nvarchar(200)`, not the convention's `List<int>`).
- Sync on save (inside the repository's own `SqlTransaction`, for atomicity with the AppUser write):
  1. `DELETE FROM dbo.AppUserRole WHERE UserId = @UserId`
  2. Bulk `INSERT` from `.Distinct()`ed `RoleIds` (`PK_AppUserRole` throws 2627 on a payload dup).
- Detail view: render assigned roles as `RoleName (RoleId)` chips.
- Response model carries `RoleCount` (subquery count over `AppUserRole`) for the list column, and
  `RoleIds` populated only on GET-by-id.

---

## Query Filters

- **keyword**: string — LIKE on `UserId`, `UserName` (both nvarchar(200)). Uses `SqlLike.ToPattern`
  + `ESCAPE '\'`. `PasswordHash` is never searched.
- **IsActive**: `bool?` — tri-state. null = 全部, true = 啟用, false = 停用. Exact match on the bit column.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/app-roles` | **Exists** (LookupRepository.GetAppRolesAsync) | AppRole list (RoleId, RoleName) |
| `GET /api/lookups/app-users` | Exists — AppUser is itself the FK target used by AppRole; no change needed |

No new backend lookup endpoint. Angular needs a new `LookupService.appRoles()` binding + an
`AppRoleLookup` interface (the backend endpoint already exists).

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/app-users` | List all, ordered by UserId; each row carries RoleCount |
| `POST` | `/api/app-users/query` | Filtered query (body: `AppUserQuery`) |
| `GET` | `/api/app-users/{id}` | Get by UserId, includes `RoleIds` |
| `POST` | `/api/app-users` | Create; hashes default password into PasswordHash |
| `PUT` | `/api/app-users` | Update (UserId from body, immutable); never touches PasswordHash |
| `DELETE` | `/api/app-users/{id}` | Delete; clears AppUserRole rows first (no ON DELETE CASCADE) |
| `POST` | `/api/app-users/{id}/reset-password` | Re-hash default password, re-stamp PasswordUpdatedTime |

- `{id}` has **no `:int` constraint** — UserId is a string; Angular `encodeURIComponent`s it.
- 409 on create when UserId already exists (pre-check + duplicate-key backstop for the TOCTOU race).
- 400 on create/update when a `RoleId` in the payload doesn't exist (FK 547 → bad input).
- 404 on get/update/delete/reset when UserId is unknown.
- No `[Authorize]` — auth is deferred project-wide (CLAUDE.md).

---

## Backend Notes

### Models

```csharp
// AppUser.cs (response)
public class AppUser
{
    public int Pkid { get; set; }                    // display-only IDENTITY, not the key
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? PasswordUpdatedTime { get; set; } // read-only
    public int RoleCount { get; set; }                 // subquery over AppUserRole
    public List<string> RoleIds { get; set; } = [];    // populated only on GET by id
    // NOTE: PasswordHash is deliberately absent — backend-only.
}

// AppUserRequest.cs (write) — no PasswordHash, no PasswordUpdatedTime
public class AppUserRequest
{
    [Required][StringLength(200)]
    [RegularExpression(@"^[^\s/\\]+$")]  // URL-path-safe: forbid whitespace, '/', '\'
    public string UserId { get; set; } = string.Empty;

    [Required][StringLength(200)]
    public string UserName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public List<string> RoleIds { get; set; } = [];
}

// AppUserQuery.cs (search)
public class AppUserQuery
{
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
}
```

**UserId validation differs from AppRole's `^[A-Za-z0-9_\-]+$`.** AppUser IDs are real logins /
emails (the seeded live user is `miles@uuu.com.tw`), so the alphabet must allow `@ . -` etc. The only
load-bearing guard is URL-path safety: `^[^\s/\\]+$` forbids whitespace and slashes (Kestrel won't
round-trip `%2F`), which is exactly what makes the string addressable as a `{id}` route segment.

### SQL — SELECT

```sql
SELECT u.pkid AS Pkid, u.UserId, u.UserName, u.IsActive, u.PasswordUpdatedTime,
       (SELECT COUNT(*) FROM dbo.AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
FROM   dbo.AppUser u
-- PasswordHash is never SELECTed into the model.
```

GET-by-id additionally reads the role list:
```sql
SELECT ur.RoleId FROM dbo.AppUserRole ur WHERE ur.UserId = @UserId ORDER BY ur.RoleId;
```

### SQL — INSERT

```sql
INSERT INTO dbo.AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
VALUES (@UserId, @UserName, @IsActive, @PasswordHash, @PasswordUpdatedTime);
SELECT CAST(SCOPE_IDENTITY() AS int);
```
`@PasswordHash` is computed in the repository: read `SysConfig` appConfig JSON → `defaultPassword`
→ `PasswordHasher.Hash` (SHA-256, lowercase hex). `@PasswordUpdatedTime = DateTime.Now`.

### SQL — UPDATE

```sql
UPDATE dbo.AppUser
SET    UserName = @UserName,
       IsActive = @IsActive
WHERE  UserId = @UserId;
```
`UserId` is immutable (never in SET — a rename would orphan AppUserRole, no ON UPDATE CASCADE).
`PasswordHash` / `PasswordUpdatedTime` are **not** in the SET list.

### SQL — reset-password

```sql
UPDATE dbo.AppUser
SET    PasswordHash = @PasswordHash, PasswordUpdatedTime = @Now
WHERE  UserId = @UserId;
```

### N-N Sync Pattern (AppUserRole)

```sql
DELETE FROM dbo.AppUserRole WHERE UserId = @UserId;
-- then, for each distinct RoleId:
INSERT INTO dbo.AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId);
```
`.Distinct(StringComparer.OrdinalIgnoreCase)` + skip blank — same as AppRoleRepository.

### DELETE

`FK_AppUserRole_AppUser` has no ON DELETE CASCADE → delete junction rows first, then the user,
both in one transaction. Returns false when no user row was affected.

### Special Column Notes

- `PasswordHash` nvarchar(800) NOT NULL — backend-only; hashed default on create/reset.
- `PasswordUpdatedTime` datetime NULL — set on create/reset only; returned for display, appended with
  `'Z'` on the frontend before parsing (Dapper returns `Kind = Unspecified`).
- `pkid` is IDENTITY but not the key — same trap as AppRole; routes key on `UserId`.
- New infra: `Infrastructure/PasswordHasher.cs` (SHA-256 → hex). `SysConfig` read lives in
  `AppUserRepository` (repository owns its SQL); a missing `appConfig`/`defaultPassword` throws a
  clear `InvalidOperationException`.

---

## Frontend Notes

### Angular model (`app-user.model.ts` — extend the existing lookup file)

The file already exports `AppUserLookup` + `appUserLabel` (used by AppRole). Add:
```ts
export interface AppUser {
  pkid: number;
  userId: string;
  userName: string;
  isActive: boolean;
  passwordUpdatedTime: string | null;
  roleCount: number;
  roleIds: string[];
}
export interface AppUserRequest {          // no password field
  userId: string; userName: string; isActive: boolean; roleIds: string[];
}
export interface AppUserQuery { keyword: string | null; isActive: boolean | null; }
export const EMPTY_APP_USER_QUERY: AppUserQuery = { keyword: null, isActive: null };
```
Also add `AppRoleLookup { roleId; roleName }` + `appRoleLabel` (in `app-role.model.ts`) and a
`LookupService.appRoles()` method hitting `/lookups/app-roles`.

### Service (`app-user.service.ts`)

Standard six + `resetPassword(userId)` → `POST /app-users/{enc}/reset-password`. `getById` / `remove`
/ `resetPassword` `encodeURIComponent` the UserId.

### List

- Columns: 主代碼(pkid), 使用者代碼(userId, strong), 使用者名稱(userName), 啟用(是/否 tag),
  角色數(roleCount), 操作.
- Filter drawer: 關鍵字 (input), 啟用 (`p-select` tri-state — 全部/啟用/停用, same shape as Course's
  `canRepeatOptions`).
- Default sort `userId ASC`. Session keys `app-user-list-{filters,sort,page}`.
- Delete confirm: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.userId}」？`

### Form

- Fields: 使用者代碼 (input; disabled + hint in edit mode — immutable PK), 使用者名稱 (input),
  啟用 (`p-toggleswitch`, default true), 角色 (`p-multiselect` over app-roles, `optionValue="roleId"`).
- **No password field anywhere.** `getRawValue()` (not `value`) so the disabled `userId` survives the
  PUT (guarded by a unit test — same trap as AppRole).
- On create success / edit success → navigate to detail.

### Detail

- 使用者資料 card: 主代碼, 使用者代碼, 使用者名稱, 啟用(是/否), 密碼更新時間
  (`{{ (passwordUpdatedTime + 'Z') | date:'yyyy-MM-dd HH:mm' }}`, or 尚未設定 when null).
- 角色 card: `RoleName (RoleId)` chips, or 尚未指派角色.
- Toolbar buttons: 回到清單 / 編輯 / 重設密碼 (confirm → `resetPassword`) / 刪除.

### Sidebar

Group **系統管理 Admin** already exists (holds 角色 AppRole). Add
`{ label: '使用者 AppUser', icon: 'pi pi-users', route: '/app-users' }` after 角色 AppRole.

---

## Session Storage Keys

| Key | Contents |
|-----|----------|
| `app-user-list-filters` | Last query filter values |
| `app-user-list-sort` | `{ sortField, sortOrder }` |
| `app-user-list-page` | `{ first, rows }` |

---

## Tests

**Backend (xUnit + NSubstitute):**
- `Controllers/AppUsersControllerTests.cs` — mocked repo; status codes for all seven endpoints
  (list, query forwarding, get 200/404, create 201 keyed on UserId / 409 dup / 400 FK, update 204/404,
  delete 204/404, reset-password 204/404). Assert the Location route value is `UserId`, never pkid.
- `Repositories/AppUserRepositoryIntegrationTests.cs` — `[Trait("Category","Integration")]`, real DB.
  Insert (hashes a real PasswordHash from SysConfig, dedupes/ignores-blank RoleIds, rolls back on bad
  RoleId FK), keyword/IsActive query, LIKE-escape, GetById role list, Update leaves UserId/pkid/hash
  untouched, reset-password changes the hash + stamp, Delete clears junction. Uses `TEST_`-prefixed
  UserIds; the fixture already sweeps `dbo.AppUser WHERE UserId LIKE 'TEST\_%'`. Roles borrowed for
  the n-n are seeded as `TEST_` AppRoles.

**Frontend (Karma + Jasmine):**
- `app-user.service.spec.ts` — HttpTestingController: each method → right URL/verb; `encodeURIComponent`
  on getById/remove/resetPassword.
- `app-user-list.spec.ts`, `app-user-detail.spec.ts`, `app-user-form.spec.ts` — mocked services;
  render, required-field enforcement, and the getRawValue()/disabled-userId regression guard.

## Files to create / modify

**Backend (create):** `Models/AppUser.cs`, `Models/AppUserRequest.cs`, `Models/AppUserQuery.cs`,
`Repositories/IAppUserRepository.cs`, `Repositories/AppUserRepository.cs`,
`Controllers/AppUsersController.cs`, `Infrastructure/PasswordHasher.cs`,
`Tests/Controllers/AppUsersControllerTests.cs`, `Tests/Repositories/AppUserRepositoryIntegrationTests.cs`.
**Backend (modify):** `Program.cs` (DI registration).
**Frontend (create):** `core/models/app-user.model.ts` additions, `core/services/app-user.service.ts`,
`features/app-users/{app-user-list,app-user-detail,app-user-form}/*` (+ `.spec.ts`).
**Frontend (modify):** `core/models/app-role.model.ts` (AppRoleLookup), `core/services/lookup.service.ts`
(appRoles), `app.routes.ts`, `app.ts` + `app.html` (sidebar).
