<script setup lang="ts">
import { useMessages } from '@/composables/useMessages'

const { messages, refresh: loadMessages, reset: resetMessages } = useMessages('Received')

function badgeClassForResult(result: string | null) {
  return result === 'ACK' ? 'badge--success' : 'badge--danger'
}

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
      <table v-if="messages.length > 0" class="data-table">
        <thead>
          <tr>
            <th>Time (UTC)</th>
            <th>Id</th>
            <th>Result</th>
            <th>Content</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="m in messages" :key="m.fileName">
            <td>{{ m.timestamp }}</td>
            <td>{{ m.messageIdentifier ?? '' }}</td>
            <td>
              <span v-if="m.result" class="badge" :class="badgeClassForResult(m.result)">{{ m.result }}</span>
            </td>
            <td>
              <details>
                <summary>{{ m.fileName }}</summary>
                <pre>{{ m.content }}</pre>
              </details>
            </td>
          </tr>
        </tbody>
      </table>
      <p v-else class="empty-state">No messages received yet.</p>
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

.data-table__row {
  cursor: default;
}

.actions {
  display: flex;
  gap: 0.5rem;
}
</style>
