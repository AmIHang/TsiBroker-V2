<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import Shell from '@tsibroker/ui-kit/components/Shell.vue'
import { useAuthStore } from '@/stores/auth'
import AccountMenu from './AccountMenu.vue'
import IconHome from '@/components/icons/IconHome.vue'
import IconOperators from '@/components/icons/IconOperators.vue'
import IconTrain from '@/components/icons/IconTrain.vue'
import IconQueue from '@/components/icons/IconQueue.vue'

defineProps<{
  title?: string
}>()

const { t } = useI18n()
const auth = useAuthStore()
const router = useRouter()

const navItems = computed(() => [
  { to: '/', label: t('sidebar.home'), icon: IconHome },
  { to: '/railway-undertakings', label: t('sidebar.railwayUndertakings'), icon: IconTrain },
  { to: '/infrastructure-operators', label: t('sidebar.infrastructureOperators'), icon: IconOperators },
  { to: '/queues', label: t('sidebar.queues'), icon: IconQueue },
])

async function onLogout() {
  await auth.logout()
  await router.push('/login')
}
</script>

<template>
  <Shell
    :title="title"
    brand="TSI-Broker"
    :nav-items="navItems"
    :expand-label="t('sidebar.expandMenu')"
    :collapse-label="t('sidebar.collapseMenu')"
  >
    <template v-if="auth.username" #sidebar-footer="{ collapsed }">
      <AccountMenu :collapsed="collapsed" :username="auth.username" @logout="onLogout" />
    </template>

    <slot />
  </Shell>
</template>
