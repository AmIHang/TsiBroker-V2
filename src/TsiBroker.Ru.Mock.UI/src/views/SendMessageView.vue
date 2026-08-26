<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { ApiError, apiFetch } from '@/lib/api'
import MessageLogTable from '@tsibroker/ui-kit/components/MessageLogTable.vue'
import { useMessages } from '@/composables/useMessages'

interface SendDefaults {
  targetUrl: string
  senderRics: string
  apiKey: string
}

interface TemplateField {
  tag: string
  label: string
  placeholder: string
}

// Header/envelope tags that already have dedicated inputs (Sender/Recipient) or
// are fixed by the message type itself, so they're excluded from the generated field list.
const NON_FIELD_TAGS = new Set(['MessageType', 'Sender', 'Recipient'])

const sender = ref('')
const recipient = ref('')
const targetUrl = ref('')
const apiKey = ref('')
const payload = ref('')
const messageTypes = ref<string[]>([])
const messageType = ref('')
const isSending = ref(false)
const templateFields = ref<TemplateField[]>([])
const fieldValues = ref<Record<string, string>>({})

const showForm = ref(false)
const dialogRef = ref<HTMLDialogElement | null>(null)
const formError = ref('')
const showPreview = ref(false)
const previewDialogRef = ref<HTMLDialogElement | null>(null)

const { messages, refresh: loadMessages } = useMessages('Sent')

watch(showForm, (value) => {
  if (value) {
    dialogRef.value?.showModal()
  } else {
    dialogRef.value?.close()
  }
})

// Stacks on top of the send dialog (native <dialog> supports nested showModal() calls) rather
// than replacing its content, so the field-grid edits and the raw payload stay on the same
// underlying `payload` ref and neither view needs to sync into the other on open/close.
watch(showPreview, (value) => {
  if (value) {
    previewDialogRef.value?.showModal()
  } else {
    previewDialogRef.value?.close()
  }
})

function openSendDialog() {
  formError.value = ''
  showPreview.value = false
  showForm.value = true
}

