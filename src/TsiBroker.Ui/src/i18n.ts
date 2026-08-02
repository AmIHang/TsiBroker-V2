import { createI18n } from 'vue-i18n'
import { getCookie } from '@/lib/cookies'

// Picks up every src/locales/*.json file automatically (resolved at build time
// by Vite) — adding a new language only requires dropping a new file there,
// no code change here.
const localeFiles = import.meta.glob('./locales/*.json', { eager: true, import: 'default' }) as Record<
  string,
  Record<string, unknown>
>

const messages = Object.fromEntries(
  Object.entries(localeFiles).map(([path, module]) => [path.match(/([\w-]+)\.json$/)![1], module]),
)

export const LOCALE_COOKIE = 'tsibroker_locale'
export const SUPPORTED_LOCALES = Object.keys(messages).sort()
export type Locale = string

function isSupportedLocale(value: string | null): value is Locale {
  return value !== null && SUPPORTED_LOCALES.includes(value)
}

function resolveInitialLocale(): Locale {
  const cookieLocale = getCookie(LOCALE_COOKIE)
  if (isSupportedLocale(cookieLocale)) {
    return cookieLocale
  }

  const envLocale = import.meta.env.VITE_DEFAULT_LOCALE ?? null
  if (isSupportedLocale(envLocale)) {
    return envLocale
  }

  return 'en'
}

const i18n = createI18n({
  legacy: false,
  locale: resolveInitialLocale(),
  fallbackLocale: 'en',
  messages,
})

export default i18n
