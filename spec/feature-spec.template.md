# Build Spec for {TABLE}
- database schema: `.\database\{SUB_SYSTEM}.sql`

---

## Summary

Fill in this table first — it forces upfront analysis before expanding each section.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` int IDENTITY (or describe if different) |
| Foreign Keys | list each FK column → referenced table |
| Required Fields | list all NOT NULL non-identity columns |
| N-N Relationships | list junction tables, or N/A |
| Primary-Foreign Links | list tables that reference {TABLE} as FK target, or N/A |
| Query Filters | keyword, FK dropdowns, date ranges, bool flags |
| Default Sort | ORDER BY clause for list and query |

---

## Localization

### Chinese Table Name

- {TABLE}: [Traditional Chinese name]
- Description: [one-line description of what this table stores]

### Chinese Column Names

For every column in {TABLE}, provide a Traditional Chinese label:

- pkid: 主代碼
- [each column]: [Chinese label]

---

## Required Fields

State which fields are required (NOT NULL, excluding IDENTITY PK and computed columns).
Separately list which fields are optional (nullable).

Required (NOT NULL):
- list each required column

Optional (nullable):
- list each nullable column

---

## Foreign Keys

For each FK column in {TABLE}:

- State the column name → referenced table and its PK.
- Note if nullable (allow null / "無" option in dropdowns).
- State the option label: which column(s) to display. If concatenating multiple columns, show the expression (e.g. `CourseId + ' ' + Title`).
- State the order-by column for the lookup dropdown.
- Call out any non-obvious label choice (e.g. why two columns are concatenated).
- Note any column name typos or aliases needed (e.g. `Partner_pkid` aliased as `PartnerPkid`).

If no foreign keys: **N/A**

---

## Foreign-Primary Links

These are navigation links shown in list, detail, edit, and new pages — they link FROM {TABLE} TO the referenced primary table's detail page.

For each FK column:
- FK column name → which primary table
- Link target: `/{primary-entity-route}/{fkValue}` (detail page)
- Only show link when value is not null (for nullable FKs)

If no foreign keys: **N/A**

---

## Primary-Foreign Links

These are navigation links shown in list and detail pages — they link FROM {TABLE} TO child entity list pages that reference {TABLE} as a foreign key target.

Scan the schema for every table that has a FK pointing to `{TABLE}.pkid` (or another unique key). For each one:

- Child table name
- Column header label (Traditional Chinese): 對應 [zhTW child entity]
- Button label and icon (e.g. `pi pi-list`)
- Link target route: `/{child-entity-route}?{fkParam}={pkid}`
- Query param name the child list accepts
- Note any special param name conventions (e.g. `coursePkid`, `trainingCenterPkid`)

If no tables reference {TABLE}: **N/A**

---

## N-N Relationships

A junction table has exactly two FK columns — one pointing to {TABLE}, one to the related entity (B). Scan the schema for all junction tables where one FK is `{TABLE}.pkid`.

For each N-N relationship:

- Junction table name and its two FK columns
- The related entity (B) and its lookup endpoint
- **List / Detail view**: how to display associated B items (show labels, not raw IDs)
- **Form (edit + new)**: UI control for selection (multi-select, checkbox list, etc.)
- Request field name and type (e.g. `CertificationPkids: List<int>`, `JobCategoryPkids: List<short>`)
- Sync pattern on save:
  1. `DELETE FROM [JunctionTable] WHERE {TABLE}_pkid = @Pkid`
  2. Bulk `INSERT` new rows from the request list

If no N-N relationships: **N/A**

---

## Query Filters

List every filter the `POST /query` endpoint accepts. Follow these rules:

- **keyword**: LIKE on short identifying string columns only (Name, Title, Code/Id fields).
  Do NOT include large text fields (nvarchar(max), nvarchar(4000)) — they are slow and rarely useful.
  State the exact columns included.

- **FK filter** (one per FK column):
  - Exact match on the FK column
  - Lookup endpoint that populates the dropdown
  - Option label and order-by

- **Bool filter** (one per bit column, if useful as a filter):
  - Exact match on the bit column
  - Tri-state: null = no filter, true = checked, false = unchecked

- **Date range filter** (one per date/datetime column, if useful as a filter):
  - From date (inclusive) and To date (inclusive)
  - State the column name and the two parameter names (e.g. `ScheduleOnFrom`, `ScheduleOnTo`)

---

## Lookup Endpoints Required

Table of all lookup endpoints this feature needs. Mark whether each already exists or is new.

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/...` | Exists / New | Description |

---

## API Endpoints

