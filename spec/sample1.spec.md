# Build Spec for Course
- database schema: .\database\course.sql

## Summary

`Course` is the central entity of the system, representing a training course offered by a partner. It carries full descriptive content (objectives, outline, prerequisites), scheduling dates, pricing, and display metadata. It links to `Partner`, `CourseGroup`, and `PublishStatus` as foreign keys, and has N-N relationships with `Certification` and `JobCategory`. Several other entities (ClassSection, CourseFAQ, CourseRelatedLink, CourseRecomm, HotCourse) reference Course as their parent.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` int IDENTITY |
| Foreign Keys | `Partner_pkid` → `Partner.pkid`, `CourseGroup_pkid` → `CourseGroup.pkid` (nullable), `PublishStatus_pkid` → `PublishStatus.pkid` |
| Required Fields | `Title`, `CourseId`, `ProdCourseId`, `FriendlyUrl`, `DisplayOrder`, `Partner_pkid`, `PublishStatus_pkid`, `ScheduleOn`, `ScheduleOff`, `Hour`, `ListPrice`, `LearningCredit`, `CanRepeat` |
| N-N Relationships | `CourseInCertification` (Course ↔ Certification), `CourseJobCategories` (Course ↔ JobCategory) |
| Primary-Foreign Links | ClassSection, CourseFAQ, CourseRelatedLink, CourseRecomm, HotCourse all reference Course |
| Query Filters | keyword (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl), Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn range, ScheduleOff range, CanRepeat |
| Default Sort | `CourseId ASC` |

---

## Localization

### Chinese Table Name

- Course: 課程
- Description: 訓練課程主資料

### Chinese Column Names

- pkid: 主代碼
- Title: 課程名稱
- OfficialTitle: 官方課程名稱
- CourseId: 課程代碼
- ProdCourseId: 產品代碼
- FriendlyUrl: 友善網址
- DisplayOrder: 顯示順序
- Partner_pkid: 合作廠商
- CourseGroup_pkid: 課程群組
- PublishStatus_pkid: 發布狀態
- ScheduleOn: 上架日期
- ScheduleOff: 下架日期
- Hour: 時數
- ListPrice: 定價
- LearningCredit: 學習學分
- Material: 教材
- Objective: 課程目標
- Target: 適合對象
- Prerequisites: 先備知識
- Outline: 課程大綱
- TowardCertOrExam: 考試／認證說明
- Note: 備註
- OtherInfo: 其他資訊
- CanRepeat: 可重複上課

---

## Required Fields

All non-null fields (excluding IDENTITY PK) are required:

- `Title` NOT NULL
- `CourseId` NOT NULL
- `ProdCourseId` NOT NULL
- `FriendlyUrl` NOT NULL
- `DisplayOrder` NOT NULL
- `Partner_pkid` NOT NULL
- `PublishStatus_pkid` NOT NULL
- `ScheduleOn` NOT NULL
- `ScheduleOff` NOT NULL
- `Hour` NOT NULL
- `ListPrice` NOT NULL
- `LearningCredit` NOT NULL
- `CanRepeat` NOT NULL

Nullable (optional): `OfficialTitle`, `CourseGroup_pkid`, `Material`, `Objective`, `Target`, `Prerequisites`, `Outline`, `TowardCertOrExam`, `Note`, `OtherInfo`.

---

## Foreign Keys

List and View use the foreign key to get the corresponding row in the primary table for display label.
Edit and New use the foreign key to get the corresponding row in the primary table for select options.

- **Partner_pkid** → `Partner.pkid`
  - Alias as `PartnerPkid` in SELECT for the C# model property.
  - Option label = `Name`
  - Order by `DisplayOrder ASC`

- **CourseGroup_pkid** → `CourseGroup.pkid` (nullable — allow null / "無" option)
  - Option label = `Description`
  - Order by `pkid ASC`

- **PublishStatus_pkid** → `PublishStatus.pkid`
  - Option label = `Description`
  - Order by `pkid ASC`

---

## Foreign-Primary Links

In list, view, edit and insert pages:

- **Partner_pkid**: Partner table
  - Link to partner-detail page
  - Pass in `Partner_pkid`
  - Partner detail page shows the details

- **CourseGroup_pkid**: CourseGroup table (only when not null)
  - Link to course-group-detail page
  - Pass in `CourseGroup_pkid`

- **PublishStatus_pkid**: PublishStatus table
  - Link to publish-status-detail page
  - Pass in `PublishStatus_pkid`

---

## Primary-Foreign Links

The following tables reference `Course.pkid` (or `Course.CourseId`) as a foreign key. Show navigation links in list and detail views.

- **ClassSection** (`Course_pkid`)
  - Column header: 對應開課時間
  - Button label: 查看開課時間 (icon: `pi pi-calendar`)
  - Link to `/class-sections?coursePkid={pkid}`
  - ClassSection list sets `coursePkid` query filter with the passed value

- **CourseFAQ** (`Course_pkid`)
  - Column header: 對應課程問答
  - Button label: 查看 FAQ
  - Link to `/course-faqs?coursePkid={pkid}`

- **CourseRelatedLink** (`Course_pkid`)
  - Column header: 對應相關連結
  - Button label: 查看相關連結
  - Link to `/course-related-links?coursePkid={pkid}`

- **HotCourse** (`Course_pkid`)
  - Column header: 對應熱門課程
  - Button label: 查看熱門課程
  - Link to `/hot-courses?coursePkid={pkid}`

---

## N-N Relationships

### CourseInCertification — Course ↔ Certification

Junction table: `CourseInCertification` (`Course_pkid`, `Certification_pkid`)

- In the Course form (edit and new): show a multi-select or transfer-list for Certifications.
- Load options via `GET /api/lookups/certifications` (returns Certification list with Partner, ordered by Partner then Title).
- On save: diff the existing set vs. the submitted list; DELETE removed rows; INSERT added rows.
- In Course detail view: display the associated Certification names.
- `CertificationPkids` (`List<int>`) carried in `CourseRequest`.

### CourseJobCategories — Course ↔ JobCategory

Junction table: `CourseJobCategories` (`Course_pkid`, `JobCategory_pkid`)

- In the Course form (edit and new): show a multi-select for JobCategories.
- Load options via `GET /api/lookups/job-categories`.
- On save: diff existing vs. submitted; DELETE removed; INSERT added.
- In Course detail view: display associated JobCategory descriptions.
- `JobCategoryPkids` (`List<short>`) carried in `CourseRequest`.

---

## Query Filters

- **keyword**: string
  - LIKE on `Title`, `OfficialTitle`, `CourseId`, `ProdCourseId`, `FriendlyUrl`

- **Partner_pkid**: smallint?
  - Exact match `Partner_pkid` column
  - Look up `Partner` table via `GET /api/lookups/partners`
  - Option label = `Name`
  - Order by `DisplayOrder ASC`

- **CourseGroup_pkid**: smallint?
  - Exact match `CourseGroup_pkid` column
  - Look up `CourseGroup` table via `GET /api/lookups/course-groups`
  - Option label = `Description`
  - Order by `pkid ASC`

- **PublishStatus_pkid**: tinyint?
  - Exact match `PublishStatus_pkid` column
  - Look up `PublishStatus` table via `GET /api/lookups/publish-statuses`
  - Option label = `Description`
  - Order by `pkid ASC`

- **ScheduleOn**: date range
  - `ScheduleOn` BETWEEN `scheduleOnFrom` and `scheduleOnTo`

- **ScheduleOff**: date range
  - `ScheduleOff` BETWEEN `scheduleOffFrom` and `scheduleOffTo`

- **CanRepeat**: bool?
  - Exact match `CanRepeat` column

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | Exists | Partner list |
| `GET /api/lookups/course-groups` | Exists | CourseGroup list |
| `GET /api/lookups/publish-statuses` | Exists | PublishStatus list |
| `GET /api/lookups/certifications` | Exists | Certification list (with Partner) |
| `GET /api/lookups/job-categories` | Exists | JobCategory list |

---

## API Endpoints

Standard CRUD:

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/courses` | List all |
| `POST` | `/api/courses/query` | Filtered query (body: `CourseQuery`) |
| `GET` | `/api/courses/{id}` | Get by pkid (includes N-N lists) |
| `POST` | `/api/courses` | Create |
| `PUT` | `/api/courses` | Update (pkid from body) |
| `DELETE` | `/api/courses/{id}` | Delete |
| `POST` | `/api/courses/{id}/copy` | Copy course (see below) |

