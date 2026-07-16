# Build Spec for PublishStatus
- database schema: `.\database\admin.sql`（`course.sql` 亦重複宣告同一張 PublishStatus，欄位一致）

---

## Summary

`PublishStatus` 是 admin 子系統的一張**固定列舉式**小查詢表，代表課程的上架狀態（草稿／上架／下架）。
它本身沒有任何外來鍵，欄位單純：一段狀態說明 `Description`，加上三個布林旗標
`IsDraft` / `IsPublished` / `IsDiscontinued`。

它的份量來自「被誰參照」：`Course.PublishStatus_pkid`（以及 reference DDL 裡的 `Promotion2`）以 FK 指向
`PublishStatus.pkid`。專案先前已為此建立 lookup（`PublishStatusLookup` + `GET /api/lookups/publish-statuses`），
供 Course 表單的下拉選單使用——**那組保留、本次不重複**。本次補的是完整的管理切片（list/detail/form + CRUD）。

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **tinyint**，**非 IDENTITY**——由使用者輸入的固定鍵（與 Partner 的 IDENTITY `pkid` 不同）|
| Foreign Keys | 無 — PublishStatus 不參照任何表 |
| Required Fields | `pkid`、`Description`、`IsDraft`、`IsPublished`、`IsDiscontinued`（皆 NOT NULL）|
| N-N Relationships | N/A |
| Primary-Foreign Links | `Course`（`Course.PublishStatus_pkid`，非 cascade）；本次不做導覽按鈕（見下）|
| Query Filters | keyword（`Description`）＋三個 tri-state 布林旗標 |
| Default Sort | `pkid ASC`（自然的 草稿→上架→下架 進程，與 lookup 排序一致）|

---

## Localization

### Chinese Table Name

- PublishStatus: 上架狀態
- Description: 課程的上架狀態主檔（草稿／上架／下架），供 Course 引用

### Chinese Column Names

- pkid: 主代碼
- Description: 狀態說明
- IsDraft: 草稿
- IsPublished: 已上架
- IsDiscontinued: 已下架

> 三個布林欄位是同一張表上的獨立旗標（DDL 未加任何檢查約束保證互斥），故各自以獨立開關呈現，
> 不合併成單選。中文命名比照既有 lookup 的「上架狀態」語彙。

---

## Required Fields

Required (NOT NULL)：

- `pkid` — tinyint（**由使用者輸入**，非 IDENTITY；新增時必填，編輯時鎖定不可改）
- `Description` — nvarchar(50)
- `IsDraft` — bit
- `IsPublished` — bit
- `IsDiscontinued` — bit

Optional (nullable)：無。

> **`pkid` 是主鍵且非 IDENTITY。** 這是 PublishStatus 與其他大部分表最大的差異：新增時 INSERT 必須帶入
> `pkid`（不是 `SCOPE_IDENTITY()`），重複輸入既有 `pkid` 會觸發 PK 違反（2627）→ Controller 回 `409`。
> 編輯時 `pkid` 不進 UPDATE 的 SET 清單（`Course` 以 FK 指向它，FK 無 `ON UPDATE CASCADE`，改鍵會孤立子列），
> 比照 AppRole.RoleId 的 immutable 處理。

---

## Foreign Keys

**N/A** — `PublishStatus` 沒有任何外來鍵欄位。

---

## Foreign-Primary Links

**N/A** — 沒有 FK，因此沒有往外的導覽連結。

---

## Primary-Foreign Links

以 FK 指向 `PublishStatus.pkid` 的表：

| 子表 | FK 欄位 | 可為 null | cascade | 子系統 |
|------|---------|-----------|---------|--------|
| `Course` | `PublishStatus_pkid` | 否 | 否（`FK_Course_PublishStatus`）| course |

> **本次不實作導覽按鈕。** 沿用 Partner 的既定作法：`Course` 列表目前不接受 `publishStatusPkid` 查詢參數，
> 硬做一個 `/courses?publishStatusPkid=` 只會是壞連結。列表／明細改以唯讀的「對應課程數 CourseCount」
> 子查詢呈現這個關係。
>
> **刪除時的 FK 保護仍要做**：`FK_Course_PublishStatus` 無 `ON DELETE CASCADE`，刪除一個仍被課程引用的
> 上架狀態會噴 SQL error 547。後端讓它往上拋，Controller 用既有的 `SqlErrorNumbers.ForeignKeyViolation`
> 攔下回 `409 Conflict`，而不是 500。

