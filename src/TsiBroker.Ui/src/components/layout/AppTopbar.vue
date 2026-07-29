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

<style scoped>
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
}

.topbar__title {
  margin: 0;
  font-size: 1.15rem;
  font-weight: 600;
  color: var(--color-topbar-text);
}

.topbar__custom-title:empty {
  display: none;
}

.topbar__actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-shrink: 0;
  margin-left: auto;
}
</style>
