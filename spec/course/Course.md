# Build Spec for Course
- database schema: `.\database\course.sql`

## Summary

`Course` 是整個系統的核心實體——一門由合作廠商開設的訓練課程。它欄位最多（23 欄）、關聯最複雜：
三個 FK（`Partner`、`CourseGroup`、`PublishStatus`）、**兩組真正的 N-N**（`Certification`、`JobCategory`），
以及日期、金額、`nvarchar(max)` 長文欄位。它是本專案第一張同時具備「FK 下拉 + N-N 多選 + 日期」的表，
會用到 AppRole 的交易式 N-N 同步、Partner/CourseGroup 的 lookup，以及已註冊但至今未用到的
`DateOnly` type handler。

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **int** IDENTITY（真主鍵，路由帶 `:int`）|
| Foreign Keys | `Partner_pkid`→`Partner`（必填）、`CourseGroup_pkid`→`CourseGroup`（**可為 null**）、`PublishStatus_pkid`→`PublishStatus`（必填）|
| Required Fields | Title、CourseId、ProdCourseId、FriendlyUrl、DisplayOrder、Partner_pkid、PublishStatus_pkid、ScheduleOn、ScheduleOff、Hour、ListPrice、LearningCredit、CanRepeat |
| N-N Relationships | `CourseInCertification`（Course↔Certification）、`CourseJobCategories`（Course↔JobCategory）—— 皆為純 junction |
| Primary-Foreign Links | `CourseFAQ`、`CourseRelatedLink`、`HotCourse`（無 cascade，擋刪除）；`CourseInCertification`、`CourseJobCategories`（cascade）|
| Query Filters | keyword、Partner、CourseGroup、PublishStatus、CanRepeat（三態）、ScheduleOn 區間、ScheduleOff 區間 |
| Default Sort | `DisplayOrder ASC, pkid ASC`（同 Partner 慣例）|

---

## Localization

### Chinese Table Name

- Course: 課程
- Description: 訓練課程主資料

### Chinese Column Names

- pkid: 主代碼
- Title: 課程名稱
- OfficialTitle: 官方課程名稱
- CourseId: 簡介代碼
- ProdCourseId: 科目代碼
- FriendlyUrl: 友善網址
- DisplayOrder: 顯示順序
- Partner_pkid: 原廠（合作廠商）
- CourseGroup_pkid: 課程群組
- PublishStatus_pkid: 上架狀態
- ScheduleOn: 上架日期
- ScheduleOff: 下架日期
- Hour: 時數
- ListPrice: 定價
- LearningCredit: 點數
- Material: 教材
- Objective: 課程目標
- Target: 適合對象
- Prerequisites: 先備知識
- Outline: 課程大綱
- TowardCertOrExam: 考試／認證說明
- Note: 備註
- OtherInfo: 其他資訊
- CanRepeat: 允許重聽

> `CourseId`／`ProdCourseId` 的中文取自使用者提供的清單欄位標示（簡介代碼／科目代碼），非字面直譯。

---

## Required Fields

Required (NOT NULL，排除 IDENTITY PK)：

- `Title` nvarchar(200)、`CourseId` varchar(50)、`ProdCourseId` varchar(50)、`FriendlyUrl` nvarchar(100)
- `DisplayOrder` int、`Partner_pkid` smallint、`PublishStatus_pkid` tinyint
- `ScheduleOn` date、`ScheduleOff` date、`Hour` smallint
- `ListPrice` decimal(9,0)、`LearningCredit` decimal(9,1)、`CanRepeat` bit

Optional (nullable)：`OfficialTitle`、`CourseGroup_pkid`、`Material`、`Objective`、`Target`、
`Prerequisites`、`Outline`、`TowardCertOrExam`、`Note`、`OtherInfo`。

> DB 對 `Hour`／`ListPrice`／`LearningCredit`／`CanRepeat` 都有 `DEFAULT 0`。前端表單也用 0 當預設，兩邊一致。
> **沒有 UNIQUE constraint**（連 `CourseId` 都沒有）——後端不做 2627 重複鍵處理。

---

## Foreign Keys

