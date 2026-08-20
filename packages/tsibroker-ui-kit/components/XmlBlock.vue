<script setup lang="ts">
import { computed } from 'vue'
import { formatXml } from '../lib/xml'

const props = defineProps<{
  content: string
}>()

const formatted = computed(() => formatXml(props.content))
</script>

<template>
  <pre class="xml-block">{{ formatted }}</pre>
</template>

<style scoped lang="less">
// Fully self-contained on purpose - relying on a page-level `pre` selector previously caused
// this to inherit an unwanted margin-top from an unrelated app stylesheet.
.xml-block {
  margin: 0;
  padding: 0.75rem;
  max-height: 16rem;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
  border-radius: 8px;
  background: var(--color-background-mute);
  color: var(--color-text);
  font-family: ui-monospace, monospace;
  font-size: 0.78rem;
  line-height: 1.5;
}
</style>