async function loadSendDefaults() {
  const res = await apiFetch('/api/send/defaults')
  const defaults: SendDefaults = await res.json()
  targetUrl.value = defaults.targetUrl
  sender.value = defaults.senderRics
  apiKey.value = defaults.apiKey
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

function humanizeTag(tag: string): string {
  return tag.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
}

function replaceTagContent(xml: string, tag: string, value: string): string {
  const regex = new RegExp(`(<${tag}>)[^<]*(</${tag}>)`)
  return xml.replace(regex, (_, open, close) => open + value + close)
}

function generateMessageIdentifier(): string {
  return 'MOCK-' + Date.now().toString(36).toUpperCase() + '-' + Math.random().toString(36).slice(2, 6).toUpperCase()
}

// Every leaf element (a tag with plain text content, no children) is a field that's
// meaningful to the message body - e.g. train number, location, delay - as opposed to
// envelope plumbing like MessageType/Sender/Recipient which already have their own inputs.
// The template's own text becomes the field's placeholder, so a still-required
// "REPLACE-WITH-..." token shows as grey hint text rather than as a value to overtype.
function extractTemplateFields(xml: string): TemplateField[] {
  const fields: TemplateField[] = []
  const seen = new Set<string>()
  const regex = /<([A-Za-z]+)>([^<]*)<\/\1>/g
  let match: RegExpExecArray | null
  while ((match = regex.exec(xml))) {
    const tag = match[1]!
    if (NON_FIELD_TAGS.has(tag) || seen.has(tag)) continue
    seen.add(tag)
    fields.push({ tag, label: humanizeTag(tag), placeholder: match[2]! })
  }
  return fields
}

async function loadTemplate() {
  if (!messageType.value) return
  const res = await apiFetch('/api/send/templates/' + encodeURIComponent(messageType.value))
  const xml = await res.text()
  const fields = extractTemplateFields(xml)

  let result = xml
  const values: Record<string, string> = {}
  for (const field of fields) {
    if (field.tag === 'MessageIdentifier' && field.placeholder.startsWith('REPLACE-WITH')) {
      // Required and must be unique - filling in a generated id beats leaving a token
      // that would collide with every other unsent message.
      const value = generateMessageIdentifier()
      values[field.tag] = value
      result = replaceTagContent(result, field.tag, value)
    } else if (field.placeholder.startsWith('REPLACE-WITH')) {
      values[field.tag] = ''
    } else {
      values[field.tag] = field.placeholder
    }
  }

  templateFields.value = fields
  fieldValues.value = values
  payload.value = applyRicsToPayload(result)
}

function updateField(tag: string, value: string) {
  fieldValues.value[tag] = value
  const field = templateFields.value.find((f) => f.tag === tag)
  payload.value = replaceTagContent(payload.value, tag, value || field?.placeholder || '')
}

// Switching the message type dropdown loads the new template's structure, but shouldn't throw
// away values (train number, date, location, ...) the tester already entered for fields that
// exist under the same tag name in both message types - that's what the separate "Reset to
// template" button is for. MessageIdentifier is excluded since it must stay a freshly generated,
// unique id rather than carrying over the previous message's.
async function onMessageTypeChange() {
  const previousValues = { ...fieldValues.value }
  await loadTemplate()
  for (const field of templateFields.value) {
    if (field.tag === 'MessageIdentifier') continue
    const previous = previousValues[field.tag]
    if (previous) {
      updateField(field.tag, previous)
    }
  }
}

async function send() {
  payload.value = applyRicsToPayload(payload.value)
  formError.value = ''
  isSending.value = true
  try {
    await apiFetch('/api/send', {
      method: 'POST',
      body: JSON.stringify({
        payload: payload.value,
        targetUrl: targetUrl.value || null,
        apiKey: apiKey.value || null,
      }),
    })
    // The broker's response (ACK/NACK, or the mock API's own send failure) is now logged
    // alongside the sent message, so closing here and letting the list pick it up is enough -
    // no need to keep the dialog open just to show the result.
    showForm.value = false
    loadMessages()
  } catch (err) {
    formError.value = err instanceof ApiError ? 'Send failed: ' + (err.status === 400 ? 'invalid payload.' : `HTTP ${err.status}.`) : 'Send failed.'
  } finally {
    isSending.value = false
  }
}

watch(messageType, () => onMessageTypeChange())

onMounted(() => {
  loadSendDefaults()
  loadMessageTypes()
})
</script>

<template>
  <div class="send">
    <div class="toolbar">
      <button type="button" class="btn btn--primary" @click="openSendDialog">+ New message</button>
    </div>

    <dialog ref="dialogRef" class="modal" @close="showForm = false" @cancel="showForm = false">
      <form class="modal__form modal__form--lg" @submit.prevent="send">
        <div class="modal__header">
          <div class="modal__header-actions">
            <h2 class="modal__title">Send a message (Mock &rarr; Broker, /message)</h2>
            <button
              type="button"
              class="icon-btn-header"
              title="Edit payload"
              aria-label="Edit payload"
              @click="showPreview = true"
            >
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path
                  d="M8 6l-5 6 5 6M16 6l5 6-5 6"
                  stroke="currentColor"
                  stroke-width="1.8"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
              </svg>
            </button>
          </div>
          <button type="button" class="modal__close" aria-label="Close" @click="showForm = false">
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
            </svg>
          </button>
        </div>

        <div class="row">
          <label class="field">
            <span class="field__label">Sender (RICS - EVU)</span>
            <input v-model="sender" />
          </label>
          <label class="field">
            <span class="field__label">Recipient (RICS - ISB)</span>
            <input v-model="recipient" />
          </label>
        </div>
        <div class="row">
          <label class="field">
            <span class="field__label">Target URL</span>
            <input v-model="targetUrl" />
          </label>
          <label class="field">
            <span class="field__label">API Key (X-Api-Key)</span>
            <input v-model="apiKey" />
          </label>
        </div>
        <div class="row row--type">
          <label class="field field--type">
            <span class="field__label">Message type</span>
            <select v-model="messageType">
              <option v-for="type in messageTypes" :key="type" :value="type">{{ type }}</option>
            </select>
          </label>
          <button
            class="icon-btn-header reset-btn"
            type="button"
            title="Reset to template"
            aria-label="Reset to template"
            @click="loadTemplate"
          >
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M20 12a8 8 0 1 1-2.34-5.66" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
              <path d="M20 4v5h-5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
          </button>
        </div>
        <div v-if="templateFields.length > 0" class="fields-grid">
          <label v-for="field in templateFields" :key="field.tag" class="field">
            <span class="field__label">{{ field.label }}</span>
            <input
              :value="fieldValues[field.tag]"
              :placeholder="field.placeholder.startsWith('REPLACE-WITH') ? field.placeholder : ''"
              @input="updateField(field.tag, ($event.target as HTMLInputElement).value)"
            />
          </label>
        </div>
        <p v-if="formError" class="error">{{ formError }}</p>

        <div class="modal__actions">
          <button type="button" class="btn" @click="showForm = false">Cancel</button>
          <button type="submit" class="btn btn--primary" :disabled="isSending">Send</button>
        </div>
      </form>
    </dialog>

    <dialog ref="previewDialogRef" class="modal" @close="showPreview = false" @cancel="showPreview = false">
      <div class="modal__form modal__form--lg">
        <div class="modal__header">
          <h2 class="modal__title">Payload</h2>
          <button type="button" class="modal__close" aria-label="Close" @click="showPreview = false">
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
            </svg>
          </button>
        </div>
        <label class="field">
          <span class="field__label">Message XML</span>
          <textarea v-model="payload" class="payload" placeholder="Message XML"></textarea>
        </label>
        <div class="modal__actions">
          <button type="button" class="btn btn--primary" @click="showPreview = false">Done</button>
        </div>
      </div>
    </dialog>

    <section class="card">
      <h2 class="card__title">Sent messages</h2>
      <MessageLogTable :messages="messages" empty-message="No messages sent yet." />
    </section>
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.card*, .field*, .toolbar, .btn*, .modal*, .data-table*, .badge*,
// .empty-state, .error) come from src/assets/styles — only this view's own layout lives here.
.send {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.row--type {
  align-items: flex-end;
}

.field--type {
  flex: none;
  width: 400px;
}

.reset-btn {
  margin-bottom: 6px;
}

.fields-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 1rem;
}

.payload {
  min-height: 16rem;
  padding: 0.85rem;
  border-radius: 10px;
  background: var(--color-background-mute);
  font-family: ui-monospace, monospace;
  font-size: 0.8rem;
  line-height: 1.5;
  white-space: pre;
  resize: vertical;
}
</style>
