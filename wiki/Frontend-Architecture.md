# Frontend Architecture

Vue 3 SPA for the TsiBroker admin UI — manages `RailwayUndertaking` and `InfrastructureOperator` master data. Consumes only `TsiBroker.ApiService` (cookie-authenticated); it has no direct relationship to `TsiBroker.Ru.Api` or `TsiBroker.Im.Api`.

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
│   ├── RailwayUndertakingsView.vue
│   └── RailwayUndertakingEditView.vue
├── components/layout/
│   ├── AppShell.vue          # sidebar + topbar + content slot
│   ├── AppSidebar.vue
│   ├── AppTopbar.vue
│   └── LanguageSwitcher.vue
├── stores/
│   └── auth.ts                # the only Pinia store
├── composables/
│   ├── useLocale.ts
│   ├── useSidebar.ts
│   └── useTopbarOverride.ts
├── lib/
│   ├── api.ts                 # apiFetch, ApiError
│   └── cookies.ts
├── locales/
│   ├── de.json
│   └── en.json
└── assets/styles/
    ├── tokens.less             # design tokens (CSS custom properties)
    ├── base.less, buttons.less, card.less, forms.less,
    ├── hints.less, list.less, modal.less, topbar.less
```

Path alias `@` → `src/` (configured in `vite.config.ts`, used everywhere instead of relative imports).

## Routing

`src/router/index.ts` — route `meta` is extended with `public?: boolean` and `title?: string`.

| Path | Name | Notes |
|---|---|---|
| `/login` | `login` | `meta.public = true` |
| `/` | `home` | |
| `/infrastructure-operators` | `infrastructure-operators` | |
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
- **`useSidebar.ts`** — module-level singleton `collapsed` ref backed by `localStorage`
- **`useTopbarOverride.ts`** — a shared `hasTopbarOverride` ref that a view can set to suppress `AppTopbar`'s default title and `Teleport` its own custom title/actions in instead (used by `RailwayUndertakingEditView.vue` to render a breadcrumb + save/delete/lock buttons in the topbar)

## Internationalization

`locales/*.json` are auto-registered via `import.meta.glob('./locales/*.json', { eager: true })` — dropping in a new `<lang>.json` file requires no code change. Locale resolution order: cookie → `VITE_DEFAULT_LOCALE` env var → `en` fallback. All user-facing text must go through `$t()`/`t()` — add matching keys to **both** `de.json` and `en.json`, never hardcode UI strings (see [AGENTS.md](../AGENTS.md)).

Namespaces: `language`, `common` (generic labels/actions reused across views), `sidebar`, `routeTitles`, `home`, `login`, `infrastructureOperators`, `railwayUndertakings`, `railwayUndertakingEdit` (including a nested `linkedOperators`/`assignmentDialog` namespace).

## Styling

No component library — everything is hand-rolled Less against a shared set of building-block classes (`.btn`/`.btn--primary`, `.field`, `.modal`, `.data-table`, `.status`, `.card`, etc., defined in `assets/styles/*.less`) plus view-level `<style scoped lang="less">` for layout.

`assets/styles/tokens.less` defines CSS custom properties in tiers: raw Vue-template palette → semantic tokens (`--color-background`, `--color-text`, `--color-danger`, ...) → brand tokens (`--color-primary` and derivatives via `color-mix()`) → app-shell tokens (sidebar/topbar sizing and gradient). Brand color is swappable per environment at runtime: `main.ts` reads `VITE_THEME_COLOR` and overwrites `--color-primary` plus re-colors the SVG favicon by string-replacing its default color and re-encoding as a data URI — no rebuild-per-environment needed beyond what the Docker build ARG bakes in.

Dark mode is only **partially** supported: `tokens.less` has a `@media (prefers-color-scheme: dark)` block remapping semantic tokens, but `AppShell.vue`'s content area and the sidebar/topbar tokens intentionally stay pinned to light-mode values (documented in code comments) — don't assume the whole app resolves correctly in dark mode without checking those overrides first.

## Layout Components

- **`AppShell.vue`** — sidebar + main column (topbar + `<slot/>`), margin-left driven by `useSidebar().collapsed`
- **`AppSidebar.vue`** — fixed, collapsible nav; account menu (only when `auth.username` is set) rendered via `Teleport to="body"` to escape the sidebar's `overflow:hidden`
- **`AppTopbar.vue`** — sticky header; shows `props.title` unless `hasTopbarOverride` is set, in which case it exposes `#topbar-custom-title` / `#topbar-actions` teleport targets
- **`LanguageSwitcher.vue`** — plain `<select>` bound to `useLocale()`

## Notable View: `RailwayUndertakingEditView.vue`

The most complex view in the app (~700 lines) — master data form, API-key management (masked/show-hide, "regenerate" staged locally until an explicit save), and a linked-operators table for managing `IsbAssignment`s via a nested dialog. Everything is staged in local refs and committed together via one explicit `saveAll()` (a `PUT` plus a conditional `PATCH .../status`) — there's no autosave. If you're adding a similarly data-heavy edit view, this is the reference implementation for the "local staging, explicit save" pattern used throughout the admin UI.
