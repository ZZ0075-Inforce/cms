# Build Spec for CourseGroup
- database schema: `.\database\course.sql`

## Summary

`CourseGroup` 是課程的分類群組——一組課程（例如「雲端」「資安」「AI」）掛在同一個群組下。
它是這個資料庫裡最單純的一張表：只有兩個欄位，一個 IDENTITY 主鍵加一個必填的群組名稱。

它的份量同樣來自「被誰參照」，而且參照方式有一個**危險的不對稱**，是本表唯一需要小心的地方：

- `Course.CourseGroup_pkid` 指向它，且該 FK 帶 **`ON DELETE CASCADE`** —— 刪掉一個群組會
  **連帶刪掉群組底下所有課程**。
- `PartnerCourseGroup.CourseGroup_pkid` 也指向它，但**沒有** cascade —— 有引用時刪除會噴 547。

因此刪除時真正會擋下操作（547 → 409）的是 `PartnerCourseGroup`，而 `Course` 會被**靜默連帶刪除**。
唯一能保護課程的是刪除確認對話框的文案（見 Frontend Notes）。

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint** IDENTITY（真主鍵，可安全用於路由）|
| Foreign Keys | 無 — CourseGroup 不參照任何表 |
| Required Fields | `Description`（nvarchar(100)）|
| N-N Relationships | N/A — 見下方對 `PartnerCourseGroup` 的說明 |
| Primary-Foreign Links | `Course`（**ON DELETE CASCADE**）、`PartnerCourseGroup`（無 cascade）|
| Query Filters | keyword（`Description`）|
| Default Sort | `pkid DESC`（無 DisplayOrder / 日期欄位，套用慣例的 fallback）|

---

## Localization

### Chinese Table Name

- CourseGroup: 課程群組
- Description: 課程的分類群組主資料

### Chinese Column Names

- pkid: 主代碼
- Description: 群組名稱

> `Description` 譯為「群組名稱」而非字面的「描述」：它是 `nvarchar(100) NOT NULL`，是這個群組
> 唯一、且必填的識別文字，功能上就是群組的名稱（與 `JobCategory.Description` 同一種用法）。
> C# 屬性名維持 `Description`，對應 DB 欄位。

---

## Required Fields

Required (NOT NULL)：

- `Description` — nvarchar(100)

Optional (nullable)：

- 無。

> `Description` **沒有 UNIQUE constraint**。DDL 上只有 `PK_CourseGroup(pkid)`，資料庫不保證群組名稱
> 不重複，因此後端**不做** 2627/2601 重複鍵處理。

---

## Foreign Keys

**N/A** — `CourseGroup` 沒有任何外來鍵欄位。

---

## Foreign-Primary Links

**N/A** — 沒有 FK，因此沒有往外的導覽連結。

---

## Primary-Foreign Links

以下各表以 FK 指向 `CourseGroup.pkid`：

| 子表 | FK 欄位 | 可為 null | 刪除行為 | 子系統 |
|------|---------|-----------|----------|--------|
| `Course` | `CourseGroup_pkid` | **是** | **`ON DELETE CASCADE`** | course |
| `PartnerCourseGroup` | `CourseGroup_pkid` | 否 | 無 cascade（擋刪除）| course |

規劃中的導覽形式（沿用慣例）：

- **Course** — 欄位標題「對應課程」、按鈕「查看課程」（`pi pi-book`）→ `/courses?courseGroupPkid={pkid}`
- **PartnerCourseGroup** — 「對應廠商群組」、「查看廠商群組」（`pi pi-sitemap`）→ `/partner-course-groups?courseGroupPkid={pkid}`

> **本次不實作這些按鈕。** 兩個 feature 目前都還沒有 Angular 路由；`app.routes.ts` 的萬用路由會把
> 連結吃掉並跳回預設頁，做出來只會是壞按鈕。等對應的 `/crud` 跑完、路由存在了再回頭補（與 Partner 一致）。

> ### ⚠️ 刪除語意（本表最重要的一點）
>
> `CourseGroup` 的兩個被參照 FK 行為**不一致**：
>
> - `FK_Course_CourseGroup` 帶 `ON DELETE CASCADE`。刪掉群組時，SQL 會**先連帶刪除該群組底下的所有
>   `Course`**（以及 Course 再往下 cascade 的 `CourseFAQ` / `CourseJobCategories` / …）。
> - `FK_PartnerCourseGroup_CourseGroup` **沒有** cascade。若仍有 `PartnerCourseGroup` row 引用該群組，
>   整個 DELETE 會拋 SQL error **547** 並回滾（含上面的 Course cascade），刪除失敗。
>
> 結論：
> 1. **547 只會由 `PartnerCourseGroup` 觸發** → Controller 攔下回 `409 Conflict`「群組使用中」。
> 2. **`Course` 不會擋刪除，而是被靜默刪掉。** 資料庫層無法阻止；唯一的保護是前端刪除確認對話框
>    必須用「對應課程數」明白警告：`此群組下的 N 門課程將一併被刪除`。詳見 Frontend Notes。

