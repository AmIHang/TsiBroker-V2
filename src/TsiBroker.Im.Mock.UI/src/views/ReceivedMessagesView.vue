<script setup lang="ts">
import MessageLogTable from '@tsibroker/ui-kit/components/MessageLogTable.vue'
import { useRouter } from 'vue-router'
import { useMessages } from '@/composables/useMessages'

const { messages, refresh: loadMessages, reset: resetMessages } = useMessages('Received')
const router = useRouter()

function clearLog() {
  if (confirm('Clear the whole message log (received and sent)? This cannot be undone.')) {
    resetMessages()
  }
}

function extractTag(xml: string, tag: string): string | null {
  const match = new RegExp(`<${tag}>([^<]*)</${tag}>`).exec(xml)
  const value = match?.[1]?.trim()
  return value ? value : null
}

// The message type is just the payload's root element name (see MessageTemplates.ByMessageType
// on the sending side, whose keys match these verbatim).
function extractMessageType(xml: string): string | null {
  return /^\s*<([A-Za-z]+)[\s>]/.exec(xml)?.[1] ?? null
}

// Opens the send dialog with a reply pre-filled from this received message: Sender/Recipient
// swapped (we're replying, so we become the sender addressing whoever sent this), plus whatever
// TrainIdentifier/Location fields are present - "Send a message" applies them only where the
// chosen template actually has a matching field, since not every message type carries all of
// them.
function replyTo(message: { content: string }) {
  const xml = message.content
  const query: Record<string, string> = {}

  const originalSender = extractTag(xml, 'Sender')
  const originalRecipient = extractTag(xml, 'Recipient')
  if (originalRecipient) query.replySender = originalRecipient
  if (originalSender) query.replyRecipient = originalSender

  const trainNumber = extractTag(xml, 'OperationalTrainNumber')
  if (trainNumber) query.replyTrainNumber = trainNumber
  const startDate = extractTag(xml, 'StartDate')
  if (startDate) query.replyStartDate = startDate
  const locationCode = extractTag(xml, 'PrimaryLocationCode')
  if (locationCode) query.replyLocationCode = locationCode

  const messageType = extractMessageType(xml)
  if (messageType) query.replyMessageType = messageType

  router.push({ name: 'send', query })
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
      <MessageLogTable :messages="messages" empty-message="No messages received yet." replyable @reply="replyTo" />
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
