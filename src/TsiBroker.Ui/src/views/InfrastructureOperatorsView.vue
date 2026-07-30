<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { apiFetch } from '@/lib/api'

interface InfrastructureOperator {
  id: string
  name: string
  ricsCode: string
  systemUrl: string
  isActive: boolean
}

const operators = ref<InfrastructureOperator[]>([])
const isLoading = ref(true)
const error = ref('')

const showForm = ref(false)
const dialogRef = ref<HTMLDialogElement | null>(null)
const isSubmitting = ref(false)
const formError = ref('')
const name = ref('')
const ricsCode = ref('')
const systemUrl = ref('')
const editingId = ref<string | null>(null)
const pendingIsActive = ref(false)

const editingOperator = computed(() => operators.value.find((o) => o.id === editingId.value) ?? null)

watch(showForm, (value) => {
  if (value) {
    dialogRef.value?.showModal()
  } else {
    dialogRef.value?.close()
  }
})

async function loadOperators() {
  isLoading.value = true
  error.value = ''
  try {
    const response = await apiFetch('/api/infrastructure-operators')
    operators.value = await response.json()
  } catch {
    error.value = 'Infrastrukturbetreiber konnten nicht geladen werden.'
  } finally {
    isLoading.value = false
  }
}

function openCreateForm() {
  editingId.value = null
  name.value = ''
  ricsCode.value = ''
  systemUrl.value = ''
  pendingIsActive.value = true
  formError.value = ''
  showForm.value = true
}

function openEditForm(op: InfrastructureOperator) {
  editingId.value = op.id
  name.value = op.name
  ricsCode.value = op.ricsCode
  systemUrl.value = op.systemUrl
  pendingIsActive.value = op.isActive
  formError.value = ''
  showForm.value = true
}

async function onSubmit() {
  formError.value = ''
  isSubmitting.value = true
  try {
    const body = JSON.stringify({ name: name.value, ricsCode: ricsCode.value, systemUrl: systemUrl.value })

    if (editingId.value) {
      const response = await apiFetch(`/api/infrastructure-operators/${editingId.value}`, { method: 'PUT', body })
      let updated = await response.json()

      if (pendingIsActive.value !== updated.isActive) {
        await apiFetch(`/api/infrastructure-operators/${editingId.value}/status`, {
          method: 'PATCH',
          body: JSON.stringify({ isActive: pendingIsActive.value }),
        })
        updated = { ...updated, isActive: pendingIsActive.value }
      }

      const index = operators.value.findIndex((o) => o.id === editingId.value)
      if (index !== -1) {
        operators.value[index] = updated
      }
    } else {
      const response = await apiFetch('/api/infrastructure-operators', { method: 'POST', body })
      const created = await response.json()
      operators.value.push(created)
    }

    showForm.value = false
  } catch {
    formError.value = editingId.value
      ? 'Infrastrukturbetreiber konnte nicht gespeichert werden.'
      : 'Infrastrukturbetreiber konnte nicht angelegt werden.'
  } finally {
    isSubmitting.value = false
  }
}

function toggleActive() {
  pendingIsActive.value = !pendingIsActive.value
}

async function deleteOperator(op: InfrastructureOperator) {
  if (!confirm(`"${op.name}" wirklich löschen?`)) {
    return
  }

  try {
    await apiFetch(`/api/infrastructure-operators/${op.id}`, { method: 'DELETE' })
    operators.value = operators.value.filter((o) => o.id !== op.id)
    showForm.value = false
  } catch {
    formError.value = 'Infrastrukturbetreiber konnte nicht gelöscht werden.'
  }
}

onMounted(loadOperators)
</script>

<template>
  <div class="operators">
    <div class="operators__toolbar">
      <button type="button" class="btn btn--primary" @click="openCreateForm">
        + Neuer Infrastrukturbetreiber
      </button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <dialog ref="dialogRef" class="modal" @close="showForm = false" @cancel="showForm = false">
      <form class="modal__form" @submit.prevent="onSubmit">
        <div class="modal__header">
          <h2 class="modal__title">{{ editingId ? 'Infrastrukturbetreiber bearbeiten' : 'Neuer Infrastrukturbetreiber' }}</h2>
          <div class="modal__header-actions">
            <template v-if="editingOperator">
              <button
                v-if="!editingOperator.isActive"
                type="button"
                class="icon-btn-header icon-btn-header--danger"
                aria-label="Löschen"
                title="Löschen"
                @click="deleteOperator(editingOperator)"
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
                :class="pendingIsActive ? 'icon-btn-header--deactivate' : 'icon-btn-header--activate'"
                :aria-label="pendingIsActive ? 'Deaktivieren' : 'Aktivieren'"
                :title="pendingIsActive ? 'Deaktivieren' : 'Aktivieren'"
                @click="toggleActive"
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

        <label class="field">
          <span class="field__label">Name</span>
          <input v-model="name" type="text" required />
        </label>
        <label class="field">
          <span class="field__label">RicsCode</span>
          <input v-model="ricsCode" type="text" required />
        </label>
        <label class="field">
          <span class="field__label">SystemUrl</span>
          <input v-model="systemUrl" type="url" required />
        </label>

        <p v-if="editingOperator && pendingIsActive !== editingOperator.isActive" class="hint hint--pending">
          Status-Änderung ({{ pendingIsActive ? 'Aktiv' : 'Inaktiv' }}) wird beim Speichern übernommen.
        </p>

        <p v-if="formError" class="error">{{ formError }}</p>

        <div class="modal__actions">
          <button type="button" class="btn" @click="showForm = false">Abbrechen</button>
          <button type="submit" class="btn btn--primary" :disabled="isSubmitting">Speichern</button>
        </div>
      </form>
    </dialog>

    <p v-if="isLoading" class="empty-state">Lädt…</p>
    <p v-else-if="operators.length === 0" class="empty-state">Keine Infrastrukturbetreiber vorhanden.</p>

    <table v-else class="operator-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>RicsCode</th>
          <th>SystemUrl</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="op in operators"
          :key="op.id"
          class="operator-table__row"
          :class="{ 'operator-table__row--inactive': !op.isActive }"
          title="Doppelklick zum Bearbeiten"
          @dblclick="openEditForm(op)"
        >
          <td>{{ op.name }}</td>
          <td>{{ op.ricsCode }}</td>
          <td>{{ op.systemUrl }}</td>
          <td>
            <span class="status" :class="op.isActive ? 'status--active' : 'status--inactive'">
              {{ op.isActive ? 'Aktiv' : 'Inaktiv' }}
            </span>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.operators {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.operators__toolbar {
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
  width: min(420px, 90vw);
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

.modal__actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-top: 0.25rem;
}

.hint {
  font-size: 0.82rem;
  opacity: 0.7;
}

.hint--pending {
  opacity: 1;
  color: #b8860b;
}

.operator-table {
  width: 100%;
  border-collapse: collapse;
}

.operator-table th,
.operator-table td {
  padding: 0.75rem;
  text-align: left;
  border-bottom: 1px solid var(--color-border);
  font-size: 0.9rem;
}

.operator-table th {
  font-weight: 600;
  opacity: 0.75;
}

.operator-table__row {
  cursor: pointer;
  transition: background-color 0.15s;
}

.operator-table__row:hover {
  background: var(--color-background-soft);
}

.operator-table__row--inactive {
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
