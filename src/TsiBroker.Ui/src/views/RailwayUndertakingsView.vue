<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
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
const { t } = useI18n()

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
    error.value = t('railwayUndertakings.loadError')
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
    formError.value = t('railwayUndertakings.createError')
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
    <div class="toolbar">
      <button type="button" class="btn btn--primary" @click="openCreateForm">
        {{ t('railwayUndertakings.new') }}
      </button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <dialog ref="dialogRef" class="modal" @close="showForm = false" @cancel="showForm = false">
      <form class="modal__form modal__form--lg" @submit.prevent="onSubmit">
        <div class="modal__header">
          <h2 class="modal__title">{{ t('railwayUndertakings.createTitle') }}</h2>
          <button type="button" class="modal__close" :aria-label="t('common.close')" @click="showForm = false">
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
            </svg>
          </button>
        </div>

        <h3 class="modal__section-title">{{ t('common.masterData') }}</h3>

        <label class="field">
          <span class="field__label">{{ t('common.name') }}</span>
          <input v-model="name" type="text" required />
        </label>

        <div class="field">
          <span class="field__label">{{ t('common.ricsCodes') }}</span>
          <div class="toolbar">
            <button type="button" class="btn btn--primary" @click="addRicsCodeField">{{ t('common.addRicsCode') }}</button>
          </div>
          <div v-for="(code, index) in ricsCodes" :key="index" class="rics-row">
            <input v-model="ricsCodes[index]" type="text" required />
            <button
              type="button"
              class="icon-btn-header icon-btn-header--danger"
              :disabled="ricsCodes.length === 1"
              :aria-label="t('common.remove')"
              :title="t('common.remove')"
              @click="removeRicsCodeField(index)"
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
          </div>
        </div>

        <label class="field">
          <span class="field__label">{{ t('common.systemUrl') }}</span>
          <input v-model="systemUrl" type="url" required />
        </label>

        <p v-if="formError" class="error">{{ formError }}</p>

        <div class="modal__actions">
          <button type="button" class="btn" @click="showForm = false">{{ t('common.cancel') }}</button>
          <button type="submit" class="btn btn--primary" :disabled="isSubmitting">{{ t('common.save') }}</button>
        </div>
      </form>
    </dialog>

    <p v-if="isLoading" class="empty-state">{{ t('common.loading') }}</p>
    <p v-else-if="undertakings.length === 0" class="empty-state">{{ t('railwayUndertakings.empty') }}</p>

    <table v-else class="data-table">
      <thead>
        <tr>
          <th>{{ t('common.name') }}</th>
          <th>{{ t('common.ricsCodes') }}</th>
          <th>{{ t('common.systemUrl') }}</th>
          <th>{{ t('railwayUndertakings.columns.isbs') }}</th>
          <th>{{ t('common.status') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="u in undertakings"
          :key="u.id"
          class="data-table__row"
          :class="{ 'data-table__row--inactive': !u.isActive }"
          :title="t('common.doubleClickToEdit')"
          @dblclick="goToEdit(u)"
        >
          <td>{{ u.name }}</td>
          <td>{{ u.ricsCodes.join(', ') }}</td>
          <td>{{ u.systemUrl }}</td>
          <td>{{ assignedIsbNames(u) }}</td>
          <td>
            <span class="status" :class="u.isActive ? 'status--active' : 'status--inactive'">
              {{ u.isActive ? t('common.active') : t('common.inactive') }}
            </span>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.btn, .field, .modal*, .icon-btn-header*,
// .data-table*, .status*, .hint*, .error, .empty-state, .toolbar, .rics-row)
// come from src/assets/styles — only this view's own layout lives here.
.undertakings {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}
</style>