List all endpoints. Start with the standard six, then add any special routes.

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/{entities}` | List all |
| `POST` | `/api/{entities}/query` | Filtered query (body: `{TABLE}Query`) |
| `GET` | `/api/{entities}/{id}` | Get by pkid |
| `POST` | `/api/{entities}` | Create |
| `PUT` | `/api/{entities}` | Update (pkid from body) |
| `DELETE` | `/api/{entities}/{id}` | Delete |

For any special endpoints (copy, swap, etc.):
- Method + route
- Request body shape
- Response shape
- Error conditions (409, 404, etc.)

Auth exceptions: note if any endpoint requires `[Authorize(Roles="Admin")]` or `[AllowAnonymous]`.

---

## Backend Notes

### Models

Provide C# class skeletons for `{TABLE}`, `{TABLE}Request`, and `{TABLE}Query`.

Type mapping rules:
- `int IDENTITY` → `int` (PK, omit from Request)
- `nvarchar` / `varchar` NOT NULL → `string`
- `nvarchar` / `varchar` NULL → `string?`
- `smallint` → `short`; `tinyint` → `byte`; `bit` → `bool`
- `decimal` → `decimal`
- `date` → `DateOnly`; `time` → `TimeOnly`; `datetime` → `DateTime`
- Nullable FK → nullable primitive (e.g. `short?`)
- N-N pkid lists → `List<int>` / `List<short>` (in Request only, populated on GET by pkid)
- Computed columns (SQL `AS` expression) → include in model as read-only, exclude from Request

### SQL — SELECT

Write out the exact SELECT column list for `GetAllAsync` / `QueryAsync` / `GetByIdAsync`.
Call out any aliases required (e.g. `c.Partner_pkid AS PartnerPkid`).
For N-N or multi-map queries, describe the JOIN and `splitOn` parameter.

### SQL — INSERT

List every writable column for INSERT. Exclude:
- IDENTITY PK
- Computed columns (SQL `AS` expressions)
- Read-only snapshot columns (captured at creation only, not updated)

Show the `SELECT CAST(SCOPE_IDENTITY() AS int)` at the end.

### SQL — UPDATE

List every writable column for UPDATE. Same exclusions as INSERT, plus exclude immutable fields (e.g. `Created`, snapshot IDs).

### N-N Sync Pattern

After INSERT or UPDATE, for each N-N relationship:
```sql
DELETE FROM [JunctionTable] WHERE {TABLE}_pkid = @Pkid;
-- then for each id in the list:
INSERT INTO [JunctionTable] ({TABLE}_pkid, {B}_pkid) VALUES (@Pkid, @BPkid);
```

### Special Column Notes

Call out any of the following that apply:
- Column name typos preserved in DB — state exact SQL name and the C# alias
- `nchar(N)` columns requiring `RTRIM()` in SELECT (e.g. `Certification.Title`)
- Computed columns (SQL `AS` expressions) — read-only, excluded from INSERT/UPDATE
- `DateOnly` / `TimeOnly` columns — type handlers must be registered in `Program.cs` (see `spec/dapper.md`)
- Default value constraints that differ from C# defaults

---

## Frontend Notes

### Delete Confirmation Message

Standard format:
```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.<nameField>}」？
```

Exception for composite PKs (no pkid):
```
「${key1} → ${key2}」
```

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `{entity}-list-filters` | Last query filter values |
| `{entity}-list-sort` | `{ sortField, sortOrder }` |
| `{entity}-list-page` | `{ first, rows }` |

Note any incoming query params (from cross-entity navigation) that override saved filter state, and which param name the list accepts (e.g. `partnerPkid`, `coursePkid`).

### Lookup Binding in List

FK columns in the list table display labels, not raw IDs. Load all required lookups via `forkJoin` on component init. Restore saved filters after lookups load.

### Date Handling

- **`toIso()` serialization**: always use local date components (`d.getFullYear()`, `d.getMonth()+1`, `d.getDate()`). Never use `d.toISOString().split('T')[0]` — that converts to UTC first, causing off-by-one for UTC+8 users. Canonical helpers: `core/utils/date.util.ts`.
- **API datetime display (UTC fix)**: Dapper returns `datetime` with `Kind = Unspecified` (no timezone suffix). Append `'Z'` before parsing: `new Date(dt + 'Z')` in TS, or `{{ item.dateTime + 'Z' | date:'...' }}` in templates.

### Special Form Behaviors

Describe any non-obvious form behaviors:
- Auto-defaulting one field from another (e.g. ScheduleOff = ScheduleOn + 10 years via `valueChanges`)
- Conditional field visibility
- Fields that are read-only in edit mode but editable in new mode
- Any `{ emitEvent: false }` needed to prevent subscription loops

### Sub-panels (edit mode only)

List any child entity inline tables shown inside the form when `isEdit`:
- Sub-panel name (Traditional Chinese) and entity
- Load trigger (after main `forkJoin` resolves)
- Inline cell edit columns (see `spec/inline-edit.md`)
- Add-row controls and default value logic
- Delete behavior (confirm dialog, reload on success)

If none: **N/A**
