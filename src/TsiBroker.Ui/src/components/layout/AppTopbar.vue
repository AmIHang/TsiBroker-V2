<script setup lang="ts">
import { hasTopbarOverride } from '@/composables/useTopbarOverride'

defineProps<{
  title?: string
}>()
</script>

<template>
  <header class="topbar">
    <h1 v-show="!hasTopbarOverride" class="topbar__title">{{ title }}</h1>
    <div id="topbar-custom-title" class="topbar__custom-title"></div>
    <div id="topbar-actions" class="topbar__actions"></div>
  </header>
</template>

<style scoped lang="less">
.topbar {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  min-height: var(--topbar-height);
  padding: 0.5rem 1.5rem;
  background: var(--color-topbar-bg);
  background-attachment: fixed;
  border-bottom: 1px solid var(--color-topbar-border);

  // &__title comes from the shared styles/topbar.less (see comment there)

  &__custom-title:empty {
    display: none;
  }

  &__actions {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    flex-shrink: 0;
    margin-left: auto;
  }
}
</style>
