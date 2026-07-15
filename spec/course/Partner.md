# Build Spec for Partner
- database schema: `.\database\course.sql`（`promotion.sql` 亦重複宣告同一張 Partner，欄位一致）

## Summary

`Partner` 是課程體系的頂層分類實體，代表一家合作的原廠／訓練夥伴（例如 Microsoft、Cisco）。
它本身沒有任何外來鍵，欄位單純：一組名稱（清單用 `Name`、選單用 `NameOnPartnerMenu`、課程明細頁用
`NameOnCourseDetailPage`）、一個短代碼 `AppKey`、顯示順序，以及選填的圖檔名。

它的份量來自「被誰參照」：`Course`、`Certification`、`PartnerCourseGroup`、`Promotion2`、`Seminar`
全都以 FK 指向 `Partner.pkid`。因此本表除了 CRUD 之外，還必須提供
`GET /api/lookups/partners`，供上述各表的下拉選單使用。

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint** IDENTITY（真主鍵，與 AppRole 的 `pkid` 不同，可安全用於路由）|
| Foreign Keys | 無 — Partner 不參照任何表 |
| Required Fields | `Name`、`AppKey`、`NameOnPartnerMenu`、`NameOnCourseDetailPage`、`DisplayOrder` |
| N-N Relationships | N/A — 見下方「N-N Relationships」對 `PartnerCourseGroup` 的說明 |
| Primary-Foreign Links | `Course`、`Certification`、`PartnerCourseGroup`、`Promotion2`、`Seminar`（**目前皆尚未建置**）|
| Query Filters | keyword（`Name`、`AppKey`、`NameOnPartnerMenu`、`NameOnCourseDetailPage`）|
| Default Sort | `DisplayOrder ASC, pkid ASC` |

---

## Localization

### Chinese Table Name

- Partner: 合作廠商
- Description: 課程的合作原廠／訓練夥伴主資料

### Chinese Column Names

- pkid: 主代碼
- Name: 廠商名稱
- AppKey: 廠商代碼
- NameOnPartnerMenu: 廠商選單顯示名稱
- NameOnCourseDetailPage: 課程明細頁顯示名稱
- DisplayOrder: 顯示順序
- ImageFilename: 圖片檔名

> `AppKey` 的中文為推斷。DDL 只說明它是 `varchar(10)` NOT NULL；由 `TrainingCenter.AppKey`
> 的同名用法看，它是給程式／網址使用的短代碼，故譯為「廠商代碼」。

---

## Required Fields

Required (NOT NULL)：

- `Name` — nvarchar(50)
- `AppKey` — varchar(10)
- `NameOnPartnerMenu` — nvarchar(200)
- `NameOnCourseDetailPage` — nvarchar(50)
- `DisplayOrder` — int

Optional (nullable)：

- `ImageFilename` — varchar(50) NULL

> **`AppKey` 沒有 UNIQUE constraint。** DDL 上只有 `PK_Partner(pkid)`，資料庫不保證 `AppKey`
> 不重複，因此後端**不做** 2627/2601 重複鍵的特別處理（AppRole 需要，是因為 `RoleId` 真的是鍵）。

---

## Foreign Keys

**N/A** — `Partner` 沒有任何外來鍵欄位。

---

## Foreign-Primary Links

**N/A** — 沒有 FK，因此沒有往外的導覽連結。

---

## Primary-Foreign Links

以下各表以 FK 指向 `Partner.pkid`：

| 子表 | FK 欄位 | 可為 null | 子系統 |
|------|---------|-----------|--------|
| `Course` | `Partner_pkid` | 否 | course |
| `Certification` | `Partner_pkid` | 否 | course |
| `PartnerCourseGroup` | `Partner_pkid` | 否 | course |
| `Promotion2` | `RelatedPartner_pkid` | 是 | promotion |
| `Seminar` | `Partner_pkid` | 是 | promotion |

規劃中的導覽形式（沿用 sample1 的慣例）：

