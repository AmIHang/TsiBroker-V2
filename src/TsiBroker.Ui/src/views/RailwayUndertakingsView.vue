<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch } from '@/lib/api'

interface IsbAssignment {
  infrastructureOperatorId: string
  allowedMessageTypesEvuToBroker: string[]
  allowedMessageTypesBrokerToEvu: string[]
}

interface RailwayUndertaking {
  id: string
  name: string
  ricsCodes: string[]
  systemUrl: string
  apiKeyEvuToBroker: string
  apiKeyBrokerToEvu: string
  infrastructureOperatorAssignments: IsbAssignment[]
  isActive: boolean
}

interface InfrastructureOperator {
  id: string
  name: string
  ricsCode: string
  systemUrl: string
  isActive: boolean
}

const router = useRouter()

const undertakings = ref<RailwayUndertaking[]>([])
const isLoading = ref(true)
const error = ref('')

const infrastructureOperators = ref<InfrastructureOperator[]>([])

const showForm = ref(false)
const dialogRef = ref<HTMLDialogElement | null>(null)
const isSubmitting = ref(false)
const formError = ref('')
const name = ref('')
const ricsCodes = ref<string[]>([''])
const systemUrl = ref('')

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

async function loadInfrastructureOperators() {
  try {
    const response = await apiFetch('/api/infrastructure-operators')
    infrastructureOperators.value = await response.json()
  } catch {
    // ISB-Liste ist nur für die Anzeige relevant; ein Fehler hier blockiert die EVU-Ansicht nicht.
  }
}

function isbFor(infrastructureOperatorId: string) {
  return infrastructureOperators.value.find((isb) => isb.id === infrastructureOperatorId)
}

function assignedIsbNames(u: RailwayUndertaking) {
  return u.infrastructureOperatorAssignments
    .map((a) => isbFor(a.infrastructureOperatorId)?.name)
    .filter((name): name is string => name !== undefined)
    .join(', ')
}

function openCreateForm() {
  name.value = ''
  ricsCodes.value = ['']
  systemUrl.value = ''
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
    const body = JSON.stringify({
      name: name.value,
      ricsCodes: cleanedRicsCodes,
      systemUrl: systemUrl.value,
      infrastructureOperatorAssignments: [],
    })
    const response = await apiFetch('/api/railway-undertakings', { method: 'POST', body })
    const created: RailwayUndertaking = await response.json()
    showForm.value = false
    router.push({ name: 'railway-undertaking-edit', params: { id: created.id } })
  } catch {
    formError.value = 'Eisenbahnverkehrsunternehmen konnte nicht angelegt werden.'
  } finally {
    isSubmitting.value = false
  }
}

function goToEdit(u: RailwayUndertaking) {
  router.push({ name: 'railway-undertaking-edit', params: { id: u.id } })
}

onMounted(() => {
  loadUndertakings()
  loadInfrastructureOperators()
})
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
          <h2 class="modal__title">Neues Eisenbahnverkehrsunternehmen</h2>
          <button type="button" class="modal__close" aria-label="Schließen" @click="showForm = false">
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
            </svg>
          </button>
        </div>

        <h3 class="modal__section-title">Stammdaten</h3>

        <label class="field">
          <span class="field__label">Name</span>
          <input v-model="name" type="text" required />
        </label>

        <div class="field">
          <span class="field__label">RicsCodes</span>
          <div class="card__toolbar">
            <button type="button" class="btn btn--primary" @click="addRicsCodeField">+ RicsCode hinzufügen</button>
          </div>
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
        </div>

        <label class="field">
          <span class="field__label">SystemUrl</span>
          <input v-model="systemUrl" type="url" required />
        </label>

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
          <th>ISBs</th>
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
          @dblclick="goToEdit(u)"
        >
          <td>{{ u.name }}</td>
          <td>{{ u.ricsCodes.join(', ') }}</td>
          <td>{{ u.systemUrl }}</td>
          <td>{{ assignedIsbNames(u) }}</td>
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

.card__toolbar {
  display: flex;
  justify-content: flex-start;
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
