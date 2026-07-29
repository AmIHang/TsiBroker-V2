<script setup lang="ts">
import { useSidebar } from '@/composables/useSidebar'
import AppSidebar from './AppSidebar.vue'
import AppTopbar from './AppTopbar.vue'

defineProps<{
  title?: string
}>()

const { collapsed } = useSidebar()
</script>

<template>
  <div class="app-shell" :class="{ 'app-shell--collapsed': collapsed }">
    <AppSidebar />

    <div class="app-shell__main">
      <AppTopbar :title="title" />
      <main class="app-shell__content">
        <slot />
      </main>
    </div>
  </div>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
}

.app-shell__main {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  margin-left: var(--sidebar-width-expanded);
  transition: margin-left 0.18s ease;
}

.app-shell--collapsed .app-shell__main {
  margin-left: var(--sidebar-width-collapsed);
}

.app-shell__content {
  flex: 1;
  padding: 1.75rem;
}
</style>