| FK 欄位 | → 主表 | 可 null | 選項標籤 | 排序 | 別名 |
|---------|--------|---------|----------|------|------|
| `Partner_pkid` | `Partner.pkid` | 否 | `Partner.Name` | `DisplayOrder ASC, pkid ASC` | `PartnerPkid` |
| `CourseGroup_pkid` | `CourseGroup.pkid` | **是**（下拉含「無」）| `CourseGroup.Description` | `Description ASC` | `CourseGroupPkid` |
| `PublishStatus_pkid` | `PublishStatus.pkid` | 否 | `PublishStatus.Description` | `pkid ASC` | `PublishStatusPkid` |

- `_pkid` 欄位在 SELECT 一律 `AS {Fk}Pkid`，對應 C# 屬性名。
- **列表／檢視顯示的是標籤而非原始 pkid**：使用者明確要求「JOIN 關聯表並顯示該欄位」。因此
  SELECT 直接 JOIN 三張表，回傳 `PartnerName`、`CourseGroupName`、`PublishStatusName` 三個扁平標籤欄位
  （見 SQL — SELECT）。**不採用** convention 的巢狀 nav 物件（multi-map）：本專案無 multi-map 前例，
  扁平標籤欄位更好綁定、也更好測；`CourseGroup` 可為 null 用 `LEFT JOIN` 自然帶出 null 標籤。

`PublishStatus` 是固定枚舉表（`pkid tinyint`，**非** IDENTITY），內容為草稿／上架／下架等狀態。

---

## Foreign-Primary Links

Course → 各主表 detail 的往外連結（列表、檢視、表單）：

- **Partner** → `/partners/{partnerPkid}`
- **CourseGroup** → `/course-groups/{courseGroupPkid}`（僅當非 null）
- **PublishStatus** → 目前無 PublishStatus 的 CRUD 頁，故不連結（只顯示標籤）

> Partner 與 CourseGroup 的 detail 頁**已存在**，這兩個往外連結可以做（在檢視頁把標籤做成連結）。
> 為與現有 slice 一致、且避免列表列過度擁擠，本次列表僅顯示標籤文字；連結只加在**檢視頁**。

---

## Primary-Foreign Links

以下各表以 FK 指向 `Course.pkid`：

| 子表 | FK 欄位 | 刪除行為 | 子系統 |
|------|---------|----------|--------|
| `CourseInCertification` | `Course_pkid` | `ON DELETE CASCADE` | course（N-N，見下）|
| `CourseJobCategories` | `Course_pkid` | `ON DELETE CASCADE` | course（N-N，見下）|
| `CourseFAQ` | `Course_pkid` | 無 cascade（**擋刪除**）| course |
| `CourseRelatedLink` | `Course_pkid` | 無 cascade（**擋刪除**）| course |
| `HotCourse` | `Course_pkid` | 無 cascade（**擋刪除**）| course |

規劃中的導覽（沿用慣例，本次**不實作按鈕**——`CourseFAQ`／`CourseRelatedLink`／`HotCourse` 都還沒有
Angular 路由，做出來只會是壞按鈕）：

- **CourseFAQ** —「對應課程問答」→ `/course-faqs?coursePkid={pkid}`
- **CourseRelatedLink** —「對應相關連結」→ `/course-related-links?coursePkid={pkid}`
- **HotCourse** —「對應熱門課程」→ `/hot-courses?coursePkid={pkid}`

> ### 刪除語意
> - **兩個 N-N junction 會 cascade**：刪 Course 時 `CourseInCertification`／`CourseJobCategories`
>   的關聯列自動清掉，不擋刪除。
> - **`CourseFAQ`／`CourseRelatedLink`／`HotCourse` 不 cascade**：仍被引用時刪除拋 SQL 547 →
>   Controller 攔成 `409 Conflict`「課程使用中」。
> - Repository 直接 `DELETE`（junction 交給 cascade，不需先手動清），547 讓它往上拋。

---

## N-N Relationships

兩組都是**純 junction**（複合 PK 剛好兩個 FK 欄、無酬載欄位），比照 AppRole↔AppUser 的交易式同步。

### CourseInCertification — Course ↔ Certification

