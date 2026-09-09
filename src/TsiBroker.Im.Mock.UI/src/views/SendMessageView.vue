<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ApiError, apiFetch } from '@/lib/api'
import MessageLogTable from '@tsibroker/ui-kit/components/MessageLogTable.vue'
import { useMessages } from '@/composables/useMessages'
import { extractTag, extractMessageType } from '@/lib/messageXml'

interface SendDefaults {
  targetUrl: string
  senderRics: string
}

interface TemplateField {
  tag: string
  label: string
  placeholder: string
}

interface TemplateSummary {
  key: string
  messageType: string
}

// Header/envelope tags that already have dedicated inputs (Sender/Recipient) or
// are fixed by the message type itself, so they're excluded from the generated field list.
const NON_FIELD_TAGS = new Set(['MessageType', 'Sender', 'Recipient'])

const sender = ref('')
const recipient = ref('')
const targetUrl = ref('')
const payload = ref('')
const messageTypes = ref<TemplateSummary[]>([])
const messageType = ref('')
const isSending = ref(false)
const templateFields = ref<TemplateField[]>([])
const fieldValues = ref<Record<string, string>>({})

const replyErrorCause = ref<{ messageType: string; messageTypeVersion: string; messageIdentifier: string; messageDateTime: string } | null>(null)

const showForm = ref(false)
const dialogRef = ref<HTMLDialogElement | null>(null)
const uploadInput = ref<HTMLInputElement | null>(null)
const isUploading = ref(false)
const uploadStatus = ref('')
const uploadError = ref('')

const formError = ref('')
const showPreview = ref(false)
const previewDialogRef = ref<HTMLDialogElement | null>(null)

const { messages, refresh: loadMessages } = useMessages('Sent')
const route = useRoute()
const router = useRouter()

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
  replyErrorCause.value = null
  showForm.value = true
}

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
    messageType.value = messageTypes.value[0]!.key
    await loadTemplate()
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

// ErrorMessage's ErrorCauseReference/MessageReference reuses the same tag names
// (MessageType/MessageTypeVersion/MessageIdentifier/MessageDateTime) as the message's own
// envelope, so it can't go through the generic single-occurrence field editor above without
// clobbering - or being clobbered by - the envelope's own values. Filled here by a block-scoped
// substitution instead, from the message this reply is answering (see replyErrorCause, set in
// applyReplyFromQuery).
function applyErrorCauseReference(xml: string): string {
  if (!replyErrorCause.value || !xml.includes('<ErrorCauseReference>')) return xml
  const { messageType, messageTypeVersion, messageIdentifier, messageDateTime } = replyErrorCause.value
  return xml.replace(/<ErrorCauseReference>[\s\S]*?<\/ErrorCauseReference>/, (block) =>
    block
      .replace(/(<MessageType>)[^<]*(<\/MessageType>)/, (_, o, c) => o + messageType + c)
      .replace(/(<MessageTypeVersion>)[^<]*(<\/MessageTypeVersion>)/, (_, o, c) => o + messageTypeVersion + c)
      .replace(/(<MessageIdentifier>)[^<]*(<\/MessageIdentifier>)/, (_, o, c) => o + messageIdentifier + c)
      .replace(/(<MessageDateTime>)[^<]*(<\/MessageDateTime>)/, (_, o, c) => o + messageDateTime + c),
  )
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
  payload.value = applyErrorCauseReference(applyRicsToPayload(result))
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
      body: JSON.stringify({ payload: payload.value, targetUrl: targetUrl.value || null }),
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

function triggerUpload() {
  uploadStatus.value = ''
  uploadError.value = ''
  uploadInput.value?.click()
}

// Sends one or more picked .xml files straight to the broker, each verbatim as its own message -
// no template/field editing in between. Handy for replaying a captured message or a hand-authored
// payload without pasting it into the dialog. The target URL falls back to the same default the
// send dialog uses; MessageIdentifier is pulled from the XML only so the sent log lists it.
async function onFilesSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  input.value = ''
  if (files.length === 0) return

  uploadStatus.value = ''
  uploadError.value = ''
  isUploading.value = true
  let sent = 0
  const failures: string[] = []
  try {
    for (const file of files) {
      const payloadXml = (await file.text()).trim()
      if (!payloadXml) {
        failures.push(`${file.name}: empty file`)
        continue
      }
      try {
        await apiFetch('/api/send', {
          method: 'POST',
          body: JSON.stringify({
            payload: payloadXml,
            targetUrl: targetUrl.value || null,
            messageIdentifier: extractTag(payloadXml, 'MessageIdentifier'),
          }),
        })
        sent++
      } catch (err) {
        failures.push(`${file.name}: ${err instanceof ApiError ? (err.status === 400 ? 'invalid payload' : `HTTP ${err.status}`) : 'send failed'}`)
      }
    }
  } finally {
    isUploading.value = false
    loadMessages()
  }

  uploadStatus.value = sent > 0 ? `Sent ${sent} message${sent === 1 ? '' : 's'}.` : ''
  uploadError.value = failures.join(' · ')
}

