<script setup lang="ts">
import { useSidebar } from '../composables/useSidebar'
import Sidebar, { type SidebarNavItem } from './Sidebar.vue'
import Topbar from './Topbar.vue'

defineProps<{
  title?: string
  brand: string
  navItems: readonly SidebarNavItem[]
  expandLabel: string
  collapseLabel: string
}>()

const { collapsed } = useSidebar()
</script>

<template>
  <div class="app-shell" :class="{ 'app-shell--collapsed': collapsed }">
    <Sidebar :brand="brand" :nav-items="navItems" :expand-label="expandLabel" :collapse-label="collapseLabel">
      <template #footer="slotProps">
        <slot name="sidebar-footer" v-bind="slotProps" />
      </template>
    </Sidebar>

    <div class="app-shell__main">
      <Topbar :title="title" />
      <main class="app-shell__content">
        <slot />
      </main>
    </div>
  </div>
</template>

<style scoped lang="less">
.app-shell {
  min-height: 100vh;

  &__main {
    display: flex;
    flex-direction: column;
    min-height: 100vh;
    margin-left: var(--sidebar-width-expanded);
    transition: margin-left 0.18s ease;
  }

  &--collapsed &__main {
    margin-left: var(--sidebar-width-collapsed);
  }

  &__content {
    flex: 1;
    padding: 1.75rem;
    background: var(--color-content-bg);
    // Content area always uses the light-mode palette, matching the sidebar/topbar
    // which also ignore dark mode, so it stays readable on the fixed light background.
    --color-background: var(--vt-c-white);
    --color-background-soft: var(--vt-c-white-soft);
    --color-background-mute: var(--vt-c-white-mute);
    --color-border: var(--vt-c-divider-light-2);
    --color-border-hover: var(--vt-c-divider-light-1);
    --color-heading: var(--vt-c-text-light-1);
    --color-text: var(--vt-c-text-light-1);
    color: var(--color-text);
  }
}
</style>
