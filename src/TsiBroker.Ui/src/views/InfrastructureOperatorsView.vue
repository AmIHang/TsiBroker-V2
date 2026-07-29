<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
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
  formError.value = ''
  showForm.value = true
}

function openEditForm(op: InfrastructureOperator) {
  editingId.value = op.id
  name.value = op.name
  ricsCode.value = op.ricsCode
  systemUrl.value = op.systemUrl
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
      const updated = await response.json()
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

async function toggleActive(op: InfrastructureOperator) {
  const nextActive = !op.isActive
  try {
    await apiFetch(`/api/infrastructure-operators/${op.id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ isActive: nextActive }),
    })
    op.isActive = nextActive
  } catch {
    error.value = 'Status konnte nicht geändert werden.'
  }
}

async function deleteOperator(op: InfrastructureOperator) {
  if (!confirm(`"${op.name}" wirklich löschen?`)) {
    return
  }

  try {
    await apiFetch(`/api/infrastructure-operators/${op.id}`, { method: 'DELETE' })
    operators.value = operators.value.filter((o) => o.id !== op.id)
  } catch {
    error.value = 'Infrastrukturbetreiber konnte nicht gelöscht werden.'
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
        <h2 class="modal__title">{{ editingId ? 'Infrastrukturbetreiber bearbeiten' : 'Neuer Infrastrukturbetreiber' }}</h2>

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
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="op in operators" :key="op.id" :class="{ 'operator-table__row--inactive': !op.isActive }">
          <td>{{ op.name }}</td>
          <td>{{ op.ricsCode }}</td>
          <td>{{ op.systemUrl }}</td>
          <td>
            <span class="status" :class="op.isActive ? 'status--active' : 'status--inactive'">
              {{ op.isActive ? 'Aktiv' : 'Inaktiv' }}
            </span>
          </td>
          <td class="operator-table__actions">
            <button type="button" class="btn btn--small" @click="openEditForm(op)">
              Bearbeiten
            </button>
            <button type="button" class="btn btn--small" @click="toggleActive(op)">
              {{ op.isActive ? 'Deaktivieren' : 'Aktivieren' }}
            </button>
            <button type="button" class="btn btn--small btn--danger" @click="deleteOperator(op)">
              Löschen
            </button>
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

.btn--small {
  padding: 0.35rem 0.7rem;
  font-size: 0.82rem;
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
  width: min(420px, 90vw);
  padding: 1.75rem;
}

.modal__title {
  font-size: 1.15rem;
  font-weight: 600;
  color: var(--color-heading);
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

.operator-table__row--inactive {
  opacity: 0.55;
}

.operator-table__actions {
  display: flex;
  gap: 0.5rem;
  justify-content: flex-end;
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
