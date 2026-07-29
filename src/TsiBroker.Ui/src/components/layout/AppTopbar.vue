<script setup lang="ts">
defineProps<{
  title?: string
}>()
</script>

<template>
  <header class="topbar">
    <h1 class="topbar__title">{{ title }}</h1>
  </header>
</template>

<style scoped>
.topbar {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  height: var(--topbar-height);
  padding: 0 1.5rem;
  background: var(--color-topbar-bg);
  background-attachment: fixed;
  border-bottom: 1px solid var(--color-topbar-border);
}

.topbar__title {
  margin: 0;
  font-size: 1.15rem;
  font-weight: 600;
  color: var(--color-topbar-text);
}
</style>