- **Course** — 欄位標題「對應課程」、按鈕「查看課程」（`pi pi-book`）→ `/courses?partnerPkid={pkid}`
- **Certification** — 「對應認證」、「查看認證」（`pi pi-verified`）→ `/certifications?partnerPkid={pkid}`
- **PartnerCourseGroup** — 「對應課程群組」、「查看課程群組」（`pi pi-sitemap`）→ `/partner-course-groups?partnerPkid={pkid}`

> **本次不實作這些按鈕。** 上列五個 feature 目前都還沒有 Angular 路由；`app.routes.ts` 的萬用路由
> `{ path: '**', redirectTo: 'app-roles' }` 會把任何一個連結吃掉並跳回角色列表，做出來只會是壞按鈕。
> 等對應的 `/crud` 跑完、路由存在了，再回頭把按鈕加進 Partner 的 list 與 detail 頁。
>
> **刪除時的 FK 保護仍要做**：這些 FK 都沒有 `ON DELETE CASCADE`，所以刪除一個仍被課程／認證引用的
> Partner 會噴 SQL error 547。後端必須攔下來回 `409 Conflict`，而不是讓它變成 500。

---

## N-N Relationships

**N/A。**

`PartnerCourseGroup` 乍看是 Partner ↔ CourseGroup 的中介表，但它不是純粹的 junction：

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

它有自己的 IDENTITY 主鍵、一個 **NOT NULL 的 `Description`**，而且 `Promotion2` 還以
`RelatedPartnerCourseGroup_pkid` 直接參照它的 `pkid`——它是一個獨立實體，不是連接線。

若把它做成 Partner 表單裡的 `p-multiselect`，使用者沒有地方可以填那個必填的 `Description`，
存檔必定違反 NOT NULL。因此本次**不**把它內嵌進 Partner，而是視為子實體（見上方 Primary-Foreign
Links），日後獨立跑一次 `/crud PartnerCourseGroup`。

---

## Query Filters

- **keyword**: string
  - LIKE on `Name`、`AppKey`、`NameOnPartnerMenu`、`NameOnCourseDetailPage`
  - 逸出 `\ % _ [` 並搭配 `ESCAPE '\'`（沿用 `AppRoleRepository.ToLikePattern`）
  - `ImageFilename` 不納入 keyword：它是檔名，不是識別資訊

沒有 FK 篩選、沒有 bit 欄位、沒有日期欄位，因此篩選抽屜只有一個關鍵字欄位。

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | **New** | `PartnerLookup { Pkid, Name }`，`ORDER BY DisplayOrder ASC, pkid ASC` |

Partner 自己不需要任何 lookup（無 FK），但它**是** `Course` / `Certification` /
`PartnerCourseGroup` / `Promotion2` / `Seminar` 的 FK 目標，所以這支端點現在就要建。

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/partners` | 全部，`ORDER BY DisplayOrder ASC, pkid ASC` |
| `POST` | `/api/partners/query` | 條件查詢（body: `PartnerQuery`）|
| `GET` | `/api/partners/{id:int}` | 依 pkid 取單筆 |
| `POST` | `/api/partners` | 新增 → `201 Created` + 新 pkid |
| `PUT` | `/api/partners` | 更新（pkid 取自 body）→ `204` / `404` |
| `DELETE` | `/api/partners/{id:int}` | 刪除 → `204` / `404` / **`409`（仍被子表引用）** |
| `GET` | `/api/lookups/partners` | 下拉用精簡清單 |

> 路由帶 `:int` 約束——`pkid` 是真正的 IDENTITY 主鍵，這裡跟 AppRole 的字串鍵不同，
> 不需要 `encodeURIComponent`，也不會有 `/new` 撞 `/:id` 的問題（`:int` 就擋掉了）。
> 但 Angular 路由順序仍照慣例把 `partners/new` 寫在 `partners/:id` 之前。

無認證（見 CLAUDE.md「Deferred, by decision」）。

---

## Backend Notes

### Models

```csharp
// Models/Partner.cs — 回應模型
public class Partner
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }

    /// <summary>唯讀統計，供列表顯示「對應課程數」。</summary>
    public int CourseCount { get; set; }
}

