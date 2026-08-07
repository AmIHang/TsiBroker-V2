<script setup lang="ts">
import type { Component } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useSidebar } from '../composables/useSidebar'

export interface SidebarNavItem {
  to: string
  label: string
  icon: Component
}

defineProps<{
  brand: string
  navItems: readonly SidebarNavItem[]
  expandLabel: string
  collapseLabel: string
}>()

const { collapsed, toggle: toggleCollapsed } = useSidebar()
const route = useRoute()

function isNavItemActive(to: string) {
  return to === '/' ? route.path === '/' : route.path === to || route.path.startsWith(`${to}/`)
}
</script>

<template>
  <aside class="sidebar" :class="{ 'sidebar--collapsed': collapsed }">
    <div class="sidebar__header">
      <button
        type="button"
        class="icon-btn"
        :aria-label="collapsed ? expandLabel : collapseLabel"
        @click="toggleCollapsed"
      >
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M4 6h16M4 12h16M4 18h16" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
        </svg>
      </button>
      <span v-if="!collapsed" class="sidebar__brand">{{ brand }}</span>
    </div>

    <nav class="sidebar__nav">
      <RouterLink
        v-for="item in navItems"
        :key="item.to"
        :to="item.to"
        class="nav-item"
        :class="{ 'nav-item--active': isNavItemActive(item.to) }"
        :title="collapsed ? item.label : undefined"
      >
        <span class="nav-item__icon">
          <component :is="item.icon" />
        </span>
        <span v-if="!collapsed" class="nav-item__label">{{ item.label }}</span>
      </RouterLink>
    </nav>

    <!-- App-specific account/logout markup (styled via the global .sidebar__footer /
         .nav-item / .account-flyout classes in styles/sidebar.less, since scoped CSS
         here wouldn't apply to slot content authored by the consumer). -->
    <slot name="footer" :collapsed="collapsed" />
  </aside>
</template>
