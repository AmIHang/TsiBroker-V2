import { useI18n } from 'vue-i18n'
import { setCookie } from '@/lib/cookies'
import { LOCALE_COOKIE, SUPPORTED_LOCALES, type Locale } from '@/i18n'

export function useLocale() {
  const { locale } = useI18n()

  function setLocale(next: Locale) {
    locale.value = next
    setCookie(LOCALE_COOKIE, next, 365)
  }

  return { locale, setLocale, supportedLocales: SUPPORTED_LOCALES }
}