// Prefills a reply to a received message (see ReceivedMessagesView's reply action): the query
// carries the original message's Sender/Recipient (swapped - we're replying, so we're now the
// recipient's sender) plus whatever body fields it could pull out, applied only where the
// chosen template actually has a matching field, since not every message type carries all of
// them. Consumed once and stripped from the URL so a refresh doesn't reapply it.
async function applyReplyFromQuery() {
  const query = route.query
  const hasReplyData = [
    'replySender',
    'replyRecipient',
    'replyTrainNumber',
    'replyStartDate',
    'replyLocationCode',
    'replyErrorCauseMessageType',
  ].some((key) => typeof query[key] === 'string')
  if (!hasReplyData) return

  openSendDialog()

  if (typeof query.replyMessageType === 'string' && messageTypes.value.some((t) => t.key === query.replyMessageType)) {
    messageType.value = query.replyMessageType
  }
  if (typeof query.replyErrorCauseMessageType === 'string') {
    replyErrorCause.value = {
      messageType: query.replyErrorCauseMessageType,
      messageTypeVersion: typeof query.replyErrorCauseMessageTypeVersion === 'string' ? query.replyErrorCauseMessageTypeVersion : '',
      messageIdentifier: typeof query.replyErrorCauseMessageIdentifier === 'string' ? query.replyErrorCauseMessageIdentifier : '',
      messageDateTime: typeof query.replyErrorCauseMessageDateTime === 'string' ? query.replyErrorCauseMessageDateTime : '',
    }
  }
  await loadTemplate()

  if (typeof query.replySender === 'string') sender.value = query.replySender
  if (typeof query.replyRecipient === 'string') recipient.value = query.replyRecipient
  payload.value = applyRicsToPayload(payload.value)

  const bodyFieldOverrides: Record<string, string | undefined> = {
    OperationalTrainNumber: typeof query.replyTrainNumber === 'string' ? query.replyTrainNumber : undefined,
    StartDate: typeof query.replyStartDate === 'string' ? query.replyStartDate : undefined,
    PrimaryLocationCode: typeof query.replyLocationCode === 'string' ? query.replyLocationCode : undefined,
  }
  for (const [tag, value] of Object.entries(bodyFieldOverrides)) {
    if (value !== undefined && templateFields.value.some((f) => f.tag === tag)) {
      updateField(tag, value)
    }
  }

  router.replace({ name: 'send', query: {} })
}