---

## N-N Relationships

**N/A。**

`PartnerCourseGroup` 乍看是 CourseGroup ↔ Partner 的中介表，但它不是純粹的 junction：

```sql
CREATE TABLE [dbo].[PartnerCourseGroup](
    [pkid]             [int] IDENTITY(1,1) NOT NULL,   -- 自己的 IDENTITY 主鍵
    [Partner_pkid]     [smallint] NOT NULL,
    [CourseGroup_pkid] [smallint] NOT NULL,
    [DisplayOrder]     [int] NOT NULL,                 -- 酬載
    [Description]      [nvarchar](100) NOT NULL,       -- 酬載，且 NOT NULL
    CONSTRAINT [PK_PartnerCourseGroup] PRIMARY KEY CLUSTERED ([pkid] ASC)
)
```

它有自己的 IDENTITY 主鍵、一個 **NOT NULL 的 `Description`**，且 `Promotion2` 還以
`RelatedPartnerCourseGroup_pkid` 直接參照它——它是獨立實體，不是連接線。與 Partner 的 spec 結論一致：
**不**把它內嵌進 CourseGroup 表單（那個必填 `Description` 沒地方填），日後獨立跑 `/crud PartnerCourseGroup`。

---

## Query Filters

- **keyword**: string
  - LIKE on `Description`（唯一的字串欄位）
  - 逸出 `\ % _ [` 並搭配 `ESCAPE '\'`（沿用共用的 `SqlLike.ToPattern`）

沒有 FK 篩選、沒有 bit 欄位、沒有日期欄位，因此篩選抽屜只有一個關鍵字欄位。

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/course-groups` | **New** | `CourseGroupLookup { Pkid, Description }`，`ORDER BY Description ASC, pkid ASC` |

CourseGroup 自己不需要任何 lookup（無 FK），但它**是** `Course` 的 FK 目標（`Course.CourseGroup_pkid`），
所以這支端點現在就建，供日後 Course 表單的群組下拉使用。下拉用 `Description` 排序（字母序好找），
與列表頁預設的 `pkid DESC` 不同——這是刻意的。

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/course-groups` | 全部，`ORDER BY pkid DESC` |
| `POST` | `/api/course-groups/query` | 條件查詢（body: `CourseGroupQuery`）|
| `GET` | `/api/course-groups/{id:int}` | 依 pkid 取單筆 |
| `POST` | `/api/course-groups` | 新增 → `201 Created` + 新 pkid |
| `PUT` | `/api/course-groups` | 更新（pkid 取自 body）→ `204` / `404` |
| `DELETE` | `/api/course-groups/{id:int}` | 刪除 → `204` / `404` / **`409`（仍被 `PartnerCourseGroup` 引用）** |
| `GET` | `/api/lookups/course-groups` | 下拉用精簡清單 |

> 路由帶 `:int` 約束——`pkid` 是真正的 IDENTITY 主鍵，不需 `encodeURIComponent`，也不會有
> `/new` 撞 `/:id` 的問題。Angular 路由順序仍照慣例把 `course-groups/new` 寫在 `course-groups/:id` 之前。

無認證（見 CLAUDE.md「Deferred, by decision」）。

---

## Backend Notes

### Models

```csharp
// Models/CourseGroup.cs — 回應模型
public class CourseGroup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>唯讀統計，供列表顯示「對應課程數」，並在刪除確認時警告 cascade 會刪幾門課。</summary>
    public int CourseCount { get; set; }
}

// Models/CourseGroupRequest.cs — 寫入 DTO
public class CourseGroupRequest
{
    public short Pkid { get; set; }                   // INSERT 時忽略

    [Required, StringLength(100)]
    public string Description { get; set; } = string.Empty;
}

// Models/CourseGroupQuery.cs — 查詢 DTO
public class CourseGroupQuery
{
    public string? Keyword { get; set; }
}

// Models/Lookups/CourseGroupLookup.cs
public class CourseGroupLookup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
```

型別對照：`smallint` → `short`（`pkid` 亦然）、`nvarchar` NOT NULL → `string`。本表沒有
`date` / `time` / `nchar` / `decimal` / `bit` 欄位，因此不需要 `RTRIM()`，也用不到
`DateOnly` / `TimeOnly` handler。