---

## N-N Relationships

**N/A** — 沒有 junction 表指向 PublishStatus。

---

## Query Filters

- **keyword**: string
  - LIKE on `Description`
  - 逸出 `\ % _ [` 並搭配 `ESCAPE '\'`（呼叫共用的 `Infrastructure/SqlLike.ToPattern`）
- **三個 tri-state 布林旗標**：`IsDraft?` / `IsPublished?` / `IsDiscontinued?`
  - null = 不篩選、true = 只顯示勾選、false = 只顯示未勾選
  - 各自對 bit 欄位做等值比對

沒有 FK 篩選、沒有日期欄位。

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/publish-statuses` | **既有（保留）** | `PublishStatusLookup { Pkid, Description }`，`ORDER BY pkid ASC` |

PublishStatus 自己不需要任何 lookup（無 FK）。它**是** `Course.PublishStatus_pkid` 的 FK 目標，
但該 lookup 端點與 model 先前已建，本次不重複、不修改。

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/publish-statuses` | 全部，`ORDER BY pkid ASC` |
| `POST` | `/api/publish-statuses/query` | 條件查詢（body: `PublishStatusQuery`）|
| `GET` | `/api/publish-statuses/{id:int}` | 依 pkid 取單筆 |
| `POST` | `/api/publish-statuses` | 新增（pkid 由 body 帶入）→ `201` / `409`（pkid 重複）|
| `PUT` | `/api/publish-statuses` | 更新（pkid 取自 body、不可改）→ `204` / `404` |
| `DELETE` | `/api/publish-statuses/{id:int}` | 刪除 → `204` / `404` / **`409`（仍被課程引用）** |

> 路由帶 `:int` 約束——`pkid` 是數字（tinyint），`/new` 不會撞 `/:id`。Controller 參數型別用 `byte`。
> 控制器類別名沿用專案複數慣例：`PublishStatusesController`（如同 `PartnersController`）。
>
> 全端點要求已登入使用者（沿用 `Program.cs` 的 `FallbackPolicy`）。

---

## Backend Notes

### Models

```csharp
// Models/PublishStatus.cs — 回應模型
public class PublishStatus
{
    public byte Pkid { get; set; }                 // tinyint PK, 非 IDENTITY
    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }

    /// <summary>對應課程數 — subquery count over Course.PublishStatus_pkid. Display only.</summary>
    public int CourseCount { get; set; }
}

// Models/PublishStatusRequest.cs — 寫入 DTO
public class PublishStatusRequest
{
    [Range(0, 255)] public byte Pkid { get; set; }   // 新增時由 body 帶入；更新時為鍵、不可改
    [Required, StringLength(50)] public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

// Models/PublishStatusQuery.cs — 查詢 DTO
public class PublishStatusQuery
{
    public string? Keyword { get; set; }             // LIKE Description
    public bool? IsDraft { get; set; }               // tri-state
    public bool? IsPublished { get; set; }
    public bool? IsDiscontinued { get; set; }
}
```

型別對照：`tinyint` → `byte`、`nvarchar` NOT NULL → `string`、`bit` → `bool`。無 `date`/`time`/`nchar`/`decimal`。

### SQL — SELECT

```sql
SELECT s.pkid AS Pkid, s.Description, s.IsDraft, s.IsPublished, s.IsDiscontinued,
       (SELECT COUNT(*) FROM dbo.Course c WHERE c.PublishStatus_pkid = s.pkid) AS CourseCount
FROM   dbo.PublishStatus s
```

`GetAllAsync` / `QueryAsync` / `GetByIdAsync` 共用。無 FK → 不需 multi-map。

`QueryAsync` 的 WHERE：

```sql
WHERE (@Keyword IS NULL OR s.Description LIKE @Like ESCAPE '\')
  AND (@IsDraft        IS NULL OR s.IsDraft        = @IsDraft)
  AND (@IsPublished    IS NULL OR s.IsPublished    = @IsPublished)
  AND (@IsDiscontinued IS NULL OR s.IsDiscontinued = @IsDiscontinued)
ORDER BY s.pkid ASC;
```

