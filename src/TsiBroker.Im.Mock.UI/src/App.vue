<script setup lang="ts">
import { RouterView, useRoute } from 'vue-router'
import MockShell from '@/components/layout/MockShell.vue'

const route = useRoute()
</script>

<template>
  <MockShell v-if="!route.meta.public" :title="route.meta.title">
    <RouterView />
  </MockShell>
  <RouterView v-else />
</template>
