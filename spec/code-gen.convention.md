# Code Generation Patterns

## Backend

  ### Models
  - `{TABLE}.cs` — response model (with nav objects for FKs, subquery counts for n-n)
  - `{TABLE}Request.cs` — write DTO (FK pkids only; n-n as `List<int>`)
  - `{TABLE}Query.cs` — search DTO (Keyword?, FK pkids?, bool fields?, date ranges?)

  ### Repository
  - `I{TABLE}Repository.cs` + `{TABLE}Repository.cs`
  - Dapper only (no EF). Multi-map when JOINing nav objects.
  - `nchar` columns: always `RTRIM()` in SQL
  - n-n: delete-then-reinsert on update; separate query on same connection for read

  ### Controller
  - Route: `/api/{tablePlural}`; `PUT` takes pkid from body (no route param)
  - String PKs: route `{id}` (no `:int` constraint); service uses `encodeURIComponent`
  - `DateOnly`/`TimeOnly` fields: register Dapper type handlers (already in Program.cs)
  
 ## Frontend

  ### Files
  - `features/{table-plural}/{table}-list/`
  - `features/{table-plural}/{table}-detail/`
  - `features/{table-plural}/{table}-form/`

  ### List page
  - Session storage keys: `{table}-list-filters`, `{table}-list-sort`, `{table}-list-page`
  - Sortable/paginated `p-table`; filter drawer (`p-drawer`)
  - `p-select` in drawer: always `appendTo="body"`; mapped `{ pkid, label }[]` getter; `[filter]="true"` for 10+ options
  - `p-select` / `p-multiselect` with 100+ items: add `[virtualScroll]="true" [virtualScrollItemSize]="43"`

  ### Form page
  - Reactive Forms; `forkJoin` for parallel lookup calls on init
  - `p-datepicker`: convert ISO string ↔ `Date` on load/save
  - `p-datepicker [timeOnly]="true"` for `time` columns; `parseTime`/`toTimeStr` helpers
  - `p-multiselect` for n-n: `[maxSelectedLabels]="9999"`; wrap chips via `::ng-deep`

  ### Sidebar nav
  - Add entry under the appropriate nav group in `app.html` / `app.ts`
  
 
  ## Special Types

  | Column type | Handling |
  |-------------|---------|
  | `nchar(n)` | `RTRIM()` in all SQL SELECTs |
  | `time(7)` | C# `TimeOnly` via `TimeOnlyTypeHandler`; display with `\| slice:0:5`; `p-datepicker [timeOnly]` in form |
  | `date` | C# `DateOnly` via `DateOnlyTypeHandler`; `p-datepicker` in form |
  | `smallint` PK | No special handling |
  | `nvarchar` PK (string) | Controller route `{id}` (no `:int`); service calls `encodeURIComponent(id)` | 
  
  
   ## API Endpoints

  | Method | Route | Description |
  |--------|-------|-------------|
  | GET    | `/api/{plural}` | All records |
  | POST   | `/api/{plural}/query` | Filtered search |
  | GET    | `/api/{plural}/{id}` | Single record |
  | POST   | `/api/{plural}` | Create |
  | PUT    | `/api/{plural}` | Update (pkid in body) |
  | DELETE | `/api/{plural}/{id}` | Delete |
  | GET    | `/api/lookups/{plural}` | Slim lookup list (if used as FK target) |