<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiFetch } from '@/lib/api'
import { useMessages } from '@/composables/useMessages'

interface SendDefaults {
  targetUrl: string
  senderRics: string
}

interface SendResult {
  success: boolean
  status: string
  responseXml: string | null
  error: string | null
}

const sender = ref('')
const recipient = ref('')
const targetUrl = ref('')
const payload = ref('')
const messageTypes = ref<string[]>([])
const messageType = ref('')
const sendResult = ref<SendResult | null>(null)
const isSending = ref(false)

const { messages, refresh: loadMessages } = useMessages('Sent')

async function loadSendDefaults() {
  const res = await apiFetch('/api/send/defaults')
  const defaults: SendDefaults = await res.json()
  targetUrl.value = defaults.targetUrl
  sender.value = defaults.senderRics
}

async function loadMessageTypes() {
  const res = await apiFetch('/api/send/templates')
  messageTypes.value = await res.json()
  if (!messageType.value && messageTypes.value.length > 0) {
    messageType.value = messageTypes.value[0]!
  }
}

// Fills MessageHeader/Sender and /Recipient in the payload from the Sender/Recipient fields, so
// those fields are the source of truth for the XML rather than something you'd otherwise
// hand-edit inside the textarea.
function applyRicsToPayload(xml: string): string {
  let result = xml
  if (sender.value.trim()) {
    result = result.replace(/(<Sender>)[^<]*(<\/Sender>)/, (_, open, close) => open + sender.value.trim() + close)
  }
  if (recipient.value.trim()) {
    result = result.replace(/(<Recipient>)[^<]*(<\/Recipient>)/, (_, open, close) => open + recipient.value.trim() + close)
  }
  return result
}

async function loadTemplate() {
  if (!messageType.value) return
  const res = await apiFetch('/api/send/templates/' + encodeURIComponent(messageType.value))
  payload.value = applyRicsToPayload(await res.text())
}

function badgeClassForResult(result: string | null) {
  return result === 'ACK' ? 'badge--success' : 'badge--danger'
}

async function send() {
  payload.value = applyRicsToPayload(payload.value)
  isSending.value = true
  try {
    const res = await apiFetch('/api/send', {
      method: 'POST',
      body: JSON.stringify({ payload: payload.value, targetUrl: targetUrl.value || null }),
    })
    sendResult.value = await res.json()
  } finally {
    isSending.value = false
    loadMessages()
  }
}

onMounted(() => {
  loadSendDefaults()
  loadMessageTypes()
})
</script>

<template>
  <div class="send">
    <section class="card">
      <h2 class="card__title">Send a message (Mock &rarr; Broker, /ci)</h2>
      <div class="row">
        <label class="field">
          <span class="field__label">Sender (RICS)</span>
          <input v-model="sender" />
        </label>
        <label class="field">
          <span class="field__label">Recipient (RICS)</span>
          <input v-model="recipient" />
        </label>
      </div>
      <label class="field">
        <span class="field__label">Target URL</span>
        <input v-model="targetUrl" />
      </label>
      <label class="field">
        <span class="field__label">Payload</span>
        <textarea v-model="payload" class="payload" placeholder="Message XML"></textarea>
      </label>
      <div class="toolbar">
        <select v-model="messageType">
          <option v-for="type in messageTypes" :key="type" :value="type">{{ type }}</option>
        </select>
        <button class="btn" @click="loadTemplate">Load template</button>
        <button class="btn btn--primary" :disabled="isSending" @click="send">Send</button>
      </div>
      <div v-if="sendResult">
        <span class="badge" :class="badgeClassForResult(sendResult.status)">{{ sendResult.status }}</span>
        <span v-if="sendResult.error" class="status-text">{{ sendResult.error }}</span>
        <pre v-if="sendResult.responseXml">{{ sendResult.responseXml }}</pre>
      </div>
    </section>

    <section class="card">
      <h2 class="card__title">Sent messages</h2>
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
      <p v-else class="empty-state">No messages sent yet.</p>
    </section>
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.card*, .field*, .toolbar, .btn*, .data-table*, .badge*,
// .empty-state) come from src/assets/styles — only this view's own layout lives here.
.send {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.status-text {
  font-size: 0.85rem;
  opacity: 0.75;
  margin-left: 0.6rem;
}

.payload {
  min-height: 10rem;
  font-family: ui-monospace, monospace;
  font-size: 0.8rem;
  resize: vertical;
}

.data-table__row {
  cursor: default;
}
</style>