```sql
CREATE TABLE dbo.CourseInCertification(
    Course_pkid int NOT NULL, Certification_pkid int NOT NULL,
    PRIMARY KEY (Course_pkid, Certification_pkid))  -- 兩 FK 皆 ON DELETE CASCADE(Course 側) 
```

- 表單（新增／編輯）：`p-multiselect` 選 Certification，選項標籤 = `Certification.Title`（`nchar(100)` → **RTRIM**），
  來源 `GET /api/lookups/certifications`。
- 檢視頁：以 chip 顯示已關聯的 Certification 標籤。
- Request 欄位：`CertificationPkids : List<int>`（Certification.pkid 是 int）。

### CourseJobCategories — Course ↔ JobCategory

```sql
CREATE TABLE dbo.CourseJobCategories(
    Course_pkid int NOT NULL, JobCategory_pkid smallint NOT NULL,
    PRIMARY KEY (Course_pkid, JobCategory_pkid))
```

- 表單：`p-multiselect` 選 JobCategory，選項標籤 = `JobCategory.Description`，來源 `GET /api/lookups/job-categories`。
- 檢視頁：以 chip 顯示已關聯的 JobCategory 標籤。
- Request 欄位：`JobCategoryPkids : List<short>`（JobCategory.pkid 是 smallint）。

### 同步方式（Create 與 Update 都做）

在**與純量 INSERT/UPDATE 同一個 `SqlTransaction` 內**，對每組 junction：

```sql
DELETE FROM dbo.CourseInCertification WHERE Course_pkid = @Pkid;
-- 再逐筆 INSERT (Course_pkid, Certification_pkid)（先 Distinct，避免複合 PK 撞 2627）
DELETE FROM dbo.CourseJobCategories  WHERE Course_pkid = @Pkid;
-- 再逐筆 INSERT (Course_pkid, JobCategory_pkid)
```

比照 `AppRoleRepository.SyncUserRolesAsync`：delete-then-reinsert、`.Distinct()` 去重、全程在交易內，
任一步失敗整筆 rollback。

---

## Query Filters

- **keyword**: string — LIKE on `Title`、`OfficialTitle`、`CourseId`、`ProdCourseId`、`FriendlyUrl`
  （逸出走 `SqlLike.ToPattern`）。長文欄位（`nvarchar(max)`／4000）**不納入**。
- **PartnerPkid**: short? — `Partner_pkid` 完全比對，下拉來源 `lookups/partners`
- **CourseGroupPkid**: short? — `CourseGroup_pkid` 完全比對，下拉來源 `lookups/course-groups`
- **PublishStatusPkid**: byte? — `PublishStatus_pkid` 完全比對，下拉來源 `lookups/publish-statuses`
- **CanRepeat**: bool? — 三態：null=不篩、true=是、false=否
- **ScheduleOn 區間**: `ScheduleOnFrom` / `ScheduleOnTo`（含端點）
- **ScheduleOff 區間**: `ScheduleOffFrom` / `ScheduleOffTo`（含端點）

---

## Lookup Endpoints Required

| Route | Status | Returns / Order |
|-------|--------|-----------------|
| `GET /api/lookups/partners` | **Exists** | `{Pkid, Name}` |
| `GET /api/lookups/course-groups` | **Exists** | `{Pkid, Description}` |
| `GET /api/lookups/publish-statuses` | **New** | `{Pkid, Description}`，`ORDER BY pkid ASC` |
| `GET /api/lookups/certifications` | **New** | `{Pkid, Title}`（Title `nchar` → RTRIM），`ORDER BY Title ASC` |
| `GET /api/lookups/job-categories` | **New** | `{Pkid, Description}`，`ORDER BY Description ASC` |

