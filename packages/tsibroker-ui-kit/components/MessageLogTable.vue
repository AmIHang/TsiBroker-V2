<script setup lang="ts">
import { ref } from 'vue'
import { useClipboard } from '../composables/useClipboard'
import CopyButton from './CopyButton.vue'
import XmlBlock from './XmlBlock.vue'

interface LoggedMessage {
  fileName: string
  timestamp: string
  messageIdentifier: string | null
  result: string | null
  content: string
  responseContent: string | null
}

withDefaults(
  defineProps<{
    messages: LoggedMessage[]
    emptyMessage: string
    // Shows a reply action per row - only received messages have anywhere meaningful to reply
    // to, so callers opt in rather than this being inferred from message direction.
    replyable?: boolean
  }>(),
  { replyable: false },
)

const emit = defineEmits<{
  reply: [message: LoggedMessage]
}>()

function badgeClassForResult(result: string | null) {
  return result === 'ACK' ? 'badge--success' : 'badge--danger'
}

const { copied, copy } = useClipboard()
const copiedFileName = ref<string | null>(null)

async function copyContent(m: LoggedMessage) {
  copiedFileName.value = m.fileName
  await copy(m.content)
}

// Which rows currently show their XML in a full-width row underneath - a Set rather than a
// single value, so several messages can be expanded side by side.
const expandedFiles = ref(new Set<string>())

function toggleExpanded(fileName: string) {
  if (expandedFiles.value.has(fileName)) {
    expandedFiles.value.delete(fileName)
  } else {
    expandedFiles.value.add(fileName)
  }
}
</script>

<template>
  <table v-if="messages.length > 0" class="data-table message-log">
    <thead>
      <tr>
        <th>Time (UTC)</th>
        <th>Id</th>
        <th>Result</th>
        <th>File</th>
        <th class="message-log__toggle-header"></th>
      </tr>
    </thead>
    <tbody>
      <template v-for="m in messages" :key="m.fileName">
        <tr>
          <td>{{ m.timestamp }}</td>
          <td>{{ m.messageIdentifier ?? '' }}</td>
          <td>
            <span v-if="m.result" class="badge" :class="badgeClassForResult(m.result)">{{ m.result }}</span>
          </td>
          <td>
            <button type="button" class="message-log__filename" title="Copy content" @click="copyContent(m)">
              {{ m.fileName }}
            </button>
            <span
              class="message-log__copied"
              :class="{ 'message-log__copied--visible': copied && copiedFileName === m.fileName }"
              >Copied!</span
            >
          </td>
          <td>
            <!-- .data-table__actions is `display: flex`, which would strip this <td>'s table-cell
            display and break it out of the row's shared height (and border-bottom position) -
            so the flex layout goes on an inner div instead of the cell itself. -->
            <div class="data-table__actions">
              <button
                v-if="replyable"
                type="button"
                class="icon-btn-header"
                title="Reply"
                aria-label="Reply"
                @click="emit('reply', m)"
              >
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path
                    d="M9 14l-5-5 5-5M4 9h10a5 5 0 0 1 5 5v2"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  />
                </svg>
              </button>
              <button
                type="button"
                class="icon-btn-header message-log__toggle"
                :class="{ 'message-log__toggle--open': expandedFiles.has(m.fileName) }"
                :aria-label="expandedFiles.has(m.fileName) ? 'Collapse' : 'Expand'"
                :title="expandedFiles.has(m.fileName) ? 'Collapse' : 'Expand'"
                @click="toggleExpanded(m.fileName)"
              >
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path
                    d="M9 6l6 6-6 6"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  />
                </svg>
              </button>
            </div>
          </td>
        </tr>
        <tr v-if="expandedFiles.has(m.fileName)" class="message-log__xml-row">
          <td colspan="5">
            <div class="message-log__xml-grid">
              <div class="message-log__xml-col">
                <span class="message-log__xml-label">Request</span>
                <XmlBlock :content="m.content" />
              </div>
              <div v-if="m.responseContent" class="message-log__xml-col">
                <div class="copy-row">
                  <span class="message-log__xml-label">Response</span>
                  <CopyButton :text="m.responseContent" />
                </div>
                <XmlBlock :content="m.responseContent" />
              </div>
            </div>
          </td>
        </tr>
      </template>
    </tbody>
  </table>
  <p v-else class="empty-state">{{ emptyMessage }}</p>
</template>

<style scoped lang="less">
// Rows can differ in content height (badge vs. none, expanded XML row, ...) - keep every
// cell's own content vertically centered rather than the browser's baseline default, which
// otherwise pushes the toggle button off-center relative to the filename/badge next to it.
td {
  vertical-align: middle;
}

.message-log {
  &__toggle-header {
    width: 2.5rem;
  }

  &__filename {
    padding: 0;
    border: none;
    background: none;
    color: inherit;
    font: inherit;
    font-family: ui-monospace, monospace;
    font-size: 0.78rem;
    cursor: pointer;
    text-align: left;

    &:hover {
      text-decoration: underline;
    }
  }

  &__copied {
    margin-left: 0.5rem;
    font-size: 0.75rem;
    color: var(--color-text);
    opacity: 0;
    transition: opacity 0.15s;

    &--visible {
      opacity: 0.7;
    }
  }

  &__toggle svg {
    transition: transform 0.15s;
  }

  &__toggle--open svg {
    transform: rotate(90deg);
  }

  // auto-fit + minmax rather than a fixed 2-column layout: side by side once there's room for
  // both at a readable width, stacked below that (narrow viewport, or a sidebar squeezing the
  // table) - matches the same responsive pattern used for .fields-grid.
  &__xml-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    gap: 0.75rem;
  }

  &__xml-col {
    min-width: 0;
  }

  &__xml-label {
    display: block;
    margin-bottom: 0.35rem;
    font-size: 0.78rem;
    font-weight: 600;
    opacity: 0.7;
  }
}
</style>
