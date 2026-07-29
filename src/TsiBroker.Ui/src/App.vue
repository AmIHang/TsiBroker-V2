<script setup lang="ts">
import { RouterView, useRoute } from 'vue-router'
import AppShell from '@/components/layout/AppShell.vue'

const route = useRoute()
</script>

<template>
  <AppShell v-if="!route.meta.public" :title="route.meta.title">
    <RouterView />
  </AppShell>
  <RouterView v-else />
</template>
