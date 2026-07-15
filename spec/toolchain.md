# Toolchain (pinned) — read before scaffolding or installing deps

Bare scaffolding / install commands pull the wrong major versions on this machine. Don't run them
without the pins below.

- **`global.json` pins the SDK to 9.0.314.** The machine default `dotnet` is **SDK 10**; without the
  pin, `dotnet new webapi` emits a *net10.0 Minimal API* project — wrong framework, wrong style.
  Do not delete `global.json`.
- **The global Angular CLI is v22.** Scaffold only via `npx @angular/cli@20.3.32`; a bare `ng new`
  or `ng generate` from the global CLI targets Angular 22.
- **PrimeNG 20 pairs with `@primeuix/themes@1.x`.** Installing `@primeuix/themes@latest` silently
  pulls the PrimeNG 21 theme line. PrimeNG 20 also has **no** `primeng/resources/**` CSS — theming is
  the styled-theme engine (`providePrimeNG({ theme: { preset: Aura } })` in `app.config.ts`) plus
  `primeicons`.
- **Node v26 is "Unsupported"** per the Angular CLI (it wants 20.19 / 22.12 / 24.x). Everything builds
  and tests green today; if the build starts misbehaving, Node 24 LTS is the fix.
