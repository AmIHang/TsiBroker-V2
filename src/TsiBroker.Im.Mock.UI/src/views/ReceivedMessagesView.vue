<script setup lang="ts">
import MessageLogTable from '@tsibroker/ui-kit/components/MessageLogTable.vue'
import { useMessages } from '@/composables/useMessages'

const { messages, refresh: loadMessages, reset: resetMessages } = useMessages('Received')

function clearLog() {
  if (confirm('Clear the whole message log (received and sent)? This cannot be undone.')) {
    resetMessages()
  }
}
</script>

<template>
  <div class="received">
    <section class="card">
      <div class="toolbar" style="justify-content: space-between">
        <h2 class="card__title">Received messages (Broker &rarr; Mock, /ci)</h2>
        <div class="actions">
          <button class="btn btn--small" @click="loadMessages">Refresh</button>
          <button class="btn btn--small btn--danger" @click="clearLog">Clear log</button>
        </div>
      </div>
      <MessageLogTable :messages="messages" empty-message="No messages received yet." />
    </section>
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.card*, .toolbar, .btn*, .data-table*, .badge*, .empty-state)
// come from src/assets/styles — only this view's own layout lives here.
.received {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.actions {
  display: flex;
  gap: 0.5rem;
}
</style>
