<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { apiFetch } from '@/lib/api'

interface RailwayUndertaking {
  id: string
  name: string
  ricsCodes: string[]
  systemUrl: string
  apiKeyEvuToBroker: string
  apiKeyBrokerToEvu: string
  isActive: boolean
}

const undertakings = ref<RailwayUndertaking[]>([])
const isLoading = ref(true)
const error = ref('')

const showForm = ref(false)
const dialogRef = ref<HTMLDialogElement | null>(null)
const isSubmitting = ref(false)
const formError = ref('')
const name = ref('')
const ricsCodes = ref<string[]>([''])
const systemUrl = ref('')
const apiKeyEvuToBroker = ref('')
const apiKeyBrokerToEvu = ref('')
const showApiKeyEvuToBroker = ref(false)
const showApiKeyBrokerToEvu = ref(false)
const isRegeneratingEvuToBroker = ref(false)
const isRegeneratingBrokerToEvu = ref(false)
const apiKeysExpanded = ref(false)
const editingId = ref<string | null>(null)

const editingUndertaking = computed(() => undertakings.value.find((u) => u.id === editingId.value) ?? null)

watch(showForm, (value) => {
  if (value) {
    dialogRef.value?.showModal()
  } else {
    dialogRef.value?.close()
  }
})

async function loadUndertakings() {
  isLoading.value = true
  error.value = ''
  try {
    const response = await apiFetch('/api/railway-undertakings')
    undertakings.value = await response.json()
  } catch {
    error.value = 'Eisenbahnverkehrsunternehmen konnten nicht geladen werden.'
  } finally {
    isLoading.value = false
  }
}

function openCreateForm() {
  editingId.value = null
  name.value = ''
  ricsCodes.value = ['']
  systemUrl.value = ''
  apiKeyEvuToBroker.value = ''
  apiKeyBrokerToEvu.value = ''
  showApiKeyEvuToBroker.value = false
  showApiKeyBrokerToEvu.value = false
  apiKeysExpanded.value = false
  formError.value = ''
  showForm.value = true
}

function openEditForm(u: RailwayUndertaking) {
  editingId.value = u.id
  name.value = u.name
  ricsCodes.value = u.ricsCodes.length > 0 ? [...u.ricsCodes] : ['']
  systemUrl.value = u.systemUrl
  apiKeyEvuToBroker.value = u.apiKeyEvuToBroker
  apiKeyBrokerToEvu.value = u.apiKeyBrokerToEvu
  showApiKeyEvuToBroker.value = false
  showApiKeyBrokerToEvu.value = false
  apiKeysExpanded.value = false
  formError.value = ''
  showForm.value = true
}

function addRicsCodeField() {
  ricsCodes.value.push('')
}

function removeRicsCodeField(index: number) {
  ricsCodes.value.splice(index, 1)
  if (ricsCodes.value.length === 0) {
    ricsCodes.value.push('')
  }
}

async function onSubmit() {
  formError.value = ''
  isSubmitting.value = true
  try {
    const cleanedRicsCodes = ricsCodes.value.map((code) => code.trim()).filter((code) => code.length > 0)

    if (editingId.value) {
      const body = JSON.stringify({
        name: name.value,
        ricsCodes: cleanedRicsCodes,
        systemUrl: systemUrl.value,
        apiKeyEvuToBroker: apiKeyEvuToBroker.value,
        apiKeyBrokerToEvu: apiKeyBrokerToEvu.value,
      })
      const response = await apiFetch(`/api/railway-undertakings/${editingId.value}`, { method: 'PUT', body })
      const updated = await response.json()
      const index = undertakings.value.findIndex((u) => u.id === editingId.value)
      if (index !== -1) {
        undertakings.value[index] = updated
      }
    } else {
      const body = JSON.stringify({ name: name.value, ricsCodes: cleanedRicsCodes, systemUrl: systemUrl.value })
      const response = await apiFetch('/api/railway-undertakings', { method: 'POST', body })
      const created = await response.json()
      undertakings.value.push(created)
    }

    showForm.value = false
  } catch {
    formError.value = editingId.value
      ? 'Eisenbahnverkehrsunternehmen konnte nicht gespeichert werden.'
      : 'Eisenbahnverkehrsunternehmen konnte nicht angelegt werden.'
  } finally {
    isSubmitting.value = false
  }
}

