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
    <div class="toolbar">
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

    <table v-else class="data-table">
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
          class="data-table__row"
          :class="{ 'data-table__row--inactive': !op.isActive }"
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

<style scoped lang="less">
// Shared building blocks (.btn, .field, .modal*, .icon-btn-header*,
// .data-table*, .status*, .hint*, .error, .empty-state, .toolbar) come from
// src/assets/styles — only this view's own layout lives here.
.operators {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}
</style>
