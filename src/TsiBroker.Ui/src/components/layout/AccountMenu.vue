<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import LanguageSwitcher from '@/components/LanguageSwitcher.vue'

const props = defineProps<{
  collapsed: boolean
  username: string
}>()

const emit = defineEmits<{
  logout: []
}>()

const { t } = useI18n()

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
</script>

<template>
  <div class="sidebar__footer" ref="accountMenuRef">
    <button
      type="button"
      class="nav-item nav-item--button"
      :class="{ 'nav-item--active': showAccountMenu }"
      :aria-expanded="showAccountMenu"
      :title="collapsed ? t('sidebar.account', { user: props.username }) : undefined"
      @click="toggleAccountMenu"
    >
      <span class="nav-item__icon">
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <circle cx="12" cy="8" r="3.2" stroke="currentColor" stroke-width="1.8" />
          <path d="M5 20c0-3.6 3.1-6 7-6s7 2.4 7 6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
        </svg>
      </span>
      <span v-if="!collapsed" class="nav-item__label">{{ props.username }}</span>
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
        <button type="button" class="nav-item nav-item--button" @click="emit('logout')">
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
          <span class="nav-item__label">{{ t('sidebar.logout', { user: props.username }) }}</span>
        </button>
      </div>
    </Teleport>
  </div>
</template>
