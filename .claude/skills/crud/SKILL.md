---
name: crud
description: Generate a feature spec from a DB table schema then scaffold the full CRUD feature (backend models/repo/controller + frontend list/detail/form/route/sidebar). Use when asked to "build CRUD for X", "scaffold X", "add X entity", or "create feature for X table".
---

# /crud — Generate spec then build CRUD for an entity

Automates the two-step pattern: generate a feature spec from the DB schema, confirm it,
then build all backend and frontend CRUD files.

## Arguments

Parse from the raw args string:
- `TABLE=<PascalCase>` — C# model name (e.g. `PageDescription`)
- `SUB_SYSTEM=<name>` — database sub-system folder (e.g. `admin`, `home`, `course`)
- `MENU_GROUP="<label>"` — sidebar nav group label (e.g. `"Administration"`)
- `API_PROJECT=<name>` — API project folder name (e.g. `MyApp.API`); auto-detected if omitted
- `NG_PROJECT=<name>` — Angular project folder name (e.g. `MyApp.NG`); auto-detected if omitted
- Any remaining tokens are treated as column display-name hints, one per token,
  in the form `"columnName: <display-label>"` (e.g. `"paper_id: Issue Number"`)

If TABLE or SUB_SYSTEM is missing, stop and ask the user to supply them.

**Auto-detecting API_PROJECT and NG_PROJECT:**
If either is not supplied, scan the workspace root:
- `API_PROJECT`: find the directory containing a `*.sln` file; use that directory name.
- `NG_PROJECT`: find the directory containing an `angular.json` file; use that directory name.

If detection finds exactly one match, proceed and inform the user:
> "Detected API_PROJECT=`{name}` and NG_PROJECT=`{name}` — override with explicit args if wrong."

If detection is ambiguous (multiple matches), stop and ask the user to supply the missing argument.

---

## Step 1 — Generate the feature spec

1. Read `spec/feature-spec.template.md` to understand the spec structure.
2. Read `database/{SUB_SYSTEM}.sql` and locate the table whose name matches TABLE
   (case-insensitive, also match the plural form). Extract: all columns, data types,
   nullability, PKs, FKs, UNIQUE constraints, and any computed columns.
3. Read two reference specs as formatting examples:
   - `spec/sample1.spec.md`
   - `spec/sample2.spec.md`
4. Using the template structure and the reference examples as a style guide, produce
   a complete feature spec in Markdown. The spec must include all sections from the
   template that are non-empty for this table:
   - Display names for the table and each column (use any supplied hints; infer the rest)
   - Required vs optional fields (based on NOT NULL / nullable)
   - Foreign keys: label column, order column, lookup source
   - Foreign-Primary links (outbound navigation)
   - Primary-Foreign links (inbound navigation — check other tables that FK to TABLE)
   - N-N relationships (junction tables whose name contains TABLE's name or its PK)
   - Query filters: keyword LIKE on all string columns; FK dropdowns; bool toggles;
     date-range pairs for any date/datetime columns
   - Default sort order (prefer DisplayOrder ASC, then pkid DESC, then date DESC)
   - API endpoints section listing all standard routes + any notable exceptions
   - Backend models: `{TABLE}.cs`, `{TABLE}Request.cs`, `{TABLE}Query.cs` with all
     field declarations and data annotations
   - Dapper SQL patterns: SELECT (with JOINs for FKs), INSERT, UPDATE, DELETE
   - Frontend route table and Angular model interface
   - List component: columns, filter drawer layout, sort defaults
   - Form layout: one p-control per field with appropriate PrimeNG widget
   - Sidebar placement: which nav group, new group or existing
   - Tests: backend xUnit controller/repository tests and frontend Angular
     service + component `.spec.ts` files to generate
   - Files to create/modify summary table (backend + frontend + tests)
5. Save the spec to `spec/{SUB_SYSTEM}/{TABLE}.md`.
6. Show the full spec to the user and ask:

> Spec saved to `spec/{SUB_SYSTEM}/{TABLE}.md`. Review it above.
> Proceed to build CRUD, or do you want to edit the spec first?

Options:
- A) Build CRUD now (recommended)
- B) I'll edit the spec — re-invoke /crud after editing

If B: stop here. The user will edit the spec and re-run `/crud` (skipping
spec generation if the file already exists — see Step 1a below).

**Step 1a — Skip spec generation if already done.**
If `spec/{SUB_SYSTEM}/{TABLE}.md` already exists when /crud is invoked,
skip steps 1–5 and go directly to step 2, reading the existing spec.
Tell the user: "Found existing spec at `spec/{SUB_SYSTEM}/{TABLE}.md` — skipping
spec generation. Reading it now."