### SQL — INSERT（pkid 由使用者帶入，非 IDENTITY）

```sql
INSERT INTO dbo.PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
```

**沒有** `SELECT SCOPE_IDENTITY()`——`pkid` 不是 IDENTITY。`InsertAsync` 回傳 `request.Pkid`。
重複 `pkid` → PK 違反 2627 → Controller 回 409。

### SQL — UPDATE（pkid 為鍵、不在 SET）

```sql
UPDATE dbo.PublishStatus
SET    Description = @Description, IsDraft = @IsDraft,
       IsPublished = @IsPublished, IsDiscontinued = @IsDiscontinued
WHERE  pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM dbo.PublishStatus WHERE pkid = @Pkid;
```

`Course.PublishStatus_pkid` 的 FK 無 cascade → 刪除仍被引用者拋 547 → Controller 轉 409。

### RowAudit（cross-cutting，必接）

比照 `PartnerRepository`：Insert/Update/Delete 都在**同一 conn/tx** 上呼叫 `IRowAuditWriter`。
- **Insert**：INSERT 後以 `LoadForAuditAsync` 讀回該列，`LogInsertAsync`（`ActionDesc` = 第一個字串欄位 `Description`）。
- **Update**：先 `LoadForAuditAsync` 讀 before（無則 404 rollback），UPDATE 後讀 after，`LogUpdateAsync`（no-op 不寫）。
- **Delete**：先讀該列（無則 404 rollback），DELETE 後 `LogDeleteAsync`。
- `PrimaryKeyValues` = `pkid`（writer 以反射從 entity 的 `pkid` 取）。`pkid` 非 IDENTITY，但 writer 只讀不寫 audit，
  repository 的 INSERT 照樣帶 `pkid`——與「never insert IDENTITY pkid」不衝突（這裡 pkid 本就不是 IDENTITY）。
- `LoadForAuditAsync` 只選基底欄位（不含 CourseCount），before/after 的 CourseCount 皆為 0，不會誤判為變更。

### 例外處理（cross-cutting）

沿用 `GlobalExceptionMiddleware`；Controller 只針對「預期」結果攔截：pkid 重複（2627→409）、
FK 使用中（547→409）。其餘非預期例外交給 middleware 回統一 500。

---

## Frontend Notes

### Files

```
core/models/publish-status.model.ts             PublishStatus / PublishStatusRequest / PublishStatusQuery
core/services/publish-status.service.ts          唯一呼叫 /api/publish-statuses 的地方
features/publish-statuses/publish-status-list/    列表 + 篩選抽屜
features/publish-statuses/publish-status-detail/   檢視
features/publish-statuses/publish-status-form/     新增／編輯共用
```

> `PublishStatusLookup` 與 `LookupService.publishStatuses()` 已存在於 `course.model.ts` / `lookup.service.ts`，
> **不重複、不修改**。

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `publish-statuses` | `PublishStatusList` | 上架狀態 PublishStatus |
| `publish-statuses/new` | `PublishStatusForm` | 新增上架狀態 |
| `publish-statuses/:id/edit` | `PublishStatusForm` | 編輯上架狀態 |
| `publish-statuses/:id` | `PublishStatusDetail` | 檢視上架狀態 |

`new` 宣告在 `:id` 之前（house rule）。

### List page

- 欄位：主代碼、狀態說明、草稿、已上架、已下架、對應課程數、操作
- 布林欄位以 `pi pi-check` / `—` 呈現
- 預設排序：`pkid` ASC
- 篩選抽屜：關鍵字（狀態說明）＋三個 tri-state 開關（`p-select` 或三態 checkbox）
- Session storage：`publish-status-list-filters` / `-sort` / `-page`
- 刪除確認：`確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？`
- 刪除回 409 → toast「上架狀態使用中」，顯示後端 detail

### Form page

Reactive Forms，一個 `p-card`：

| 欄位 | 控制項 | 必填 | 備註 |
|------|--------|------|------|
| 主代碼 | `p-inputnumber` | ✅ | 0–255；**編輯時 disable（鍵不可改）**|
| 狀態說明 | `input pInputText` | ✅ | maxlength 50 |
| 草稿 | `p-toggleswitch` | — | bit |
| 已上架 | `p-toggleswitch` | — | bit |
| 已下架 | `p-toggleswitch` | — | bit |

