# Build Spec for SkillTrain
- database schema: .\database\skill-train.sql

## Summary

`SkillTrain` is a top-level category entity grouping courses into training programme series (養成班). It has a short `Name` and an `AppKey` code. Each `SkillTrain` can be associated with many `Course` records via the junction table `SkillTrainCourse` (N-N, with `DisplayOrder`), and with many `LinkDefinition` records via the junction table `SkillTrainRelatedLink` (N-N, with `DisplayOrder`).

| Item | Detail |
|------|--------|
| Primary Key | `pkid` int IDENTITY |
| Foreign Keys | None |
| Required Fields | `Name`, `AppKey` |
| N-N Relationships | `SkillTrainCourse` — SkillTrain ↔ Course (with `DisplayOrder`); `SkillTrainRelatedLink` — SkillTrain ↔ LinkDefinition (with `DisplayOrder`) |
| Primary-Foreign Links | N/A — managed via N-N Relationships |
| Query Filters | keyword (Name, AppKey) |
| Default Sort | `Name ASC` |

---

## Localization

### Chinese Table Name

- SkillTrain: 養成班
- Description: 養成班課程系列

### Chinese Column Names

- pkid: 主代碼
- Name: 養成班名稱
- AppKey: 關鍵字

---

## Required Fields

All non-null fields are required:

- `Name` NOT NULL
- `AppKey` NOT NULL

---

## Foreign Keys

`SkillTrain` has no foreign key columns.

N/A

---

## Foreign-Primary Links

`SkillTrain` has no foreign key columns.

N/A

---

## Primary-Foreign Links

Both `SkillTrainCourse` and `SkillTrainRelatedLink` are managed inline via the N-N Relationships section. No separate list-page links are needed.

N/A

---

## N-N Relationships

### SkillTrain ↔ Course via `SkillTrainCourse`

| Column | Type | Notes |
|--------|------|-------|
| SkillTrain_pkid | int NOT NULL | FK → SkillTrain.pkid (composite PK) |
| Course_pkid | int NOT NULL | FK → Course.pkid (composite PK) |
| DisplayOrder | int NOT NULL | Sort order of this course within the skill train |

- **View page**: show associated courses as a read-only list (`CourseId + ' ' + Title`), sorted by `DisplayOrder ASC`.
- **Edit / New page**: use a `p-multiselect` to pick courses (option label = `CourseId + ' ' + Title`, ordered by `CourseId`). For each selected course, allow editing `DisplayOrder`.
- **Write pattern** (Create & Update):
  - `DELETE FROM SkillTrainCourse WHERE SkillTrain_pkid = @pkid`
  - Re-INSERT all rows from the request list (each item: `Course_pkid` + `DisplayOrder`).
- **Request DTO**: `SkillTrainRequest.Courses` — `List<SkillTrainCourseItem>` where `SkillTrainCourseItem` has `CoursePkid` (int) and `DisplayOrder` (int).

---

### SkillTrain ↔ LinkDefinition via `SkillTrainRelatedLink`

| Column | Type | Notes |
|--------|------|-------|
| pkid | int IDENTITY | PK |
| SkillTrain_pkid | int NOT NULL | FK → SkillTrain.pkid |
| LinkDefinition_pkid | int NOT NULL | FK → LinkDefinition.pkid |
| DisplayOrder | int NOT NULL | Sort order of this link within the skill train |

- **View page**: show associated links as a read-only list (`LinkDefinition.Name`), sorted by `DisplayOrder ASC`.
- **Edit / New page**: use a `p-multiselect` to pick link definitions (option label = `Name`, ordered by `Name`). For each selected link, allow editing `DisplayOrder`.
- **Write pattern** (Create & Update):
  - `DELETE FROM SkillTrainRelatedLink WHERE SkillTrain_pkid = @pkid`
  - Re-INSERT all rows from the request list (each item: `LinkDefinition_pkid` + `DisplayOrder`).
- **Request DTO**: `SkillTrainRequest.RelatedLinks` — `List<SkillTrainRelatedLinkItem>` where `SkillTrainRelatedLinkItem` has `LinkDefinitionPkid` (int) and `DisplayOrder` (int).

---

## Query Filters

- **keyword**: string
  - LIKE on `Name`, `AppKey`

---
