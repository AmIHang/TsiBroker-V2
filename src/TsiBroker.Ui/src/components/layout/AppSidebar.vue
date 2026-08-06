<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useSidebar } from '@/composables/useSidebar'
import LanguageSwitcher from '@/components/LanguageSwitcher.vue'

const { collapsed, toggle: toggleCollapsed } = useSidebar()
const auth = useAuthStore()
const router = useRouter()
const route = useRoute()
const { t } = useI18n()

const navItems = computed(
  () =>
    [
      { to: '/', label: t('sidebar.home'), icon: 'home' },
      { to: '/railway-undertakings', label: t('sidebar.railwayUndertakings'), icon: 'train' },
      { to: '/infrastructure-operators', label: t('sidebar.infrastructureOperators'), icon: 'operators' },
      { to: '/queues', label: t('sidebar.queues'), icon: 'queue' },
    ] as const,
)

function isNavItemActive(to: string) {
  return to === '/' ? route.path === '/' : route.path === to || route.path.startsWith(`${to}/`)
}

const showAccountMenu = ref(false)
const accountMenuRef = ref<HTMLElement | null>(null)
const accountPanelRef = ref<HTMLElement | null>(null)

function toggleAccountMenu() {
  showAccountMenu.value = !showAccountMenu.value
}

function onDocumentClick(event: MouseEvent) {
  const target = event.target as Node
  if (
    showAccountMenu.value &&
    !accountMenuRef.value?.contains(target) &&
    !accountPanelRef.value?.contains(target)
  ) {
    showAccountMenu.value = false
  }
}

onMounted(() => document.addEventListener('click', onDocumentClick))
onBeforeUnmount(() => document.removeEventListener('click', onDocumentClick))

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
        :aria-label="collapsed ? t('sidebar.expandMenu') : t('sidebar.collapseMenu')"
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
          <svg v-else-if="item.icon === 'queue'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <rect x="3" y="5" width="18" height="4" rx="1" stroke="currentColor" stroke-width="1.8" />
            <rect x="3" y="10.5" width="18" height="4" rx="1" stroke="currentColor" stroke-width="1.8" />
            <rect x="3" y="16" width="18" height="4" rx="1" stroke="currentColor" stroke-width="1.8" />
          </svg>
        </span>
        <span v-if="!collapsed" class="nav-item__label">{{ item.label }}</span>
      </RouterLink>
    </nav>

    <div v-if="auth.username" class="sidebar__footer" ref="accountMenuRef">
      <button
        type="button"
        class="nav-item nav-item--button"
        :class="{ 'nav-item--active': showAccountMenu }"
        :aria-expanded="showAccountMenu"
        :title="collapsed ? t('sidebar.account', { user: auth.username }) : undefined"
        @click="toggleAccountMenu"
      >
        <span class="nav-item__icon">
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <circle cx="12" cy="8" r="3.2" stroke="currentColor" stroke-width="1.8" />
            <path d="M5 20c0-3.6 3.1-6 7-6s7 2.4 7 6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
          </svg>
        </span>
        <span v-if="!collapsed" class="nav-item__label">{{ auth.username }}</span>
        <svg v-if="!collapsed" class="nav-item__chevron" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M9 6l6 6-6 6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>

      <Teleport to="body">
        <div
          v-if="showAccountMenu"
          ref="accountPanelRef"
          class="account-flyout"
          :class="{ 'account-flyout--collapsed': collapsed }"
        >
          <LanguageSwitcher class="sidebar__language" />
          <button type="button" class="nav-item nav-item--button" @click="onLogout">
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
            <span class="nav-item__label">{{ t('sidebar.logout', { user: auth.username }) }}</span>
          </button>
        </div>
      </Teleport>
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

  &__language {
    color: var(--color-sidebar-text);
    width: 100%;
  }
}

// Teleported to <body> so it isn't clipped by .sidebar's `overflow: hidden`
// (needed for the collapse-width transition). Positioned as a viewport-fixed
// flyout anchored just to the right of the sidebar.
.account-flyout {
  position: fixed;
  left: calc(var(--sidebar-width-expanded) + 0.5rem);
  bottom: 0.5rem;
  z-index: 30;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  width: 220px;
  padding: 0.5rem;
  background: var(--color-sidebar-bg);
  border: 1px solid var(--color-sidebar-border);
  border-radius: 10px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.18);
  color: var(--color-sidebar-text);

  &--collapsed {
    left: calc(var(--sidebar-width-collapsed) + 0.5rem);
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

  &__chevron {
    width: 16px;
    height: 16px;
    flex-shrink: 0;
    margin-left: auto;
  }
}
</style>
