<script setup lang="ts">
import { RouterView, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import AppShell from '@/components/layout/AppShell.vue'

const route = useRoute()
const { t } = useI18n()
</script>

<template>
  <AppShell v-if="!route.meta.public" :title="route.meta.title ? t(route.meta.title) : undefined">
    <RouterView />
  </AppShell>
  <RouterView v-else />
</template>