### Copy Action

`POST /api/courses/{id}/copy` — body: `{ newCourseId: string }`

- Returns `409 Conflict` if `newCourseId` already exists.
- Returns `404 Not Found` if source course not found.
- Copies all scalar fields and both N-N relations (Certifications, JobCategories).
- Returns `{ pkid }` of the newly created course.

---

## Backend Notes

### Models

```csharp
public class Course
{
    public int Pkid { get; set; }
    public string Title { get; set; }
    public string? OfficialTitle { get; set; }
    public string CourseId { get; set; }
    public string ProdCourseId { get; set; }
    public string FriendlyUrl { get; set; }
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
    // Populated on GET by pkid:
    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}

public class CourseRequest
{
    public int Pkid { get; set; }
    public string Title { get; set; }
    public string? OfficialTitle { get; set; }
    public string CourseId { get; set; }
    public string ProdCourseId { get; set; }
    public string FriendlyUrl { get; set; }
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
    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}

public class CourseQuery
{
    public string? Keyword { get; set; }
    public short? PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte? PublishStatusPkid { get; set; }
    public DateOnly? ScheduleOnFrom { get; set; }
    public DateOnly? ScheduleOnTo { get; set; }
    public DateOnly? ScheduleOffFrom { get; set; }
    public DateOnly? ScheduleOffTo { get; set; }
    public bool? CanRepeat { get; set; }
}
```