### SQL — SELECT

```sql
SELECT g.pkid AS Pkid, g.Description,
       (SELECT COUNT(*) FROM dbo.Course c WHERE c.CourseGroup_pkid = g.pkid) AS CourseCount
FROM   dbo.CourseGroup g
```

`GetAllAsync` / `QueryAsync` / `GetByIdAsync` 共用這段（沿用 `SelectColumns` 的 `const string` 寫法）。
沒有 FK，因此不需要 multi-map、也沒有 `splitOn`。

`QueryAsync` 的 WHERE：

```sql
WHERE (@Keyword IS NULL OR g.Description LIKE @Like ESCAPE '\')
ORDER BY g.pkid DESC;
```

### SQL — INSERT

```sql
INSERT INTO dbo.CourseGroup (Description)
VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

`pkid` 是 IDENTITY，排除在外。`SCOPE_IDENTITY()` CAST 成 `smallint` 對應 `CourseGroup.Pkid`。

### SQL — UPDATE

```sql
UPDATE dbo.CourseGroup
SET    Description = @Description
WHERE  pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM dbo.CourseGroup WHERE pkid = @Pkid;
```

Repository 直接執行、**不**在刪除前做任何 cascade 或清理——`Course` 的刪除交給 DB 的
`ON DELETE CASCADE` 處理，`PartnerCourseGroup` 的 547 讓它往上拋。Controller 用既有的
`SqlErrorNumbers.ForeignKeyViolation (547)` 攔下回 `409 Conflict`，訊息「此群組仍被廠商群組
使用中，無法刪除」。

> **注意：547 不代表「沒有課程」。** 有課程但無 `PartnerCourseGroup` 引用的群組會刪除成功，
> 並連帶刪掉那些課程。這無法在後端阻止（DB 的 cascade 設定），保護只在前端確認文案。

### N-N Sync Pattern

**N/A** — 無 N-N。repository **不需要**開 transaction：每個寫入操作都是單一 statement。

### RowAudit

**不寫入**——沿用 CLAUDE.md「Deferred, by decision」。本專案不存在 `RowAuditWriter` /
`RowAuditBadgeComponent`，故略過 skill 樣板中對它們的引用。

---

## Frontend Notes

### Files

```
core/models/course-group.model.ts            CourseGroup / CourseGroupRequest / CourseGroupQuery / CourseGroupLookup
core/services/course-group.service.ts         唯一呼叫 /api/course-groups 的地方
features/course-groups/course-group-list/      列表 + 篩選抽屜
features/course-groups/course-group-detail/    檢視
features/course-groups/course-group-form/      新增／編輯共用
```

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `course-groups` | `CourseGroupList` | 課程群組 CourseGroup |
| `course-groups/new` | `CourseGroupForm` | 新增課程群組 |
| `course-groups/:id/edit` | `CourseGroupForm` | 編輯課程群組 |
| `course-groups/:id` | `CourseGroupDetail` | 檢視課程群組 |

`new` 仍宣告在 `:id` 之前（照慣例）。

### List page

- 欄位：主代碼、群組名稱、對應課程數、操作
- 預設排序：`pkid` DESC（無 DisplayOrder；列表可點欄位改排序，例如按群組名稱）
- `p-table` 可排序、可分頁；篩選抽屜（`p-drawer`）只有一個關鍵字輸入框
- Session storage：`course-group-list-filters` / `course-group-list-sort` / `course-group-list-page`
- **刪除確認訊息（安全關鍵）**：
  - 一律顯示：`確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？`
  - `courseCount > 0` 時**追加紅字警告**：`此群組下的 ${item.courseCount} 門課程將一併被刪除，且無法復原。`
- 刪除回 409 時，用 toast 顯示「群組使用中」（仍被廠商群組引用），不要只丟泛用的「刪除失敗」

### Form page

Reactive Forms，一個 `p-card` 內只有一個控制項：

| 欄位 | 控制項 | 必填 | 備註 |
|------|--------|------|------|
| 群組名稱 | `input pInputText` | ✅ | maxlength 100 |

- 沒有 FK，所以**不需要** `forkJoin` 載入 lookup——`ngOnInit` 在編輯模式下只呼叫一次 `getById`
- 存檔一律用 `form.getRawValue()`（CLAUDE.md 既有規範）
- Sticky `p-toolbar`（儲存／取消）

### Detail page

唯讀顯示主代碼、群組名稱、對應課程數。工具列：編輯、刪除、返回列表。
（Primary-Foreign 導覽按鈕待子表 feature 建好後再加。）

### Sidebar

`MENU_GROUP = 課程管理 Course` — **群組已存在**（Partner 已建立），因此是**加一個 NavItem**，非新群組：

```ts
// 加進既有的「課程管理 Course」群組 items 陣列（接在 合作廠商 Partner 之後）
{ label: '課程群組 CourseGroup', icon: 'pi pi-sitemap', route: '/course-groups' }
```

---

## Tests

### Backend（`CMS.API.Tests`）

- `Controllers/CourseGroupsControllerTests.cs` — 以 NSubstitute 替身 `ICourseGroupRepository`：
  list、query、get-by-id（找到 / 404）、create（201 + Location）、update（204 / 404）、
  delete（204 / 404 / **409 FK 違反**，用 `SqlExceptionFactory.Create(547)`）
- `Repositories/CourseGroupRepositoryIntegrationTests.cs` — `[IntegrationFact]`，打真實 `CMS` 資料庫，
  自行 seed `TEST_` 前綴的 row，斷言用「包含」而非精確筆數；LIKE 逸出：keyword 給 `%` 不會撈出全部

> 整合測試清掃：`DatabaseFixture` 加掃 `DELETE FROM dbo.CourseGroup WHERE Description LIKE 'TEST\_%'
> ESCAPE '\'`。CourseGroup 的 seed row 以 `Description` 辨識（有 100 字空間放前綴）。若測試有 seed
> `PartnerCourseGroup` 子 row，需**先刪子 row** 再刪 CourseGroup（否則 547）；`Course` 子 row 則由
> cascade 自動清掉。**絕不動既有的正式資料，也不 seed 會誤刪正式課程的群組。**