---

## Step 2 — Build CRUD

Read `spec/{SUB_SYSTEM}/{TABLE}.md` (just generated or pre-existing).
Read `spec/code-gen.convention.md` for scaffolding conventions.
Follow CLAUDE.md for all non-obvious rules (string PKs, DateOnly handlers,
RowAudit logging, sticky toolbar pattern, session storage keys, etc.).

Build in this order:

### Backend ({API_PROJECT})

1. **Models** — `{API_PROJECT}/Models/{TABLE}.cs`,
   `{TABLE}Request.cs`, `{TABLE}Query.cs`
2. **Repository interface** — `{API_PROJECT}/Repositories/I{TABLE}Repository.cs`
3. **Repository implementation** — `{API_PROJECT}/Repositories/{TABLE}Repository.cs`
   - Dapper only. Multi-map for FK nav objects.
   - `nchar` columns: `RTRIM()` in all SELECTs.
   - n-n: delete-then-reinsert on update; separate query on same connection for read.
   - Inject `RowAuditWriter`; log on INSERT / UPDATE / DELETE.
4. **Controller** — `{API_PROJECT}/Controllers/{TABLE}sController.cs`
   (pluralize correctly — check existing controllers for the pattern)
5. **Register** — add `I{TABLE}Repository` / `{TABLE}Repository` to `Program.cs`
   DI registrations.

### Frontend ({NG_PROJECT})

6. **Model** — `{NG_PROJECT}/src/app/core/models/{table-kebab}.model.ts`
7. **Service** — `{NG_PROJECT}/src/app/core/services/{table-kebab}.service.ts`
   (string PK: `encodeURIComponent` in `getById`/`delete`)
8. **List component** — `{NG_PROJECT}/src/app/features/{table-plural}/{table-kebab}-list/`
   - Session storage: `{table}-list-filters`, `{table}-list-sort`, `{table}-list-page`
   - Filter drawer with all query fields from spec
   - `confirmDelete` message includes the record's PK and display name
9. **Detail component** — `{NG_PROJECT}/src/app/features/{table-plural}/{table-kebab}-detail/`
   - `RowAuditBadgeComponent` in toolbar `#start`
   - Primary-Foreign link buttons if spec has them
10. **Form component** — `{NG_PROJECT}/src/app/features/{table-plural}/{table-kebab}-form/`
    - Reactive Forms; `forkJoin` for parallel lookups; sticky `p-toolbar`
    - `RowAuditBadgeComponent` in toolbar `#start`
11. **Lazy route** — add to `{NG_PROJECT}/src/app/app.routes.ts`
    (route order: `/new` before `/:id`)
12. **Sidebar entry** — add to `{NG_PROJECT}/src/app/app.html` and `app.ts` under
    MENU_GROUP (create new group if it doesn't exist)
13. **Lookup endpoint** (if this table is used as an FK target elsewhere) —
    add to `{API_PROJECT}/{API_PROJECT}/Controllers/LookupsController.cs` and
    `{NG_PROJECT}/src/app/core/services/lookup.service.ts`

### Tests (both sides — always generated)

14. **Backend tests** — add xUnit tests to the API test project
    (`{API_PROJECT}.Tests/`; create it if missing and reference the API project):
    - Controller tests for `{TABLE}sController` covering each endpoint:
      list with a query filter, get-by-id (found + not-found), create, update, delete.
    - Mock `I{TABLE}Repository` (e.g. Moq) and assert the controller returns the
      correct result types and status codes (200/201/204/404).
    - Validate required-field rules from the spec produce the expected 400s.
15. **Frontend tests** — add Angular unit tests (Karma + Jasmine, the `.spec.ts`
    files generated alongside each unit):
    - `{table-kebab}.service.spec.ts` — use `HttpClientTestingModule` /
      `HttpTestingController` to assert each method hits the right URL/verb and
      `encodeURIComponent`s string PKs in `getById`/`delete`.
    - `*-list`, `*-detail`, `*-form` component specs — mount each component with a
      mocked service, assert it renders and that the form enforces required fields.

### Finish

After all files are written, report:
- List of every file created or modified
- Any non-obvious decisions made (e.g. inferred display name, chosen sort order)
- Next step: `dotnet run` to verify the API compiles; `ng serve` to verify the UI
- Run the tests: `dotnet test` (backend) and `ng test --watch=false` (frontend)
