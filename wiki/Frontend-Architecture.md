# Frontend Architecture

Vue 3 SPA for the TsiBroker admin UI — manages `RailwayUndertaking` and `InfrastructureOperator` master data. Consumes only `TsiBroker.ApiService` (cookie-authenticated); it has no direct relationship to `TsiBroker.Ru.Api` or `TsiBroker.Im.Api`.

## Shared package: `@tsibroker/ui-kit`

The admin UI (`TsiBroker.Ui`), the IM mock UI (`TsiBroker.Im.Mock.UI`), and the RU mock UI (`TsiBroker.Ru.Mock.UI`) are separate Vite apps that share the same look and the same chrome (sidebar, topbar, shell), auth store, and `apiFetch` helper. Rather than keep copy-pasted files in sync by hand, all three apps are npm workspaces (root `package.json`) alongside a fourth workspace, `packages/tsibroker-ui-kit`, which holds:

- `styles/` — `tokens.less`, `base.less`, `buttons.less`, `forms.less`, `card.less`, `list.less`, `hints.less`, `topbar.less`, `sidebar.less` (the design system; each app's `main.less` imports these from `@tsibroker/ui-kit/styles/...` instead of a local copy)
- `components/` — `Shell.vue`, `Sidebar.vue`, `Topbar.vue` (generic chrome; nav items, brand text, and the account/footer area are passed in by each app via props/slots — see `AppShell.vue`/`MockShell.vue`)
- `composables/` — `useSidebar.ts`, `useTopbarOverride.ts`
- `lib/api.ts`, `stores/auth.ts` — `apiFetch`/`ApiError` and the Pinia auth store

Each app's own `src/lib/api.ts`, `src/stores/auth.ts`, and (for `TsiBroker.Ui`) `src/composables/useTopbarOverride.ts` are thin `export * from '@tsibroker/ui-kit/...'` re-export shims, so existing `@/lib/api` / `@/stores/auth` imports across views keep working unchanged.

The three apps stay **independently deployable** — the workspace only shares source at build time; nothing couples their runtime or deployment. `TsiBroker.Ui`'s Docker build context is the repo root (not just `src/TsiBroker.Ui/`) specifically so it can see the workspace root and the kit's source — see the comments in `src/TsiBroker.Ui/Dockerfile` and `infrastructure/docker-compose.yml`.

This is why a third frontend (`TsiBroker.Ru.Mock.UI`, the RU mock UI) could reuse the same chrome instead of copy-pasting again; each app still owns its own nav items, icons, and business views.

The kit has its own `eslint.config.ts`/`.oxlintrc.json`/`tsconfig.json` (mirroring the three apps') and `npm run lint` script, since each app's `oxlint .`/`eslint .` only covers its own directory. Run `npm run lint` at the repo root to lint all four workspaces at once.

## Tech Stack

| Technology | Purpose |
|---|---|
| Vue 3 (Composition API, `<script setup>`) | Framework |
| Pinia | Client-side state (currently just `auth`) |
| vue-router | Routing, with a global auth guard |
| vue-i18n | Localization (`de`/`en`) |
| Vite | Dev server / build |
| Less | Styling — **no Vuetify, no other component library** |
| vitest, vue-tsc, oxlint + eslint | Testing / type-checking / linting |

There is **no generated API client** (no Orval/OpenAPI codegen) and **no TanStack Query** — all server calls go through a single small `apiFetch` helper, and server data is fetched imperatively per view rather than cached in a query layer.

## Project Structure

```
src/TsiBroker.Ui/src/
├── router/
│   └── index.ts              # routes + auth guard
├── views/
│   ├── HomeView.vue
│   ├── LoginView.vue
│   ├── InfrastructureOperatorsView.vue
│   ├── InfrastructureOperatorCertificatesView.vue
│   ├── RailwayUndertakingsView.vue
│   └── RailwayUndertakingEditView.vue
├── components/
│   ├── layout/
│   │   ├── AppShell.vue      # configures @tsibroker/ui-kit's Shell (nav items, brand, footer slot)
│   │   └── AccountMenu.vue   # account/logout flyout, passed into Shell's sidebar-footer slot
│   ├── icons/                 # one tiny SFC per nav icon (IconHome.vue, IconTrain.vue, ...)
│   └── LanguageSwitcher.vue
├── stores/
│   └── auth.ts                 # re-exports @tsibroker/ui-kit/stores/auth
├── composables/
│   ├── useLocale.ts
│   └── useTopbarOverride.ts    # re-exports @tsibroker/ui-kit/composables/useTopbarOverride
├── lib/
│   ├── api.ts                  # re-exports @tsibroker/ui-kit/lib/api
│   └── cookies.ts
├── locales/
│   ├── de.json
│   └── en.json
└── assets/styles/
    └── modal.less              # app-specific; the rest come from @tsibroker/ui-kit/styles
```

Path alias `@` → `src/` (configured in `vite.config.ts`, used everywhere instead of relative imports). The shared design system and chrome live in `packages/tsibroker-ui-kit` (see above) rather than under `src/`.

## Routing

`src/router/index.ts` — route `meta` is extended with `public?: boolean` and `title?: string`.

| Path | Name | Notes |
|---|---|---|
| `/login` | `login` | `meta.public = true` |
| `/` | `home` | |
| `/infrastructure-operators` | `infrastructure-operators` | list view; each row links to its certificates page |
| `/infrastructure-operators/:id/certificates` | `infrastructure-operator-certificates` | certificate management (separate from the list view's edit modal — see below) |
| `/railway-undertakings` | `railway-undertakings` | list view |
| `/railway-undertakings/:id` | `railway-undertaking-edit` | detail/edit view |

`router.beforeEach` lets public routes through unconditionally; for everything else it ensures `useAuthStore().fetchMe()` has run at least once (tracked via `isChecked`), and redirects to `/login?redirect=<path>` if no `username` is set. `App.vue` wraps every non-public route in `<AppShell>` (sidebar + topbar); the login page renders bare.

## State: Pinia (`stores/auth.ts`)

The **only** store in the app:

```ts
export const useAuthStore = defineStore('auth', () => {
  const username = ref<string | null>(null)
  const isChecked = ref(false)

  async function fetchMe() { /* GET /api/auth/me, fails closed to null */ }
  async function login(username: string, password: string) { /* POST /api/auth/login */ }
  async function logout() { /* POST /api/auth/logout */ }

  return { username, isChecked, fetchMe, login, logout }
})
```

Everything else (form state, table data, dialog visibility) is local component `ref`/`reactive` state in the view that owns it — there's no global entity cache. If you're adding a new data-heavy view, follow the existing pattern (fetch on mount via `apiFetch`, hold results in local refs) rather than introducing a new global store.

## API Access: `lib/api.ts`

```ts
export class ApiError extends Error {
  constructor(public status: number) { super(`API error: ${status}`) }
}

export async function apiFetch(path: string, init: RequestInit = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...init.headers },
  })
  if (!response.ok) throw new ApiError(response.status)
  return response
}
```

`API_BASE_URL` comes from `VITE_API_BASE_URL` (wired by Aspire in local dev, see [[Architecture]]). `credentials: 'include'` is mandatory — the admin cookie (`TsiBroker.Auth`) only round-trips if every request sends it. This is the **only** sanctioned way to call the backend — don't bypass it with a manual `fetch`, and don't introduce a generated client.

Callers do their own `.json()` on the returned `Response` and their own error handling (typically catching `ApiError` and checking `.status`, e.g. `LoginView.vue` distinguishing `401` from other failures).

## Composables

- **`useLocale.ts`** — wraps `vue-i18n`'s `locale`; `setLocale()` updates it and persists to a cookie (`tsibroker_locale`, 1-year expiry)
- **`useSidebar.ts`** (`@tsibroker/ui-kit`) — module-level singleton `collapsed` ref backed by `localStorage`
- **`useTopbarOverride.ts`** (`@tsibroker/ui-kit`, re-exported from `@/composables/useTopbarOverride`) — a shared `hasTopbarOverride` ref that a view can set to suppress the kit's `Topbar`'s default title and `Teleport` its own custom title/actions in instead (used by `RailwayUndertakingEditView.vue` to render a breadcrumb + save/delete/lock buttons in the topbar)

## Internationalization

`locales/*.json` are auto-registered via `import.meta.glob('./locales/*.json', { eager: true })` — dropping in a new `<lang>.json` file requires no code change. Locale resolution order: cookie → `VITE_DEFAULT_LOCALE` env var → `en` fallback. All user-facing text must go through `$t()`/`t()` — add matching keys to **both** `de.json` and `en.json`, never hardcode UI strings (see [AGENTS.md](../AGENTS.md)).

Namespaces: `language`, `common` (generic labels/actions reused across views), `sidebar`, `routeTitles`, `home`, `login`, `infrastructureOperators`, `infrastructureOperatorCertificates`, `railwayUndertakings`, `railwayUndertakingEdit` (including a nested `linkedOperators`/`assignmentDialog` namespace).

## Styling

No component library — everything is hand-rolled Less against a shared set of building-block classes (`.btn`/`.btn--primary`, `.field`, `.modal`, `.data-table`, `.status`, `.card`, etc., defined in `@tsibroker/ui-kit/styles/*.less`, plus this app's own `assets/styles/modal.less`) plus view-level `<style scoped lang="less">` for layout.

`@tsibroker/ui-kit/styles/tokens.less` defines CSS custom properties in tiers: raw Vue-template palette → semantic tokens (`--color-background`, `--color-text`, `--color-danger`, ...) → brand tokens (`--color-primary` and derivatives via `color-mix()`) → app-shell tokens (sidebar/topbar sizing and gradient). Brand color is swappable per environment at runtime: `main.ts` reads `VITE_THEME_COLOR` and overwrites `--color-primary` plus re-colors the SVG favicon by string-replacing its default color and re-encoding as a data URI — no rebuild-per-environment needed beyond what the Docker build ARG bakes in.

Dark mode is only **partially** supported: `tokens.less` has a `@media (prefers-color-scheme: dark)` block remapping semantic tokens, but `AppShell.vue`'s content area and the sidebar/topbar tokens intentionally stay pinned to light-mode values (documented in code comments) — don't assume the whole app resolves correctly in dark mode without checking those overrides first.

## Layout Components

- **`Shell.vue`, `Sidebar.vue`, `Topbar.vue`** (`@tsibroker/ui-kit/components/`) — generic chrome shared with `TsiBroker.Im.Mock.UI` and `TsiBroker.Ru.Mock.UI`. `Sidebar` takes `navItems` (each `{ to, label, icon: Component }`) and a `#footer` scoped slot; `Topbar` shows `props.title` unless `hasTopbarOverride` is set, in which case it exposes `#topbar-custom-title` / `#topbar-actions` teleport targets.
- **`AppShell.vue`** — this app's wrapper around the kit's `Shell`: builds `navItems` from `IconHome`/`IconTrain`/`IconOperators`/`IconQueue` + i18n labels, and fills the `sidebar-footer` slot with `AccountMenu.vue`
- **`AccountMenu.vue`** — account/logout flyout (only rendered when `auth.username` is set), teleported to `body` to escape the sidebar's `overflow:hidden`
- **`LanguageSwitcher.vue`** — plain `<select>` bound to `useLocale()`

## Notable View: `RailwayUndertakingEditView.vue`

The most complex view in the app (~700 lines) — master data form, API-key management (masked/show-hide, "regenerate" staged locally until an explicit save), and a linked-operators table for managing `IsbAssignment`s via a nested dialog. Everything is staged in local refs and committed together via one explicit `saveAll()` (a `PUT` plus a conditional `PATCH .../status`) — there's no autosave. If you're adding a similarly data-heavy edit view, this is the reference implementation for the "local staging, explicit save" pattern used throughout the admin UI.

## Notable View: `InfrastructureOperatorCertificatesView.vue`

Manages one `InfrastructureOperator`'s `PartnerCertificateBundle` (see [[Data-Model]]#PartnerCertificateBundle) — three certificate upload slots (own client cert, expected server CA, expected client CA) plus an identity form (expected CNs, CRL URL). Unlike `RailwayUndertakingEditView.vue`, uploads are **not** staged — selecting a file immediately `POST`s it (the backend persists it to disk right away regardless), so there's nothing to "save" for that part; only the identity form fields are staged locally and committed via an explicit `PUT`.

This is the app's first file upload, so it introduces two things with no prior precedent:

- **File → base64**: `FileReader.readAsDataURL()`, then the `data:...;base64,` prefix is stripped before sending `{ certificateBase64 }` in a JSON body — no `multipart/form-data`, keeping the same `apiFetch`/JSON convention as every other endpoint.
- **Styled file input** (`.file-upload` / `.file-upload__input`, scoped in this view): a native `<input type="file">` renders as an unstyled OS widget, so it's visually hidden (clipped to 1px, not `display:none`, to stay screen-reader-accessible) inside a `<label class="btn file-upload">` — clicking/activating the label opens the native file picker, and the label itself is styled like any other `.btn`. Reuse this pattern rather than reinventing it for the next file upload.
