# Testing conventions

Read this before writing or running tests.

## Authorization cannot be tested from a controller unit test

`[Authorize]` is an MVC **pipeline filter**. A test that news up the controller directly
(`new AppUsersController(repo)`) never runs the pipeline, so it passes whether the attribute is
present, absent or misspelled. Authorization is structurally invisible to it.

This is not theoretical: a privilege escalation survived 26 commits behind a test that could not fail.
Verified by deleting the gate — every controller unit test stayed green; only the
`WebApplicationFactory` tests went red.

- Any change to an auth gate MUST be covered through `WebApplicationFactory`
  (`CMS.API.Tests/Infrastructure/AdminAuthTestFactory.cs`), which boots the real pipeline.
- `Controllers/AuthorizationConventionTests.cs` reflects over the **whole** controller surface and
  asserts the expected gate per endpoint, so a new endpoint is covered the moment it compiles. If it
  fails, add the attribute — do not add an exemption without a reason you would defend in a review.
- The Angular sidebar's Admin check is **cosmetic**; routes are reachable by typing the URL. The
  server is the only authority.

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
