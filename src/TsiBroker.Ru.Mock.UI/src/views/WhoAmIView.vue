<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiFetch } from '@/lib/api'

interface WhoAmIDefaults {
  url: string
  apiKey: string
}

interface WhoAmIResult {
  success: boolean
  responseXml: string | null
  error: string | null
}

const url = ref('')
const apiKey = ref('')
const result = ref<WhoAmIResult | null>(null)
const isChecking = ref(false)

async function loadDefaults() {
  const res = await apiFetch('/api/whoami/defaults')
  const defaults: WhoAmIDefaults = await res.json()
  url.value = defaults.url
  apiKey.value = defaults.apiKey
}

async function check() {
  isChecking.value = true
  try {
    const res = await apiFetch('/api/whoami', {
      method: 'POST',
      body: JSON.stringify({ url: url.value || null, apiKey: apiKey.value || null }),
    })
    result.value = await res.json()
  } finally {
    isChecking.value = false
  }
}

onMounted(loadDefaults)
</script>

<template>
  <div class="whoami">
    <section class="card">
      <h2 class="card__title">Check config (Mock &rarr; Broker, /whoami)</h2>
      <div class="row">
        <label class="field">
          <span class="field__label">URL</span>
          <input v-model="url" />
        </label>
        <label class="field">
          <span class="field__label">API Key (X-Api-Key)</span>
          <input v-model="apiKey" />
        </label>
      </div>
      <div class="toolbar">
        <button class="btn btn--primary" :disabled="isChecking" @click="check">Check</button>
      </div>
      <div v-if="result">
        <span class="badge" :class="result.success ? 'badge--success' : 'badge--danger'">
          {{ result.success ? 'OK' : 'Error' }}
        </span>
        <span v-if="result.error" class="status-text">{{ result.error }}</span>
        <pre v-if="result.responseXml">{{ result.responseXml }}</pre>
      </div>
    </section>
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.card*, .field*, .toolbar, .btn*, .badge*) come from
// src/assets/styles — only this view's own layout lives here.
.whoami {
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
