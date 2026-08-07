<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiFetch } from '@/lib/api'

interface ResponseConfigDto {
  mode: string
  delayMs: number
}

const mode = ref('Ack')
const delayMs = ref(0)
const configStatus = ref('')

async function loadConfig() {
  const res = await apiFetch('/api/response-config')
  const config: ResponseConfigDto = await res.json()
  mode.value = config.mode
  delayMs.value = config.delayMs
}

async function saveConfig() {
  configStatus.value = ''
  try {
    await apiFetch('/api/response-config', {
      method: 'PUT',
      body: JSON.stringify({ mode: mode.value, delayMs: Number(delayMs.value) || 0 }),
    })
    configStatus.value = 'saved'
  } catch {
    configStatus.value = 'error'
  }
}

onMounted(loadConfig)
</script>

<template>
  <div class="settings">
    <section class="card">
      <h2 class="card__title">Response behaviour (Broker &rarr; Mock, /message)</h2>
      <div class="row">
        <label class="field">
          <span class="field__label">Mode</span>
          <select v-model="mode">
            <option value="Ack">Ack</option>
            <option value="Nack">Nack</option>
            <option value="HttpError">HttpError</option>
            <option value="Unauthorized">Unauthorized (invalid API key)</option>
            <option value="Forbidden">Forbidden (missing permissions)</option>
          </select>
        </label>
        <label class="field">
          <span class="field__label">Delay (ms)</span>
          <input v-model.number="delayMs" type="number" min="0" />
        </label>
      </div>
      <div class="toolbar">
        <button class="btn btn--primary" @click="saveConfig">Save</button>
        <span class="status-text">{{ configStatus }}</span>
      </div>
    </section>
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.card*, .field*, .toolbar, .btn*) come from src/assets/styles —
// only this view's own layout lives here.
.settings {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.status-text {
  font-size: 0.85rem;
  opacity: 0.75;
  margin-left: 0.6rem;
}
</style>
