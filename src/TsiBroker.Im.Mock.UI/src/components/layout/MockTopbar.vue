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

<style scoped lang="less">
.topbar {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  gap: 1rem;
  min-height: var(--topbar-height);
  padding: 0.5rem 1.5rem;
  background: var(--color-topbar-bg);
  background-attachment: fixed;
  border-bottom: 1px solid var(--color-topbar-border);

  // &__title comes from the shared styles/topbar.less (see comment there)
}
</style>