### SQL — SELECT columns

Alias the `_pkid` FK columns in SELECT:
```sql
SELECT c.pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl,
       c.DisplayOrder, c.Partner_pkid AS PartnerPkid, c.CourseGroup_pkid AS CourseGroupPkid,
       c.PublishStatus_pkid AS PublishStatusPkid, c.ScheduleOn, c.ScheduleOff,
       c.Hour, c.ListPrice, c.LearningCredit, c.Material, c.Objective, c.Target,
       c.Prerequisites, c.Outline, c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat
FROM Course c
```

### SQL — INSERT/UPDATE

Column lists use the exact DB column names; parameters use the C# property names:
```sql
-- INSERT
INSERT INTO Course (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
    Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff, Hour,
    ListPrice, LearningCredit, Material, Objective, Target, Prerequisites, Outline,
    TowardCertOrExam, Note, OtherInfo, CanRepeat)
VALUES (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
    @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid, @ScheduleOn, @ScheduleOff, @Hour,
    @ListPrice, @LearningCredit, @Material, @Objective, @Target, @Prerequisites, @Outline,
    @TowardCertOrExam, @Note, @OtherInfo, @CanRepeat);
SELECT CAST(SCOPE_IDENTITY() AS int);
```

### N-N Sync Pattern

For both `CourseInCertification` and `CourseJobCategories`, after INSERT/UPDATE:
1. `DELETE FROM CourseInCertification WHERE Course_pkid = @Pkid`
2. Bulk INSERT new rows from `request.CertificationPkids`
3. Same pattern for `CourseJobCategories` / `JobCategoryPkids`

### RowAudit

- INSERT: logs `CourseId` (first string-ish field — use `CourseId`)
- UPDATE: loads existing entity, logs changed property names via `AuditHelper.ChangedColumns`
- DELETE: loads existing entity, logs its `CourseId`

### DateOnly Type Handlers

`DateOnly` and `TimeOnly` handlers must be registered in `Program.cs`. See `spec/dapper.md`.

---

## Frontend Notes

### Form — ScheduleOff Auto-Default

When `ScheduleOn` changes (via `valueChanges`), automatically set `ScheduleOff` to `ScheduleOn + 10 years`:
- Use `addYears()` from `core/utils/date.util.ts`.
- Only fires when the new value is a `Date` instance.
- Use `{ emitEvent: false }` on the `setValue` to avoid loops.
- In edit mode, `patchValue` applies `ScheduleOff` after `ScheduleOn` so the loaded value wins (subscription fires on `ScheduleOn` patch, then `ScheduleOff` is overwritten by the patch value).

### Form — Sub-panels (edit mode only)

Shown after the main form fields, only when `isEdit`:

1. **課程相關連結** (`CourseRelatedLink`) — see `spec/course/CourseRelatedLink.md` for inline-edit cell pattern.
2. **推薦課程** (`CourseRecomm`) — see `spec/course/CourseRecomm.md` for inline-edit cell pattern.

Both sub-panels load after the main `forkJoin` resolves.

### List — Copy Button

Each row in the course list has a "複製" button (per-row action):
- Opens a dialog prompting for a new CourseId (displays source `courseId + title`).
- Calls `POST /api/courses/{id}/copy` with `{ newCourseId }`.
- On success: navigate to the new course detail page.

### List — 查看開課時間 Button

Each row has a calendar button (icon: `pi pi-calendar`, label: 查看開課時間):
- Navigates to `/class-sections?coursePkid={pkid}`.

### List — Lookup Binding

The list table displays partner name, course group description, and publish status description (not raw FK values). Load these via `forkJoin` of lookup endpoints on init.

### Detail — Inline QR Code

Auto-generated on course load inside the 基本資料 card. See `spec/course/Course.md` for canvas compositing details.

### Detail — 列印PDF

`window.print()` via a toolbar button. See `spec/course/Course.md` for print CSS details.

### date.util.ts

ALL `toIso()` helpers use local date components (`d.getFullYear()`, `d.getMonth()+1`, `d.getDate()`), never `d.toISOString().split('T')[0]`. See `core/utils/date.util.ts`.

### Delete Confirmation

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.courseId}」？
```

---

## Session Storage Keys

| Key | Contents |
|-----|----------|
| `course-list-filters` | Last query filter values |
| `course-list-sort` | `{ sortField, sortOrder }` |
| `course-list-page` | `{ first, rows }` |

Restore filters after lookups load. Incoming `partnerPkid` query param overrides saved state (cross-entity navigation from Partner list/detail).
