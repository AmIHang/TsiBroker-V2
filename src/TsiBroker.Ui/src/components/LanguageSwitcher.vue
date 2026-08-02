<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useLocale } from '@/composables/useLocale'
import type { Locale } from '@/i18n'

const { t } = useI18n()
const { locale, setLocale, supportedLocales } = useLocale()

function onChange(event: Event) {
  setLocale((event.target as HTMLSelectElement).value as Locale)
}
</script>

<template>
  <select class="language-switcher" :aria-label="t('language.label')" :value="locale" @change="onChange">
    <option v-for="code in supportedLocales" :key="code" :value="code">{{ t(`language.${code}`) }}</option>
  </select>
</template>

<style scoped lang="less">
.language-switcher {
  padding: 0.4rem 0.6rem;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: transparent;
  color: inherit;
  font-size: 0.85rem;
  cursor: pointer;
}
</style>
