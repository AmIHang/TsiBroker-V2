<script setup lang="ts">
import { useClipboard } from '../composables/useClipboard'

const props = defineProps<{
  text: string
}>()

const { copied, copy } = useClipboard()
</script>

<template>
  <button
    type="button"
    class="copy-button"
    :class="{ 'copy-button--copied': copied }"
    @click.stop.prevent="copy(props.text)"
  >
    {{ copied ? 'Copied!' : 'Copy' }}
  </button>
</template>

<style scoped lang="less">
// .stop.prevent on the click above also keeps this safe to place inside a <summary> —
// preventDefault stops the browser from treating the click as toggling the parent <details>.
.copy-button {
  flex-shrink: 0;
  padding: 0.25rem 0.6rem;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-background);
  color: var(--color-text);
  font-family: initial;
  font-size: 0.75rem;
  line-height: 1.2;
  cursor: pointer;
  opacity: 0.7;
  transition: opacity 0.15s, border-color 0.15s, color 0.15s;

  &:hover {
    opacity: 1;
    border-color: var(--color-border-hover);
  }

  &--copied {
    opacity: 1;
    color: var(--color-success);
    border-color: var(--color-success);
  }
}
</style>
