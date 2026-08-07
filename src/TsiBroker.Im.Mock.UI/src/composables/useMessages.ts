import { onBeforeUnmount, onMounted, ref } from 'vue'
import { apiFetch } from '@/lib/api'

export interface MockMessage {
  fileName: string
  direction: 'Sent' | 'Received'
  timestamp: string
  messageIdentifier: string | null
  result: string | null
  content: string
}

// Polls the shared /api/messages log and keeps only the given direction - used by both
// SendMessageView (its own Sent log) and ReceivedMessagesView (the Received log).
export function useMessages(direction: MockMessage['direction']) {
  const messages = ref<MockMessage[]>([])

  async function load() {
    const res = await apiFetch('/api/messages')
    const all: MockMessage[] = await res.json()
    messages.value = all.filter((m) => m.direction === direction)
  }

  let interval: ReturnType<typeof setInterval> | undefined

  onMounted(() => {
    load()
    interval = setInterval(load, 5000)
  })

  onBeforeUnmount(() => clearInterval(interval))

  return { messages, refresh: load }
}