> sample1 標這些為「Exists」是通用範本的說法；本專案目前只有 partners / course-groups 兩支，其餘三支都要新建。

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/courses` | 全部，`ORDER BY DisplayOrder ASC, pkid ASC` |
| `POST` | `/api/courses/query` | 條件查詢（body: `CourseQuery`）|
| `GET` | `/api/courses/{id:int}` | 取單筆（含兩組 N-N pkid 清單）|
| `POST` | `/api/courses` | 新增 → `201 Created` + 新 pkid |
| `PUT` | `/api/courses` | 更新（pkid 取自 body）→ `204` / `404` |
| `DELETE` | `/api/courses/{id:int}` | 刪除 → `204` / `404` / **`409`（仍被 FAQ／相關連結／熱門課程引用）** |

無認證。**本次不做** sample1 提到的 `POST /api/courses/{id}/copy` 複製端點——超出標準 CRUD，且非使用者要求；
日後要做再單獨加。

---

## Backend Notes

### Models

```csharp
// Models/Course.cs — 回應模型
public class Course
{
    public int Pkid { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OfficialTitle { get; set; }
    public string CourseId { get; set; } = string.Empty;
    public string ProdCourseId { get; set; } = string.Empty;
    public string FriendlyUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public short PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte PublishStatusPkid { get; set; }
    public DateOnly ScheduleOn { get; set; }
    public DateOnly ScheduleOff { get; set; }
    public short Hour { get; set; }
    public decimal ListPrice { get; set; }
    public decimal LearningCredit { get; set; }
    public string? Material { get; set; }
    public string? Objective { get; set; }
    public string? Target { get; set; }
    public string? Prerequisites { get; set; }
    public string? Outline { get; set; }
    public string? TowardCertOrExam { get; set; }
    public string? Note { get; set; }
    public string? OtherInfo { get; set; }
    public bool CanRepeat { get; set; }

    // JOIN 帶出的顯示標籤（唯讀，非寫入欄位）
    public string PartnerName { get; set; } = string.Empty;
    public string? CourseGroupName { get; set; }
    public string PublishStatusName { get; set; } = string.Empty;

    // 僅在 GetById 填入
    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}

// Models/CourseRequest.cs — 寫入 DTO（含 DataAnnotations；排除三個 *Name 標籤欄位）
public class CourseRequest
{
    public int Pkid { get; set; }                                  // INSERT 時忽略

    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [StringLength(300)] public string? OfficialTitle { get; set; }
    [Required, StringLength(50)]  public string CourseId { get; set; } = string.Empty;
    [Required, StringLength(50)]  public string ProdCourseId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string FriendlyUrl { get; set; } = string.Empty;
    [Range(0, int.MaxValue)] public int DisplayOrder { get; set; }
    [Required] public short PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    [Required] public byte PublishStatusPkid { get; set; }
    [Required] public DateOnly ScheduleOn { get; set; }
    [Required] public DateOnly ScheduleOff { get; set; }
    [Range(0, short.MaxValue)] public short Hour { get; set; }
    [Range(0, 999999999)] public decimal ListPrice { get; set; }
    [Range(0, 99999999.9)] public decimal LearningCredit { get; set; }
    [StringLength(500)]  public string? Material { get; set; }
    [StringLength(4000)] public string? Objective { get; set; }
    [StringLength(500)]  public string? Target { get; set; }
    [StringLength(4000)] public string? Prerequisites { get; set; }
    public string? Outline { get; set; }              // nvarchar(max)
    public string? TowardCertOrExam { get; set; }     // nvarchar(max)
    [StringLength(4000)] public string? Note { get; set; }
    [StringLength(4000)] public string? OtherInfo { get; set; }
    public bool CanRepeat { get; set; }

    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}

// Models/CourseQuery.cs
public class CourseQuery
{
    public string? Keyword { get; set; }
    public short? PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte? PublishStatusPkid { get; set; }
    public bool? CanRepeat { get; set; }
    public DateOnly? ScheduleOnFrom { get; set; }
    public DateOnly? ScheduleOnTo { get; set; }
    public DateOnly? ScheduleOffFrom { get; set; }
    public DateOnly? ScheduleOffTo { get; set; }
}

// Models/Lookups/{PublishStatusLookup, CertificationLookup, JobCategoryLookup}.cs
public class PublishStatusLookup { public byte Pkid { get; set; } public string Description { get; set; } = ""; }
public class CertificationLookup { public int Pkid { get; set; }  public string Title { get; set; } = ""; }
public class JobCategoryLookup   { public short Pkid { get; set; } public string Description { get; set; } = ""; }
```

型別：`smallint`→`short`、`tinyint`→`byte`、`int`→`int`、`decimal`→`decimal`、`date`→`DateOnly`、
`bit`→`bool`。`DateOnly` 需要 `DateOnlyTypeHandler`（**已註冊在 `Program.cs`**，這是它第一次真正被用到）。

### SQL — SELECT

```sql
SELECT c.pkid AS Pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl,
       c.DisplayOrder,
       c.Partner_pkid      AS PartnerPkid,
       c.CourseGroup_pkid  AS CourseGroupPkid,
       c.PublishStatus_pkid AS PublishStatusPkid,
       c.ScheduleOn, c.ScheduleOff, c.Hour, c.ListPrice, c.LearningCredit,
       c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline,
       c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat,
       p.Name        AS PartnerName,
       g.Description AS CourseGroupName,
       s.Description AS PublishStatusName
FROM        dbo.Course c
INNER JOIN  dbo.Partner       p ON p.pkid = c.Partner_pkid
LEFT  JOIN  dbo.CourseGroup   g ON g.pkid = c.CourseGroup_pkid   -- 可為 null
INNER JOIN  dbo.PublishStatus s ON s.pkid = c.PublishStatus_pkid
```

`QueryAsync` 追加 WHERE（全部 null-guard）：keyword 五欄 LIKE、`Partner_pkid`／`CourseGroup_pkid`／
`PublishStatus_pkid` 完全比對、`CanRepeat` 完全比對、`ScheduleOn`／`ScheduleOff` 各自
`>= @From AND <= @To`（`@From`/`@To` 為 null 時該側不限）。`ORDER BY c.DisplayOrder ASC, c.pkid ASC`。

`GetByIdAsync` 用 `QueryMultipleAsync`：第一段取 Course（上面的 SELECT + `WHERE c.pkid=@Pkid`），
再兩段分別 `SELECT Certification_pkid FROM CourseInCertification WHERE Course_pkid=@Pkid` 與
`SELECT JobCategory_pkid FROM CourseJobCategories WHERE Course_pkid=@Pkid`，填入兩個 pkid 清單
（比照 `AppRoleRepository.GetByIdAsync`）。

### SQL — INSERT / UPDATE

INSERT 寫入全部 23 個可寫欄位（排除 IDENTITY `pkid` 與三個 `*Name`），
`SELECT CAST(SCOPE_IDENTITY() AS int);`。UPDATE 同一組欄位（`pkid` 只在 WHERE）。
兩者都在交易內接著跑兩組 junction 的 delete-then-reinsert（見 N-N）。

### RowAudit

**不寫入**——沿用 CLAUDE.md「Deferred, by decision」。sample1 描述的 `RowAuditWriter` /
`AuditHelper.ChangedColumns` 本專案不存在，略過。

---

## Frontend Notes

### Files / Model

```
core/models/course.model.ts            Course / CourseRequest / CourseQuery /
                                        PublishStatusLookup / CertificationLookup / JobCategoryLookup
core/services/course.service.ts        唯一呼叫 /api/courses 的地方
features/courses/course-list|detail|form/
```

TS `Course` 介面欄位對齊後端，日期為 `string`（ISO `yyyy-MM-dd`），另含 `partnerName`、
`courseGroupName: string | null`、`publishStatusName`、`certificationPkids: number[]`、
`jobCategoryPkids: number[]`。

### Routes

`courses`／`courses/new`／`courses/:id/edit`／`courses/:id`（`new` 在 `:id` 前）。

### List page（欄位完全依使用者指定）

| 欄位 | 來源 |
|------|------|
| 主代碼 | `pkid` |
| 顯示順序 | `displayOrder` |
| 簡介代碼 | `courseId` |
| 科目代碼 | `prodCourseId` |
| 課程名稱 | `title` |
| 原廠 | `partnerName`（後端 JOIN）|
| 課程群組 | `courseGroupName`（可空 → 顯示「—」）|
| 上架狀態 | `publishStatusName` |
| 上架日期 | `scheduleOn`（`\| date:'yyyy/MM/dd'`）|
| 下架日期 | `scheduleOff` |
| 時數 | `hour` |
| 定價 | `listPrice` |
| 點數 | `learningCredit` |
| 允許重聽 | `canRepeat`（是/否 或 `pi-check`）|
| 操作 | 檢視／編輯／刪除 |

- 預設排序 `displayOrder` ASC；session key `course-list-filters` / `-sort` / `-page`
- 篩選抽屜：關鍵字輸入框 + 三個 `p-select`（原廠／課程群組／上架狀態，`appendTo="body"`，
  `[filter]="true"`）+ 允許重聽三態 `p-select`（全部／是／否）+ 上架日期區間 + 下架日期區間（`p-datepicker`）
- 下拉選項於 `ngOnInit` 以 `forkJoin` 載入（partners/course-groups/publish-statuses），載入後再套用已存篩選
- 刪除確認：`確定要刪除主代碼 <b>${item.pkid}</b>「${item.courseId} ${item.title}」？`
- 刪除回 409 → toast「課程使用中」（仍被 FAQ／相關連結／熱門課程引用）
- 進入時若帶 `partnerPkid` query param（由 Partner 頁導覽而來），覆蓋已存篩選

### Form page

Reactive Forms + `forkJoin` 平行載入五個 lookup（partners、course-groups、publish-statuses、
certifications、job-categories）＋（編輯時）`getById`。控制項：

| 欄位 | 控制項 | 必填 |
|------|--------|------|
| 課程名稱 / 官方課程名稱 | `pInputText` | 名稱必填 |
| 簡介代碼 / 科目代碼 / 友善網址 | `pInputText` | ✅ |
| 顯示順序 / 時數 | `p-inputnumber` | ✅ |
| 定價 | `p-inputnumber`（min 0，整數）| ✅ |
| 點數 | `p-inputnumber`（min 0，`minFractionDigits=1`）| ✅ |
| 原廠 / 上架狀態 | `p-select` | ✅ |
| 課程群組 | `p-select`（含「無」= null）| ❌ |
| 上架日期 / 下架日期 | `p-datepicker` | ✅ |
| 允許重聽 | `p-toggleswitch`（或 checkbox）| — |
| 認證 / 職務類別 | `p-multiselect`（N-N）| ❌ |
| 教材／適合對象 | `pInputTextarea` | ❌ |
| 課程目標／先備知識／備註／其他資訊 | `pInputTextarea` | ❌ |
| 課程大綱／考試認證說明 | `pInputTextarea`（長文，`nvarchar(max)`）| ❌ |

- **日期序列化用 `core/utils/date.util.ts` 的 `toIso`/`fromIso`**（本地時間分量，避免 UTC+8 差一天）。
  送出前 `DateOnly` 欄位用 `toIso(Date)`；載入時 `fromIso(iso)` 綁 `p-datepicker`。
- **下架日期自動預設**：`scheduleOn` 變動時（`valueChanges`）自動把 `scheduleOff` 設為
  `addYears(scheduleOn, 10)`，用 `{ emitEvent: false }` 避免迴圈；編輯模式先 patch `scheduleOn`
  再 patch `scheduleOff`，讓載入值蓋過自動值。
- 存檔一律 `form.getRawValue()`；空字串長文欄位正規化為 `null`
- N-N：`p-multiselect [maxSelectedLabels]="9999"`；送出時 `certificationPkids` / `jobCategoryPkids` 為 number[]
- Sticky `p-toolbar`（儲存／取消）

### Detail page

唯讀顯示全部欄位；`partnerName`／`courseGroupName`／`publishStatusName` 直接用回傳標籤，其中
Partner、CourseGroup 做成連到各自 detail 的連結。N-N 以 chip 顯示——detail 額外 `forkJoin`
載入 certifications / job-categories lookup，把 pkid 對成標籤（比照 AppRole 檢視頁的使用者 chip）。

> **本次不做**（sample1 有、但依賴不存在的功能）：`複製` 按鈕與 copy 端點、`查看開課時間`
> （ClassSection 不在 DDL）、內嵌子面板（CourseRelatedLink／CourseRecomm inline edit）、QR code、
> 列印 PDF、RowAudit badge。Primary-Foreign 導覽按鈕待子表 feature 建好再補。

### Sidebar

`課程管理 Course` 群組**已存在**，加一個 NavItem（接在 CourseGroup 之後）：
`{ label: '課程 Course', icon: 'pi pi-book', route: '/courses' }`。

---

## Tests

### Backend（`CMS.API.Tests`）

- `Controllers/CoursesControllerTests.cs` — NSubstitute 替身：list、query（轉傳篩選）、get-by-id
  （找到 / 404）、create（201 + Location）、update（204 / 404）、delete（204 / 404 / **409 FK 違反**，
  用 `SqlExceptionFactory.Create(547)`）
- `Repositories/CourseRepositoryIntegrationTests.cs` — `[IntegrationFact]`，打真實 DB：
  - Insert 帶兩組 N-N，GetById 讀回、pkid 清單一致；含 JOIN 標籤（PartnerName 等）正確
  - Update 改 N-N（新增／移除），delete-then-reinsert 生效
  - keyword LIKE 逸出（`%` 不撈全部）；FK / 日期區間 / CanRepeat 篩選各一
  - **需要既有的 Partner／PublishStatus 種子**：seed 測試 Course 需要合法 `Partner_pkid` 與
    `PublishStatus_pkid`。fixture 先查一筆現有 Partner／PublishStatus 的 pkid 當外鍵（**唯讀，不改**），
    Course 本身以 `CourseId LIKE 'TEST\_%'` 種＋掃。日期用固定值（`Date.Now` 在 workflow/測試中不可用，
    測試碼用 `new DateOnly(2026,1,1)` 之類常數）。

> `DatabaseFixture.CleanupAsync` 加一行：`DELETE FROM dbo.Course WHERE CourseId LIKE 'TEST\_%' ESCAPE '\'`
> （`CourseId` varchar(50) 放得下前綴）。Course 的 cascade 會清掉其 N-N 關聯列；但若測試有種
> `CourseFAQ`／`CourseRelatedLink`／`HotCourse` 子列需先刪（本測試不種）。**絕不動正式課程資料。**

### Frontend（Karma + Jasmine）

- `course.service.spec.ts` — 各方法打對 URL/verb
- `course-list.spec.ts` — 渲染、預設 `displayOrder` ASC、FK 篩選轉傳、日期區間、409→「課程使用中」、
  session 還原（注意 `p-table` 就地重排：以 identity 而非索引取列）
- `course-detail.spec.ts` — 渲染標籤與 N-N chip、404 狀態
- `course-form.spec.ts` — 必填驗證、`getRawValue()`、`toIso` 送出的是 `yyyy-MM-dd`、
  scheduleOff 自動 +10 年、N-N pkid 陣列送出

---

## Files to Create / Modify

### Backend
| 檔案 | 動作 |
|------|------|
| `Models/Course.cs`、`CourseRequest.cs`、`CourseQuery.cs` | 新增 |
| `Models/Lookups/{PublishStatusLookup,CertificationLookup,JobCategoryLookup}.cs` | 新增 |
| `Repositories/ICourseRepository.cs`、`CourseRepository.cs` | 新增 |
| `Controllers/CoursesController.cs` | 新增 |
| `Repositories/ILookupRepository.cs`、`LookupRepository.cs` | 修改 — 加 3 支 lookup |
| `Controllers/LookupsController.cs` | 修改 — 加 3 支路由 |
| `Program.cs` | 修改 — DI 註冊 `ICourseRepository` |

### Frontend
| 檔案 | 動作 |
|------|------|
| `core/models/course.model.ts`、`core/services/course.service.ts` | 新增 |
| `features/courses/course-list|detail|form/*` | 新增 |
| `core/services/lookup.service.ts` | 修改 — 加 publishStatuses/certifications/jobCategories |
| `app.routes.ts` | 修改 — 4 條 route |
| `app.ts` | 修改 — 「課程管理 Course」群組加 NavItem |

### Tests
| 檔案 | 動作 |
|------|------|
| `CMS.API.Tests/Controllers/CoursesControllerTests.cs` | 新增 |
| `CMS.API.Tests/Repositories/CourseRepositoryIntegrationTests.cs` | 新增 |
| `CMS.API.Tests/Infrastructure/DatabaseFixture.cs` | 修改 — 加 Course 清掃 + 種子外鍵探測 |
| `core/services/course.service.spec.ts` | 新增 |
| `features/courses/*/*.spec.ts` | 新增（list / detail / form）|
