import { ref } from 'vue'

// Copies text to the clipboard and reports back a short-lived `copied` flag so callers can show
// "Copied!" feedback. Used by CopyButton and by MessageLogTable's click-to-copy filename.
export function useClipboard(resetAfterMs = 1500) {
  const copied = ref(false)
  let copiedTimeout: ReturnType<typeof setTimeout> | undefined

  async function copy(text: string) {
    if (navigator.clipboard) {
      await navigator.clipboard.writeText(text)
    } else {
      // No Clipboard API (e.g. insecure context) — fall back to the classic hidden-textarea trick.
      const textarea = document.createElement('textarea')
      textarea.value = text
      textarea.style.position = 'fixed'
      textarea.style.opacity = '0'
      document.body.appendChild(textarea)
      textarea.select()
      document.execCommand('copy')
      document.body.removeChild(textarea)
    }

    copied.value = true
    clearTimeout(copiedTimeout)
    copiedTimeout = setTimeout(() => (copied.value = false), resetAfterMs)
  }

  return { copied, copy }
}