function updateUndertakingInList(updated: RailwayUndertaking) {
  const index = undertakings.value.findIndex((u) => u.id === updated.id)
  if (index !== -1) {
    undertakings.value[index] = updated
  }
}

async function regenerateApiKeyEvuToBroker() {
  if (!editingId.value || !confirm('Neuen API-Key (EVU → Broker) generieren? Der bisherige Key wird ungültig.')) {
    return
  }

  isRegeneratingEvuToBroker.value = true
  try {
    const response = await apiFetch(`/api/railway-undertakings/${editingId.value}/api-key-evu-to-broker/regenerate`, {
      method: 'POST',
    })
    const updated: RailwayUndertaking = await response.json()
    apiKeyEvuToBroker.value = updated.apiKeyEvuToBroker
    showApiKeyEvuToBroker.value = true
    updateUndertakingInList(updated)
  } catch {
    formError.value = 'API-Key konnte nicht neu generiert werden.'
  } finally {
    isRegeneratingEvuToBroker.value = false
  }
}

async function regenerateApiKeyBrokerToEvu() {
  if (!editingId.value || !confirm('Neuen API-Key (Broker → EVU) generieren? Der bisherige Key wird ungültig.')) {
    return
  }

  isRegeneratingBrokerToEvu.value = true
  try {
    const response = await apiFetch(`/api/railway-undertakings/${editingId.value}/api-key-broker-to-evu/regenerate`, {
      method: 'POST',
    })
    const updated: RailwayUndertaking = await response.json()
    apiKeyBrokerToEvu.value = updated.apiKeyBrokerToEvu
    showApiKeyBrokerToEvu.value = true
    updateUndertakingInList(updated)
  } catch {
    formError.value = 'API-Key konnte nicht neu generiert werden.'
  } finally {
    isRegeneratingBrokerToEvu.value = false
  }
}