// Reopens the send dialog pre-filled from an already-sent message - handy while iterating on a
// test, so the whole form doesn't need retyping for each attempt. Reads straight off the
// message's own XML (rather than re-fetching that type's blank template) so it carries over the
// exact values that were sent, not just the fields the current template happens to define. Only
// the MessageIdentifier is refreshed, since resending the same one verbatim could collide with
// the original.
function duplicateMessage(message: { content: string }) {
  const xml = message.content
  const fields = extractTemplateFields(xml)
  const newIdentifier = generateMessageIdentifier()

  let result = xml
  const values: Record<string, string> = {}
  for (const field of fields) {
    if (field.tag === 'MessageIdentifier') {
      values[field.tag] = newIdentifier
      result = replaceTagContent(result, field.tag, newIdentifier)
    } else {
      values[field.tag] = field.placeholder
    }
  }

  const detectedType = extractMessageType(xml)
  if (detectedType && messageTypes.value.some((t) => t.key === detectedType)) {
    messageType.value = detectedType
  }
  const detectedSender = extractTag(xml, 'Sender')
  if (detectedSender) sender.value = detectedSender
  const detectedRecipient = extractTag(xml, 'Recipient')
  if (detectedRecipient) recipient.value = detectedRecipient

  templateFields.value = fields
  fieldValues.value = values
  payload.value = applyRicsToPayload(result)

  openSendDialog()
}

onMounted(async () => {
  await Promise.all([loadSendDefaults(), loadMessageTypes()])
  await applyReplyFromQuery()
})
</script>

<template>
  <div class="send">
    <div class="toolbar">
      <button type="button" class="btn btn--primary" @click="openSendDialog">+ New message</button>
      <button type="button" class="btn btn--primary" :disabled="isUploading" @click="triggerUpload">
        {{ isUploading ? 'Uploading…' : 'Upload XML' }}
      </button>
      <input
        ref="uploadInput"
        type="file"
        class="upload-input"
        accept=".xml,text/xml,application/xml"
        multiple
        @change="onFilesSelected"
      />
      <span v-if="uploadStatus" class="upload-msg">{{ uploadStatus }}</span>
      <span v-if="uploadError" class="upload-msg upload-msg--error">{{ uploadError }}</span>
    </div>

    <dialog ref="dialogRef" class="modal" @close="showForm = false" @cancel="showForm = false">
      <form class="modal__form modal__form--lg" @submit.prevent="send">
        <div class="modal__header">
          <div class="modal__header-actions">
            <h2 class="modal__title">Send a message (Mock &rarr; Broker, /ci)</h2>
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
        <div class="row row--type">
          <label class="field field--type">
            <span class="field__label">Message type</span>
            <select v-model="messageType" @change="onMessageTypeChange">
              <option v-for="type in messageTypes" :key="type.key" :value="type.key">{{ type.messageType }} - {{ type.key }}</option>
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

    <section class="card sent-card">
      <h2 class="card__title">Sent messages</h2>
      <div class="sent-list">
        <MessageLogTable
          :messages="messages"
          empty-message="No messages sent yet."
          duplicatable
          :response-copyable="false"
          @duplicate="duplicateMessage"
        />
      </div>
    </section>
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.card*, .field*, .toolbar, .btn*, .modal*, .data-table*, .badge*,
// .empty-state, .error) come from src/assets/styles — only this view's own layout lives here.
// Fills the shell's content area (which is itself locked to the viewport height) so the sent
// list can flex into whatever space is left and scroll on its own, rather than the whole page
// scrolling once the log gets long.
.send {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  height: 100%;
  min-height: 0;
}

.sent-card {
  flex: 1;
  min-height: 0;
}

// .toolbar (from the ui-kit) is a gapless flex row built for a single button - this view puts
// several controls plus status text in it, so space and vertically centre them here.
.toolbar {
  gap: 0.75rem;
  align-items: center;
  flex-wrap: wrap;
}

.upload-input {
  display: none;
}

.upload-msg {
  font-size: 0.85rem;
  opacity: 0.8;

  &--error {
    color: var(--color-danger);
    opacity: 1;
  }
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

// Flexes to fill the card and scrolls inside itself once the log gets long, so the page itself
// never grows a scrollbar (toolbar and "+ New message" button stay put).
.sent-list {
  flex: 1;
  min-height: 0;
  overflow: auto;

  // Header row stays visible while scrolling. .data-table th has no background of its own, so
  // give it the card's - otherwise rows show through as they pass underneath.
  :deep(.data-table thead th) {
    position: sticky;
    top: 0;
    z-index: 1;
    background: var(--color-background);
  }
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