- 編輯模式 `pkid` 控制項 `disable()`，並顯示「主代碼為主鍵，建立後不可修改」提示
- 存檔用 `form.getRawValue()`（非 `form.value`）——編輯時 disabled 的 pkid 必須仍進 payload，否則 PUT 會掉鍵
- `<app-row-audit-badge [tableName]="'PublishStatus'" [pkid]="pk">` 放在 `.actions` 開頭，
  以 `auditPkid` signal 僅在編輯模式顯示（新增時省略）

### Detail page

唯讀顯示五個欄位 + 對應課程數。`.actions` 開頭放
`<app-row-audit-badge [tableName]="'PublishStatus'" [pkid]="item.pkid">`。工具列：編輯、刪除、返回列表。

### Sidebar

`MENU_GROUP = 系統管理 Admin`（既有群組）。在 `app.ts` 的 Admin group `items` 追加：

```ts
{ label: '上架狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' }
```

---

## Tests

### Backend（`CMS.API.Tests`）

- `Controllers/PublishStatusesControllerTests.cs` — NSubstitute 替身 `IPublishStatusRepository`：
  list、query、get-by-id（找到 / 404）、create（201 + Location，pkid 取自 body、409 重複）、
  update（204 / 404）、delete（204 / 404 / **409 FK 違反**）
- `Repositories/PublishStatusRepositoryIntegrationTests.cs` — `[IntegrationFact]`，打真實 `CMS` 資料庫，
  自建 TEST_ 前綴 row（`pkid` 取未用值、`Description` 帶前綴），`DatabaseFixture` 清掃；
  斷言用「包含」而非精確筆數；含 **RowAudit 斷言**（Insert/Update/Delete 各寫一列、ActionDesc 正確、
  `UserName = "system"`）；LIKE 逸出（keyword `%` 不撈全部）

### Frontend（Karma + Jasmine）

- `publish-status.service.spec.ts` — `HttpTestingController` 驗證每個方法打對 URL 與 verb
- `publish-status-list.spec.ts` / `-detail.spec.ts` / `-form.spec.ts` — 假 service 掛載，
  斷言渲染、必填驗證會擋、送出用 `getRawValue()`、編輯時 pkid disabled 仍進 payload

---

## Files to Create / Modify

### Backend

| 檔案 | 動作 |
|------|------|
| `src/CMS.API/Models/PublishStatus.cs` | 新增 |
| `src/CMS.API/Models/PublishStatusRequest.cs` | 新增 |
| `src/CMS.API/Models/PublishStatusQuery.cs` | 新增 |
| `src/CMS.API/Repositories/IPublishStatusRepository.cs` | 新增 |
| `src/CMS.API/Repositories/PublishStatusRepository.cs` | 新增 |
| `src/CMS.API/Controllers/PublishStatusesController.cs` | 新增 |
| `src/CMS.API/Program.cs` | 修改 — DI 註冊 `IPublishStatusRepository` |

`Models/Lookups/PublishStatusLookup.cs` 與 LookupsController/LookupRepository 的 publish-statuses 端點：**既有、不動**。

### Frontend

| 檔案 | 動作 |
|------|------|
| `core/models/publish-status.model.ts` | 新增 |
| `core/services/publish-status.service.ts` | 新增 |
| `features/publish-statuses/publish-status-list/*` | 新增（ts / html / scss）|
| `features/publish-statuses/publish-status-detail/*` | 新增（ts / html / scss）|
| `features/publish-statuses/publish-status-form/*` | 新增（ts / html / scss）|
| `app.routes.ts` | 修改 — 4 條 lazy route |
| `app.ts` | 修改 — 系統管理 Admin 群組新增選單項目 |

### Tests

| 檔案 | 動作 |
|------|------|
| `CMS.API.Tests/Controllers/PublishStatusesControllerTests.cs` | 新增 |
| `CMS.API.Tests/Repositories/PublishStatusRepositoryIntegrationTests.cs` | 新增 |
| `CMS.API.Tests/Infrastructure/DatabaseFixture.cs` | 修改 — 加 PublishStatus 清掃 + pkid/Description 產生器 |
| `core/services/publish-status.service.spec.ts` | 新增 |
| `features/publish-statuses/*/*.spec.ts` | 新增（list / detail / form）|
