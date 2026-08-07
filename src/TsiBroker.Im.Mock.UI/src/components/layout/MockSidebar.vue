<script setup lang="ts">
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useSidebar } from '@/composables/useSidebar'

const { collapsed, toggle: toggleCollapsed } = useSidebar()
const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const navItems = [
  { to: '/', label: 'Send message', icon: 'send' },
  { to: '/received', label: 'Received messages', icon: 'inbox' },
  { to: '/settings', label: 'Response settings', icon: 'settings' },
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
        :aria-label="collapsed ? 'Expand menu' : 'Collapse menu'"
        @click="toggleCollapsed"
      >
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M4 6h16M4 12h16M4 18h16" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
        </svg>
      </button>
      <span v-if="!collapsed" class="sidebar__brand">TSI-Broker | Infra </span>
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
          <svg v-if="item.icon === 'send'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path d="M3 12L21 3L14 21L11 13L3 12Z" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round" />
            <path d="M11 13L21 3" stroke="currentColor" stroke-width="1.8" />
          </svg>
          <svg v-else-if="item.icon === 'inbox'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path
              d="M4 13V6a1 1 0 0 1 1-1h14a1 1 0 0 1 1 1v7"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
            <path
              d="M4 13h4.5a1 1 0 0 1 .9.55l.7 1.4a1 1 0 0 0 .9.55h2a1 1 0 0 0 .9-.55l.7-1.4a1 1 0 0 1 .9-.55H20"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
            <path
              d="M4 13v5a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-5"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
          <svg v-else-if="item.icon === 'settings'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="1.8" />
            <path
              d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09a1.65 1.65 0 0 0-1-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09a1.65 1.65 0 0 0 1.51-1 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"
              stroke="currentColor"
              stroke-width="1.6"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
        </span>
        <span v-if="!collapsed" class="nav-item__label">{{ item.label }}</span>
      </RouterLink>
    </nav>

    <div v-if="auth.username" class="sidebar__footer">
      <button
        type="button"
        class="nav-item nav-item--button"
        :title="collapsed ? `Log out (${auth.username})` : undefined"
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
        <span v-if="!collapsed" class="nav-item__label">Log out ({{ auth.username }})</span>
      </button>
    </div>
  </aside>
</template>

<style scoped lang="less">
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

  &--collapsed {
    width: var(--sidebar-width-collapsed);

    .icon-btn {
      width: 40px;
      height: 40px;
      padding: 0;
      margin: 0 auto;
    }

    .nav-item {
      width: 40px;
      height: 40px;
      padding: 0;
      justify-content: center;
      margin: 0 auto;
    }
  }

  &__header {
    display: flex;
    align-items: center;
    gap: 0.85rem;
    padding: 0.75rem 0.5rem;
    flex-shrink: 0;
  }

  &__brand {
    color: var(--color-sidebar-text-strong);
    font-weight: 700;
    font-size: 0.95rem;
    white-space: nowrap;
  }

  &__nav {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    padding: 0.5rem;
    flex: 1;
    overflow-y: auto;
  }

  &__footer {
    padding: 0.5rem;
    border-top: 1px solid var(--color-sidebar-border);
  }
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

  &:hover {
    background: var(--color-sidebar-hover-bg);
    color: var(--color-sidebar-text-strong);
  }

  svg {
    width: 20px;
    height: 20px;
  }
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

  &--button {
    font-family: inherit;
    text-align: left;
  }

  &:hover {
    background: var(--color-sidebar-hover-bg);
    color: var(--color-sidebar-text-strong);
  }

  &--active {
    background: var(--color-sidebar-active-bg);
    color: var(--color-sidebar-active-text);
  }

  &__icon {
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    width: 20px;
    height: 20px;

    svg {
      width: 100%;
      height: 100%;
    }
  }

  &__label {
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }
}
</style>