async function toggleActive(u: RailwayUndertaking) {
  const nextActive = !u.isActive
  try {
    await apiFetch(`/api/railway-undertakings/${u.id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ isActive: nextActive }),
    })
    u.isActive = nextActive
  } catch {
    formError.value = 'Status konnte nicht geändert werden.'
  }
}

async function deleteUndertaking(u: RailwayUndertaking) {
  if (!confirm(`"${u.name}" wirklich löschen?`)) {
    return
  }

  try {
    await apiFetch(`/api/railway-undertakings/${u.id}`, { method: 'DELETE' })
    undertakings.value = undertakings.value.filter((x) => x.id !== u.id)
    showForm.value = false
  } catch {
    formError.value = 'Eisenbahnverkehrsunternehmen konnte nicht gelöscht werden.'
  }
}

onMounted(loadUndertakings)
</script>

<template>
  <div class="undertakings">
    <div class="undertakings__toolbar">
      <button type="button" class="btn btn--primary" @click="openCreateForm">
        + Neues Eisenbahnverkehrsunternehmen
      </button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <dialog ref="dialogRef" class="modal" @close="showForm = false" @cancel="showForm = false">
      <form class="modal__form" @submit.prevent="onSubmit">
        <div class="modal__header">
          <h2 class="modal__title">
            {{ editingId ? 'Eisenbahnverkehrsunternehmen bearbeiten' : 'Neues Eisenbahnverkehrsunternehmen' }}
          </h2>
          <div class="modal__header-actions">
            <template v-if="editingUndertaking">
              <button
                v-if="!editingUndertaking.isActive"
                type="button"
                class="icon-btn-header icon-btn-header--danger"
                aria-label="Löschen"
                title="Löschen"
                @click="deleteUndertaking(editingUndertaking)"
              >
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path
                    d="M4 7h16M9 7V5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2m-9 0 1 13a1 1 0 0 0 1 1h8a1 1 0 0 0 1-1l1-13"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  />
                  <path d="M10 11v6M14 11v6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                </svg>
              </button>
              <button
                type="button"
                class="icon-btn-header"
                :class="editingUndertaking.isActive ? 'icon-btn-header--deactivate' : 'icon-btn-header--activate'"
                :aria-label="editingUndertaking.isActive ? 'Deaktivieren' : 'Aktivieren'"
                :title="editingUndertaking.isActive ? 'Deaktivieren' : 'Aktivieren'"
                @click="toggleActive(editingUndertaking)"
              >
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path d="M12 3v7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                  <path d="M7 5.5a7 7 0 1 0 10 0" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                </svg>
              </button>
            </template>
            <button type="button" class="modal__close" aria-label="Schließen" @click="showForm = false">
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
              </svg>
            </button>
          </div>
        </div>

        <h3 class="modal__section-title">Stammdaten</h3>

        <label class="field">
          <span class="field__label">Name</span>
          <input v-model="name" type="text" required />
        </label>

        <div class="field">
          <span class="field__label">RicsCodes</span>
          <div v-for="(code, index) in ricsCodes" :key="index" class="rics-row">
            <input v-model="ricsCodes[index]" type="text" required />
            <button
              type="button"
              class="btn btn--small btn--danger"
              :disabled="ricsCodes.length === 1"
              @click="removeRicsCodeField(index)"
            >
              Entfernen
            </button>
          </div>
          <button type="button" class="btn btn--small" @click="addRicsCodeField">+ RicsCode hinzufügen</button>
        </div>

        <label class="field">
          <span class="field__label">SystemUrl</span>
          <input v-model="systemUrl" type="url" required />
        </label>

        <template v-if="editingId">
          <button
            type="button"
            class="modal__section-toggle"
            :aria-expanded="apiKeysExpanded"
            @click="apiKeysExpanded = !apiKeysExpanded"
          >
            <span>API-Keys</span>
            <svg
              class="modal__section-toggle-icon"
              :class="{ 'modal__section-toggle-icon--open': apiKeysExpanded }"
              viewBox="0 0 24 24"
              fill="none"
              aria-hidden="true"
            >
              <path d="M6 9l6 6 6-6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
          </button>
        </template>

        <template v-if="editingId && apiKeysExpanded">
          <div class="field">
            <span class="field__label">API-Key (EVU → Broker)</span>
            <div class="key-row">
              <input
                v-model="apiKeyEvuToBroker"
                :type="showApiKeyEvuToBroker ? 'text' : 'password'"
                autocomplete="new-password"
              />
              <button
                type="button"
                class="icon-btn-inline"
                :aria-label="showApiKeyEvuToBroker ? 'Key verbergen' : 'Key anzeigen'"
                :title="showApiKeyEvuToBroker ? 'Key verbergen' : 'Key anzeigen'"
                @click="showApiKeyEvuToBroker = !showApiKeyEvuToBroker"
              >
                <svg v-if="showApiKeyEvuToBroker" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path d="M3 3l18 18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                  <path
                    d="M10.6 5.2A10.9 10.9 0 0 1 12 5c5 0 9 4.2 10 7-.4 1.1-1.2 2.4-2.3 3.6M6.6 6.6C4.5 8 3 10 2 12c1 2.8 5 7 10 7 1.3 0 2.5-.3 3.6-.7"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  />
                  <path
                    d="M9.9 10.1a2.5 2.5 0 0 0 3.9 3.1"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                  />
                </svg>
                <svg v-else viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path
                    d="M2 12c1-2.8 5-7 10-7s9 4.2 10 7c-1 2.8-5 7-10 7s-9-4.2-10-7Z"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linejoin="round"
                  />
                  <circle cx="12" cy="12" r="2.7" stroke="currentColor" stroke-width="1.8" />
                </svg>
              </button>
              <button
                type="button"
                class="icon-btn-inline"
                aria-label="Neuen Key generieren"
                title="Neuen Key generieren"
                :disabled="isRegeneratingEvuToBroker"
                @click="regenerateApiKeyEvuToBroker"
              >
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path
                    d="M20 12a8 8 0 1 1-2.34-5.66M20 4v5h-5"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  />
                </svg>
              </button>
            </div>
          </div>

          <div class="field">
            <span class="field__label">API-Key (Broker → EVU)</span>
            <div class="key-row">
              <input
                v-model="apiKeyBrokerToEvu"
                :type="showApiKeyBrokerToEvu ? 'text' : 'password'"
                autocomplete="new-password"
              />
              <button
                type="button"
                class="icon-btn-inline"
                :aria-label="showApiKeyBrokerToEvu ? 'Key verbergen' : 'Key anzeigen'"
                :title="showApiKeyBrokerToEvu ? 'Key verbergen' : 'Key anzeigen'"
                @click="showApiKeyBrokerToEvu = !showApiKeyBrokerToEvu"
              >
                <svg v-if="showApiKeyBrokerToEvu" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path d="M3 3l18 18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                  <path
                    d="M10.6 5.2A10.9 10.9 0 0 1 12 5c5 0 9 4.2 10 7-.4 1.1-1.2 2.4-2.3 3.6M6.6 6.6C4.5 8 3 10 2 12c1 2.8 5 7 10 7 1.3 0 2.5-.3 3.6-.7"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  />
                  <path
                    d="M9.9 10.1a2.5 2.5 0 0 0 3.9 3.1"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                  />
                </svg>
                <svg v-else viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path
                    d="M2 12c1-2.8 5-7 10-7s9 4.2 10 7c-1 2.8-5 7-10 7s-9-4.2-10-7Z"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linejoin="round"
                  />
                  <circle cx="12" cy="12" r="2.7" stroke="currentColor" stroke-width="1.8" />
                </svg>
              </button>
              <button
                type="button"
                class="icon-btn-inline"
                aria-label="Neuen Key generieren"
                title="Neuen Key generieren"
                :disabled="isRegeneratingBrokerToEvu"
                @click="regenerateApiKeyBrokerToEvu"
              >
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path
                    d="M20 12a8 8 0 1 1-2.34-5.66M20 4v5h-5"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  />
                </svg>
              </button>
            </div>
          </div>
        </template>

        <p v-if="formError" class="error">{{ formError }}</p>

        <div class="modal__actions">
          <button type="button" class="btn" @click="showForm = false">Abbrechen</button>
          <button type="submit" class="btn btn--primary" :disabled="isSubmitting">Speichern</button>
        </div>
      </form>
    </dialog>

    <p v-if="isLoading" class="empty-state">Lädt…</p>
    <p v-else-if="undertakings.length === 0" class="empty-state">Keine Eisenbahnverkehrsunternehmen vorhanden.</p>

    <table v-else class="undertaking-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>RicsCodes</th>
          <th>SystemUrl</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="u in undertakings"
          :key="u.id"
          class="undertaking-table__row"
          :class="{ 'undertaking-table__row--inactive': !u.isActive }"
          title="Doppelklick zum Bearbeiten"
          @dblclick="openEditForm(u)"
        >
          <td>{{ u.name }}</td>
          <td>{{ u.ricsCodes.join(', ') }}</td>
          <td>{{ u.systemUrl }}</td>
          <td>
            <span class="status" :class="u.isActive ? 'status--active' : 'status--inactive'">
              {{ u.isActive ? 'Aktiv' : 'Inaktiv' }}
            </span>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.undertakings {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.undertakings__toolbar {
  display: flex;
  justify-content: flex-start;
}

.empty-state {
  text-align: center;
}

.error {
  font-size: 0.85rem;
  color: #d33;
}

.btn {
  padding: 0.6rem 1.1rem;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-background);
  color: var(--color-text);
  font-size: 0.9rem;
  cursor: pointer;
  transition: background-color 0.15s, border-color 0.15s;
}

.btn:hover {
  border-color: var(--color-border-hover);
}

.btn--primary {
  border-color: var(--color-primary);
  background: var(--color-primary);
  color: var(--color-on-primary);
}

.btn--primary:hover {
  background: var(--color-primary-hover);
}

.btn--primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn--small {
  padding: 0.35rem 0.7rem;
  font-size: 0.82rem;
}

.btn--small:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.btn--danger {
  color: #d33;
  border-color: #d33;
}

.btn--danger:hover {
  background: color-mix(in srgb, #d33 10%, transparent);
}

.modal {
  margin: auto;
  padding: 0;
  border: none;
  border-radius: 14px;
  background: var(--color-background);
  box-shadow: 0 20px 45px rgba(30, 20, 45, 0.18);
}

.modal::backdrop {
  background: rgba(20, 15, 30, 0.45);
}

.modal__form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  width: min(460px, 90vw);
  max-height: 85vh;
  overflow-y: auto;
  padding: 1.75rem;
}

.modal__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.modal__header-actions {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-shrink: 0;
}

.icon-btn-header {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 28px;
  height: 28px;
  padding: 0;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-background);
  color: var(--color-text);
  cursor: pointer;
  transition: background-color 0.15s, border-color 0.15s, color 0.15s, opacity 0.15s;
}

.icon-btn-header:hover {
  border-color: var(--color-border-hover);
}

.icon-btn-header svg {
  width: 16px;
  height: 16px;
}

.icon-btn-header--activate {
  color: #2e9e5b;
  border-color: #2e9e5b;
}

.icon-btn-header--activate:hover {
  background: color-mix(in srgb, #2e9e5b 10%, transparent);
}

.icon-btn-header--deactivate {
  color: #d33;
  border-color: #d33;
}

.icon-btn-header--deactivate:hover {
  background: color-mix(in srgb, #d33 10%, transparent);
}

.icon-btn-header--danger:hover {
  border-color: #d33;
  color: #d33;
  background: color-mix(in srgb, #d33 10%, transparent);
}

.modal__title {
  font-size: 1.15rem;
  font-weight: 600;
  color: var(--color-heading);
}

.modal__close {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 28px;
  height: 28px;
  padding: 0;
  border: none;
  border-radius: 6px;
  background: none;
  color: var(--color-text);
  opacity: 0.6;
  cursor: pointer;
  transition: background-color 0.15s, opacity 0.15s;
}

.modal__close:hover {
  opacity: 1;
  background: var(--color-background-soft);
}

.modal__close svg {
  width: 18px;
  height: 18px;
}

.modal__section-title {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--color-heading);
  opacity: 0.75;
  margin-top: 0.25rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--color-border);
}

.modal__section-toggle {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  width: 100%;
  padding: 0;
  border: none;
  border-top: 1px solid var(--color-border);
  background: none;
  cursor: pointer;
  font: inherit;
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--color-heading);
  opacity: 0.75;
  margin-top: 0.25rem;
  padding-top: 0.75rem;
}

.modal__section-toggle:hover {
  opacity: 1;
}

.modal__section-toggle-icon {
  width: 16px;
  height: 16px;
  flex-shrink: 0;
  transition: transform 0.15s ease;
}

.modal__section-toggle-icon--open {
  transform: rotate(180deg);
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.field__label {
  font-size: 0.8rem;
  color: var(--color-text);
  opacity: 0.75;
}

.field input {
  padding: 0.6rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-background);
  color: var(--color-text);
  font-size: 0.9rem;
}

.field input:focus {
  outline: none;
  border-color: var(--color-primary);
}

.rics-row {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.rics-row input {
  flex: 1;
  padding: 0.6rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-background);
  color: var(--color-text);
  font-size: 0.9rem;
}

.rics-row input:focus {
  outline: none;
  border-color: var(--color-primary);
}

.key-row {
  display: flex;
  gap: 0.4rem;
  align-items: center;
}

.key-row input {
  flex: 1;
  padding: 0.6rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-background);
  color: var(--color-text);
  font-size: 0.9rem;
  font-family: monospace;
}

.key-row input:focus {
  outline: none;
  border-color: var(--color-primary);
}

.icon-btn-inline {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 36px;
  height: 36px;
  padding: 0;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-background);
  color: var(--color-text);
  cursor: pointer;
  transition: background-color 0.15s, border-color 0.15s;
}

.icon-btn-inline:hover {
  border-color: var(--color-border-hover);
}

.icon-btn-inline:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.icon-btn-inline svg {
  width: 18px;
  height: 18px;
}

.modal__actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-top: 0.25rem;
}

.undertaking-table {
  width: 100%;
  border-collapse: collapse;
}

.undertaking-table th,
.undertaking-table td {
  padding: 0.75rem;
  text-align: left;
  border-bottom: 1px solid var(--color-border);
  font-size: 0.9rem;
}

.undertaking-table th {
  font-weight: 600;
  opacity: 0.75;
}

.undertaking-table__row {
  cursor: pointer;
  transition: background-color 0.15s;
}

.undertaking-table__row:hover {
  background: var(--color-background-soft);
}

.undertaking-table__row--inactive {
  opacity: 0.55;
}

.status {
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
  font-size: 0.78rem;
  font-weight: 600;
}

.status--active {
  background: color-mix(in srgb, #2e9e5b 15%, transparent);
  color: #2e9e5b;
}

.status--inactive {
  background: color-mix(in srgb, #999 15%, transparent);
  color: #777;
}
</style>