// Models/PartnerRequest.cs — 寫入 DTO
public class PartnerRequest
{
    public short Pkid { get; set; }                       // INSERT 時忽略

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string AppKey { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    [StringLength(50)]
    public string? ImageFilename { get; set; }            // 選填
}

// Models/PartnerQuery.cs — 查詢 DTO
public class PartnerQuery
{
    public string? Keyword { get; set; }
}

// Models/Lookups/PartnerLookup.cs
public class PartnerLookup
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

型別對照：`smallint` → `short`（`pkid` 亦然）、`int` → `int`、`varchar`/`nvarchar` NOT NULL →
`string`、NULL → `string?`。本表沒有 `date` / `time` / `nchar` / `decimal` / `bit` 欄位，
因此不需要 `RTRIM()`，也用不到 `DateOnly` / `TimeOnly` handler（它們仍註冊在 `Program.cs`，供日後的
`Course.ScheduleOn` 使用）。

### SQL — SELECT

```sql
SELECT p.pkid AS Pkid, p.Name, p.AppKey, p.NameOnPartnerMenu,
       p.NameOnCourseDetailPage, p.DisplayOrder, p.ImageFilename,
       (SELECT COUNT(*) FROM dbo.Course c WHERE c.Partner_pkid = p.pkid) AS CourseCount
FROM   dbo.Partner p
```

`GetAllAsync` / `QueryAsync` / `GetByIdAsync` 共用這段（沿用 `AppRoleRepository.SelectColumns`
的 `const string` 內插寫法）。沒有 FK，因此不需要 multi-map、也沒有 `splitOn`。

`QueryAsync` 的 WHERE：

```sql
WHERE (@Keyword IS NULL
       OR p.Name                   LIKE @Like ESCAPE '\'
       OR p.AppKey                 LIKE @Like ESCAPE '\'
       OR p.NameOnPartnerMenu      LIKE @Like ESCAPE '\'
       OR p.NameOnCourseDetailPage LIKE @Like ESCAPE '\')
ORDER BY p.DisplayOrder ASC, p.pkid ASC;
```

### SQL — INSERT

```sql
INSERT INTO dbo.Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage,
                         DisplayOrder, ImageFilename)
VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage,
        @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

`pkid` 是 IDENTITY，排除在外。`SCOPE_IDENTITY()` 回傳 `numeric(38,0)`，必須 CAST；
這裡 CAST 成 `smallint` 以對應 `Partner.Pkid` 的型別。

### SQL — UPDATE

```sql
UPDATE dbo.Partner
SET    Name = @Name,
       AppKey = @AppKey,
       NameOnPartnerMenu = @NameOnPartnerMenu,
       NameOnCourseDetailPage = @NameOnCourseDetailPage,
       DisplayOrder = @DisplayOrder,
       ImageFilename = @ImageFilename
WHERE  pkid = @Pkid;
```

所有非 IDENTITY 欄位都可改——包括 `AppKey`。它不是鍵、沒有 FK 指向它，改名不會孤立任何 row，
所以**不**比照 AppRole 把它設為 immutable。

### SQL — DELETE

```sql
DELETE FROM dbo.Partner WHERE pkid = @Pkid;
```

不需要先清 junction（沒有 junction）。但 `Course` / `Certification` / `PartnerCourseGroup` /
`Promotion2` / `Seminar` 的 FK 都**沒有** `ON DELETE CASCADE`，所以刪除一個仍被引用的 Partner 會拋
SQL error **547**。Repository 讓它往上拋，Controller 用既有的
`SqlErrorNumbers.ForeignKeyViolation (547)` 攔下來回 `409 Conflict`，訊息說明「此廠商仍有課程／認證
使用中，無法刪除」。

### N-N Sync Pattern

**N/A** — 無 N-N。也因此 repository **不需要**開 transaction：每個寫入操作都是單一 statement。
（AppRole 之所以開 `SqlTransaction`，是為了 AppUserRole 的 n-n 同步；這裡沒有那個需求。）

### RowAudit

**不寫入**——沿用 CLAUDE.md「Deferred, by decision」的既定決策：目前沒有登入機制，就沒有真實的
`UserName` 可記，塞 `"system"` 只是把假資料寫進稽核表。等 auth 落地時，注入 `IAuditWriter` 到
repository 即可。`/crud` skill 的樣板提到 `RowAuditWriter` 與 `RowAuditBadgeComponent`，
本專案兩者皆不存在，故略過。

---

## Frontend Notes

### Files

```
core/models/partner.model.ts                Partner / PartnerRequest / PartnerQuery / PartnerLookup
core/services/partner.service.ts            唯一呼叫 /api/partners 的地方
features/partners/partner-list/             列表 + 篩選抽屜
features/partners/partner-detail/           檢視
features/partners/partner-form/             新增／編輯共用
```

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `partners` | `PartnerList` | 合作廠商 Partner |
| `partners/new` | `PartnerForm` | 新增合作廠商 |
| `partners/:id/edit` | `PartnerForm` | 編輯合作廠商 |
| `partners/:id` | `PartnerDetail` | 檢視合作廠商 |

`new` 仍宣告在 `:id` 之前（照慣例；此處 `pkid` 是數字，本來也不會撞）。

### List page

- 欄位：主代碼、廠商名稱、廠商代碼、廠商選單顯示名稱、課程明細頁顯示名稱、顯示順序、圖片檔名、對應課程數、操作
- 預設排序：`displayOrder` ASC
- `p-table` 可排序、可分頁；篩選抽屜（`p-drawer`）只有一個關鍵字輸入框
- Session storage：`partner-list-filters` / `partner-list-sort` / `partner-list-page`
- 刪除確認訊息：`確定要刪除主代碼 <b>${item.pkid}</b>「${item.name}」？`
- 刪除回 409 時，用 toast 顯示後端訊息（廠商仍被引用），不要只丟一個泛用的「刪除失敗」

### Form page

Reactive Forms，一個 `p-card` 內排下列控制項：

| 欄位 | 控制項 | 必填 | 備註 |
|------|--------|------|------|
| 廠商名稱 | `input pInputText` | ✅ | maxlength 50 |
| 廠商代碼 | `input pInputText` | ✅ | maxlength 10；**編輯時不鎖定**（非鍵值）|
| 廠商選單顯示名稱 | `input pInputText` | ✅ | maxlength 200 |
| 課程明細頁顯示名稱 | `input pInputText` | ✅ | maxlength 50 |
| 顯示順序 | `p-inputnumber` | ✅ | 整數，預設 0 |
| 圖片檔名 | `input pInputText` | ❌ | maxlength 50 |

- 沒有 FK，所以**不需要** `forkJoin` 載入 lookup——`ngOnInit` 在編輯模式下只呼叫一次 `getById`
- 存檔一律用 `form.getRawValue()`，不用 `form.value`（CLAUDE.md 的既有規範；本表雖無 disabled 控制項，
  仍照規範走，避免日後加了 disabled 欄位時默默掉值）
- 空字串的 `imageFilename` 送出前正規化為 `null`，避免把 `''` 寫進 nullable 欄位
- Sticky `p-toolbar`（儲存／取消）

### Detail page

唯讀顯示全部七個欄位 + 對應課程數。工具列：編輯、刪除、返回列表。
（Primary-Foreign 導覽按鈕待子表 feature 建好後再加，理由見上。）

### Sidebar

`MENU_GROUP = 課程管理 Course` — **新群組**，目前 `app.ts` 只有「系統管理 Admin」。
新增：

```ts
{
  label: '課程管理 Course',
  icon: 'pi pi-book',
  expanded: true,
  items: [{ label: '合作廠商 Partner', icon: 'pi pi-building', route: '/partners' }]
}
```

---

## Tests

### Backend（`CMS.API.Tests`）

- `Controllers/PartnersControllerTests.cs` — 以 NSubstitute 替身 `IPartnerRepository`：
  list、query、get-by-id（找到 / 404）、create（201 + Location）、update（204 / 404）、
  delete（204 / 404 / **409 FK 違反**）
- `Repositories/PartnerRepositoryIntegrationTests.cs` — `[IntegrationFact]`，打真實 `CMS` 資料庫，
  自行 seed `TEST_` 前綴的 row，`DatabaseFixture` 開頭與結尾都清掃；斷言用「包含」而非精確筆數
- LIKE 逸出：keyword 給 `%`，斷言不會撈出全部

> 整合測試的清掃述詞需要擴充：現有 `DatabaseFixture` 只掃 `AppRole`。Partner 的 seed row 以
> `AppKey LIKE 'TEST\_%' ESCAPE '\'` 辨識，且必須**先刪 Course/Certification 等子 row**（若測試有建）
> 才能刪 Partner。**絕不動既有的正式 Partner 資料。**

### Frontend（Karma + Jasmine）

- `partner.service.spec.ts` — `HttpTestingController` 驗證每個方法打對 URL 與 verb
- `partner-list.spec.ts` / `partner-detail.spec.ts` / `partner-form.spec.ts` — 以假 service 掛載，
  斷言能渲染、必填驗證會擋、送出時用的是 `getRawValue()`

---

## Files to Create / Modify

### Backend

| 檔案 | 動作 |
|------|------|
| `src/CMS.API/Models/Partner.cs` | 新增 |
| `src/CMS.API/Models/PartnerRequest.cs` | 新增 |
| `src/CMS.API/Models/PartnerQuery.cs` | 新增 |
| `src/CMS.API/Models/Lookups/PartnerLookup.cs` | 新增 |
| `src/CMS.API/Repositories/IPartnerRepository.cs` | 新增 |
| `src/CMS.API/Repositories/PartnerRepository.cs` | 新增 |
| `src/CMS.API/Controllers/PartnersController.cs` | 新增 |
| `src/CMS.API/Repositories/ILookupRepository.cs` | 修改 — 加 `GetPartnersAsync` |
| `src/CMS.API/Repositories/LookupRepository.cs` | 修改 — 加 `GetPartnersAsync` |
| `src/CMS.API/Controllers/LookupsController.cs` | 修改 — 加 `GET api/lookups/partners` |
| `src/CMS.API/Program.cs` | 修改 — DI 註冊 `IPartnerRepository` |

### Frontend

| 檔案 | 動作 |
|------|------|
| `core/models/partner.model.ts` | 新增 |
| `core/services/partner.service.ts` | 新增 |
| `features/partners/partner-list/*` | 新增（ts / html / scss）|
| `features/partners/partner-detail/*` | 新增（ts / html / scss）|
| `features/partners/partner-form/*` | 新增（ts / html / scss）|
| `core/services/lookup.service.ts` | 修改 — 加 `getPartners()` |
| `app.routes.ts` | 修改 — 4 條 lazy route |
| `app.ts` / `app.html` | 修改 — 新增「課程管理 Course」導覽群組 |

### Tests

| 檔案 | 動作 |
|------|------|
| `CMS.API.Tests/Controllers/PartnersControllerTests.cs` | 新增 |
| `CMS.API.Tests/Repositories/PartnerRepositoryIntegrationTests.cs` | 新增 |
| `CMS.API.Tests/Infrastructure/DatabaseFixture.cs` | 修改 — 加 Partner 清掃 |
| `core/services/partner.service.spec.ts` | 新增 |
| `features/partners/*/*.spec.ts` | 新增（list / detail / form）|