### Frontend（Karma + Jasmine）

- `course-group.service.spec.ts` — `HttpTestingController` 驗證每個方法打對 URL 與 verb
- `course-group-list.spec.ts` — 含「`courseCount > 0` 時刪除確認文案帶課程數警告」與「409 → 群組使用中」兩個斷言
- `course-group-detail.spec.ts` / `course-group-form.spec.ts` — 以假 service 掛載，斷言渲染與必填驗證

---

## Files to Create / Modify

### Backend

| 檔案 | 動作 |
|------|------|
| `src/CMS.API/Models/CourseGroup.cs` | 新增 |
| `src/CMS.API/Models/CourseGroupRequest.cs` | 新增 |
| `src/CMS.API/Models/CourseGroupQuery.cs` | 新增 |
| `src/CMS.API/Models/Lookups/CourseGroupLookup.cs` | 新增 |
| `src/CMS.API/Repositories/ICourseGroupRepository.cs` | 新增 |
| `src/CMS.API/Repositories/CourseGroupRepository.cs` | 新增 |
| `src/CMS.API/Controllers/CourseGroupsController.cs` | 新增 |
| `src/CMS.API/Repositories/ILookupRepository.cs` | 修改 — 加 `GetCourseGroupsAsync` |
| `src/CMS.API/Repositories/LookupRepository.cs` | 修改 — 加 `GetCourseGroupsAsync` |
| `src/CMS.API/Controllers/LookupsController.cs` | 修改 — 加 `GET api/lookups/course-groups` |
| `src/CMS.API/Program.cs` | 修改 — DI 註冊 `ICourseGroupRepository` |

### Frontend

| 檔案 | 動作 |
|------|------|
| `core/models/course-group.model.ts` | 新增 |
| `core/services/course-group.service.ts` | 新增 |
| `features/course-groups/course-group-list/*` | 新增（ts / html / scss）|
| `features/course-groups/course-group-detail/*` | 新增（ts / html / scss）|
| `features/course-groups/course-group-form/*` | 新增（ts / html / scss）|
| `core/services/lookup.service.ts` | 修改 — 加 `getCourseGroups()` |
| `app.routes.ts` | 修改 — 4 條 lazy route |
| `app.ts` | 修改 — 在既有「課程管理 Course」群組加一個 NavItem |

### Tests

| 檔案 | 動作 |
|------|------|
| `CMS.API.Tests/Controllers/CourseGroupsControllerTests.cs` | 新增 |
| `CMS.API.Tests/Repositories/CourseGroupRepositoryIntegrationTests.cs` | 新增 |
| `CMS.API.Tests/Infrastructure/DatabaseFixture.cs` | 修改 — 加 CourseGroup 清掃 |
| `core/services/course-group.service.spec.ts` | 新增 |
| `features/course-groups/*/*.spec.ts` | 新增（list / detail / form）|
