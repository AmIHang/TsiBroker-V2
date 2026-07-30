<script setup lang="ts">
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useSidebar } from '@/composables/useSidebar'

const { collapsed, toggle: toggleCollapsed } = useSidebar()
const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const navItems = [
  { to: '/', label: 'Startseite', icon: 'home' },
  { to: '/railway-undertakings', label: 'Eisenbahnverkehrsunternehmen', icon: 'train' },
  { to: '/infrastructure-operators', label: 'Infrastrukturbetreiber', icon: 'operators' },
] as const

function isNavItemActive(to: string) {
  return to === '/' ? route.path === '/' : route.path === to || route.path.startsWith(`${to}/`)
}

async function onLogout() {
  await auth.logout()
  await router.push('/login')
}
</script>

<template>
  <aside class="sidebar" :class="{ 'sidebar--collapsed': collapsed }">
    <div class="sidebar__header">
      <button
        type="button"
        class="icon-btn"
        :aria-label="collapsed ? 'Menü ausklappen' : 'Menü einklappen'"
        @click="toggleCollapsed"
      >
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M4 6h16M4 12h16M4 18h16" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
        </svg>
      </button>
      <span v-if="!collapsed" class="sidebar__brand">TSI-Broker</span>
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
          <svg v-if="item.icon === 'home'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <rect x="4" y="4" width="7" height="7" rx="1.5" stroke="currentColor" stroke-width="1.8" />
            <rect x="13" y="4" width="7" height="7" rx="1.5" stroke="currentColor" stroke-width="1.8" />
            <rect x="4" y="13" width="7" height="7" rx="1.5" stroke="currentColor" stroke-width="1.8" />
            <rect x="13" y="13" width="7" height="7" rx="1.5" stroke="currentColor" stroke-width="1.8" />
          </svg>
          <svg v-else-if="item.icon === 'operators'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <rect x="4" y="10" width="7" height="10" rx="1" stroke="currentColor" stroke-width="1.8" />
            <rect x="13" y="4" width="7" height="16" rx="1" stroke="currentColor" stroke-width="1.8" />
            <path d="M7 13.5h1M7 16.5h1M16 7.5h1M16 10.5h1M16 13.5h1" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
          </svg>
          <svg v-else-if="item.icon === 'train'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <rect x="6" y="3.5" width="12" height="12" rx="4" stroke="currentColor" stroke-width="1.8" />
            <path d="M6 10.5h12" stroke="currentColor" stroke-width="1.8" />
            <circle cx="9" cy="13" r="0.9" fill="currentColor" />
            <circle cx="15" cy="13" r="0.9" fill="currentColor" />
            <path d="M9 18.5l-2 2.2M15 18.5l2 2.2" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
          </svg>
        </span>
        <span v-if="!collapsed" class="nav-item__label">{{ item.label }}</span>
      </RouterLink>
    </nav>

    <div class="sidebar__footer">
      <button
        v-if="auth.username"
        type="button"
        class="nav-item nav-item--button"
        :title="collapsed ? `Logout (${auth.username})` : undefined"
        @click="onLogout"
      >
        <span class="nav-item__icon">
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path
              d="M9 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h3"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
            <path
              d="M15 16l4-4-4-4M19 12H9"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
        </span>
        <span v-if="!collapsed" class="nav-item__label">Logout ({{ auth.username }})</span>
      </button>
    </div>
  </aside>
</template>

<style scoped>
.sidebar {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  z-index: 20;
  display: flex;
  flex-direction: column;
  width: var(--sidebar-width-expanded);
  background: var(--color-sidebar-bg);
  background-attachment: fixed;
  border-right: 1px solid var(--color-sidebar-border);
  transition: width 0.18s ease;
  overflow: hidden;
}

.sidebar--collapsed {
  width: var(--sidebar-width-collapsed);
}

.sidebar__header {
  display: flex;
  align-items: center;
  gap: 0.85rem;
  padding: 0.75rem 0.5rem;
  flex-shrink: 0;
}

.sidebar__brand {
  color: var(--color-sidebar-text-strong);
  font-weight: 700;
  font-size: 0.95rem;
  white-space: nowrap;
}

.icon-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  padding: 0.6rem;
  border: none;
  border-radius: 10px;
  background: transparent;
  color: var(--color-sidebar-text);
  cursor: pointer;
  transition: background-color 0.15s, color 0.15s;
}

.icon-btn:hover {
  background: var(--color-sidebar-hover-bg);
  color: var(--color-sidebar-text-strong);
}

.icon-btn svg {
  width: 20px;
  height: 20px;
}

.sidebar--collapsed .icon-btn {
  width: 40px;
  height: 40px;
  padding: 0;
  margin: 0 auto;
}

.sidebar__nav {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.5rem;
  flex: 1;
  overflow-y: auto;
}

.sidebar__footer {
  padding: 0.5rem;
  border-top: 1px solid var(--color-sidebar-border);
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 0.85rem;
  width: 100%;
  padding: 0.6rem;
  border: none;
  border-radius: 10px;
  color: var(--color-sidebar-text);
  text-decoration: none;
  font-size: 0.9rem;
  background: transparent;
  cursor: pointer;
  transition: background-color 0.15s, color 0.15s;
}

.nav-item--button {
  font-family: inherit;
  text-align: left;
}

.nav-item:hover {
  background: var(--color-sidebar-hover-bg);
  color: var(--color-sidebar-text-strong);
}

.nav-item--active {
  background: var(--color-sidebar-active-bg);
  color: var(--color-sidebar-active-text);
}

.sidebar--collapsed .nav-item {
  width: 40px;
  height: 40px;
  padding: 0;
  justify-content: center;
  margin: 0 auto;
}

.nav-item__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 20px;
  height: 20px;
}

.nav-item__icon svg {
  width: 100%;
  height: 100%;
}

.nav-item__label {
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}
</style>
