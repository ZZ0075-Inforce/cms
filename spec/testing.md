# Testing conventions

Read this before writing or running tests.

## xUnit is v2 here

There is no `Assert.Skip` / `SkipUnless` (that is v3). Use the `[IntegrationFact]` /
`[IntegrationTheory]` attributes in `CMS.API.Tests/Infrastructure/`, which set `Skip` at discovery
time from a probed `DatabaseProbe`. Run DB-free with `dotnet test --filter "Category!=Integration"`.

## Integration tests hit the real database

Integration tests run real SQL against the real `CMS` database — mocking Dapper would not catch a
wrong join column or a broken `ESCAPE` clause. They are `[Trait("Category","Integration")]`, seed their
own `TEST_`-prefixed rows, and the fixture (`DatabaseFixture.CleanupAsync`) sweeps them at both start
and end so a killed run self-heals.

- **Each new table adds its own sweep line**, keyed on whichever column has room for the `TEST_`
  prefix: `AppRole.RoleId`, `AppUser.UserId`, `Partner.Name` (AppKey is `varchar(10)`, too short),
  `CourseGroup.Description`, `Course.CourseId`, `Certification.Title`.
- **The sweep is FK-ordered** — a child goes before the parent it references (Course before
  Partner/CourseGroup; Certification before Partner; AppUserRole before AppUser/AppRole).
- A test needing a valid FK either seeds its own `TEST_` parent (Course seeds a Partner +
  Certification + JobCategory; AppUser seeds `TEST_` AppRoles for the n-n) or borrows one existing pkid
  from a fixed enum table read-only (`DatabaseFixture.AnyPublishStatusPkidAsync` — PublishStatus rows
  are never TEST-owned or mutated).
- **AppUser's create reads `SysConfig 'appConfig'`.** Its integration test seeds a throwaway
  `appConfig` row **only if absent** and removes it afterward; it never overwrites a pre-existing one.

Because `CMS` is the *real* database: **never mutate or delete the seeded `Admin` / `User` rows, and
assert containment — never an exact row count.** Isolation is deliberately *not* `TransactionScope`:
the repository owns its own `SqlTransaction` for n-n atomicity, and an ambient transaction around it
either throws or escalates to MSDTC.
