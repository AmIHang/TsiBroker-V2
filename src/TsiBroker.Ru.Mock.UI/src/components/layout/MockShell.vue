<script setup lang="ts">
import { useRouter } from 'vue-router'
import Shell from '@tsibroker/ui-kit/components/Shell.vue'
import { useAuthStore } from '@/stores/auth'
import IconSend from '@/components/icons/IconSend.vue'
import IconInbox from '@/components/icons/IconInbox.vue'
import IconSettings from '@/components/icons/IconSettings.vue'
import IconWhoAmI from '@/components/icons/IconWhoAmI.vue'

defineProps<{
  title?: string
}>()

const auth = useAuthStore()
const router = useRouter()

const navItems = [
  { to: '/', label: 'Send message', icon: IconSend },
  { to: '/received', label: 'Received messages', icon: IconInbox },
  { to: '/settings', label: 'Response settings', icon: IconSettings },
  { to: '/whoami', label: 'WhoAmI', icon: IconWhoAmI },
]

async function onLogout() {
  await auth.logout()
  await router.push('/login')
}
</script>

<template>
  <Shell
    :title="title"
    brand="TSI-Broker | EVU"
    :nav-items="navItems"
    expand-label="Expand menu"
    collapse-label="Collapse menu"
  >
    <template v-if="auth.username" #sidebar-footer>
      <div class="sidebar__footer">
        <button
          type="button"
          class="nav-item nav-item--button"
          :title="`Log out (${auth.username})`"
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
          <span class="nav-item__label">Log out ({{ auth.username }})</span>
        </button>
      </div>
    </template>

    <slot />
  </Shell>
</template>
