<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiFetch } from '@/lib/api'
import { hasTopbarOverride } from '@/composables/useTopbarOverride'

interface IsbAssignment {
  infrastructureOperatorId: string
  allowedMessageTypesEvuToBroker: string[]
  allowedMessageTypesBrokerToEvu: string[]
  isActive: boolean
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

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const undertaking = ref<RailwayUndertaking | null>(null)
const infrastructureOperators = ref<InfrastructureOperator[]>([])
const isLoading = ref(true)
const loadError = ref('')

const name = ref('')
const ricsCodes = ref<string[]>([''])
const systemUrl = ref('')
const pendingIsActive = ref(false)
const pendingAssignments = ref<IsbAssignment[]>([])
const pendingApiKeyEvuToBroker = ref<string | null>(null)
const pendingApiKeyBrokerToEvu = ref<string | null>(null)

const saveError = ref('')
const isSaving = ref(false)

const isTriggeringConfigUpdate = ref(false)
const configUpdateResult = ref<'success' | 'error' | null>(null)

const showApiKeyEvuToBroker = ref(false)
const showApiKeyBrokerToEvu = ref(false)
const copiedApiKeyEvuToBroker = ref(false)
const copiedApiKeyBrokerToEvu = ref(false)

const showAssignmentDialog = ref(false)
const assignmentDialogRef = ref<HTMLDialogElement | null>(null)
const editingAssignmentId = ref<string | null>(null)
const dialogIsbId = ref('')
const dialogAllowedEvuToBroker = ref('')
const dialogAllowedBrokerToEvu = ref('')
const dialogError = ref('')

const assignedIsbIds = computed(
  () => new Set(pendingAssignments.value.map((a) => a.infrastructureOperatorId)),
)
const selectableIsbs = computed(() =>
  infrastructureOperators.value.filter((isb) => !assignedIsbIds.value.has(isb.id)),
)
const dialogSelectableIsbs = computed(() => {
  if (editingAssignmentId.value) {
    const current = isbFor(editingAssignmentId.value)
    return current ? [current] : []
  }
  return selectableIsbs.value
})

watch(showAssignmentDialog, (value) => {
  if (value) {
    assignmentDialogRef.value?.showModal()
  } else {
    assignmentDialogRef.value?.close()
  }
})

function resetLocalState(u: RailwayUndertaking) {
  name.value = u.name
  ricsCodes.value = u.ricsCodes.length > 0 ? [...u.ricsCodes] : ['']
  systemUrl.value = u.systemUrl
  pendingIsActive.value = u.isActive
  pendingAssignments.value = u.infrastructureOperatorAssignments.map((a) => ({ ...a }))
  pendingApiKeyEvuToBroker.value = null
  pendingApiKeyBrokerToEvu.value = null
}

async function load() {
  isLoading.value = true
  loadError.value = ''
  try {
    const [undertakingsResponse, isbResponse] = await Promise.all([
      apiFetch('/api/railway-undertakings'),
      apiFetch('/api/infrastructure-operators'),
    ])
    const undertakings: RailwayUndertaking[] = await undertakingsResponse.json()
    infrastructureOperators.value = await isbResponse.json()

    const found = undertakings.find((u) => u.id === route.params.id)
    if (!found) {
      loadError.value = t('railwayUndertakingEdit.notFound')
      return
    }

    undertaking.value = found
    resetLocalState(found)
    hasTopbarOverride.value = true
  } catch {
    loadError.value = t('railwayUndertakingEdit.loadError')
  } finally {
    isLoading.value = false
  }
}

function isbLabel(isb: InfrastructureOperator) {
  return `${isb.name} (${isb.ricsCode})`
}

function isbFor(infrastructureOperatorId: string) {
  return infrastructureOperators.value.find((isb) => isb.id === infrastructureOperatorId)
}

function parseMessageTypes(input: string): string[] {
  const seen = new Set<string>()
  const result: string[] = []
  for (const raw of input.split(/[,;\s]+/)) {
    const value = raw.trim()
    if (value.length === 0 || seen.has(value.toLowerCase())) {
      continue
    }
    seen.add(value.toLowerCase())
    result.push(value)
  }
  return result
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

async function saveAll() {
  if (!undertaking.value) {
    return
  }

  saveError.value = ''
  isSaving.value = true
  try {
    const id = undertaking.value.id
    const cleanedRicsCodes = ricsCodes.value.map((code) => code.trim()).filter((code) => code.length > 0)
    const body = JSON.stringify({
      name: name.value,
      ricsCodes: cleanedRicsCodes,
      systemUrl: systemUrl.value,
      apiKeyEvuToBroker: pendingApiKeyEvuToBroker.value ?? undertaking.value.apiKeyEvuToBroker,
      apiKeyBrokerToEvu: pendingApiKeyBrokerToEvu.value ?? undertaking.value.apiKeyBrokerToEvu,
      infrastructureOperatorAssignments: pendingAssignments.value,
    })
    const response = await apiFetch(`/api/railway-undertakings/${id}`, { method: 'PUT', body })
    let updated: RailwayUndertaking = await response.json()

    if (pendingIsActive.value !== updated.isActive) {
      await apiFetch(`/api/railway-undertakings/${id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ isActive: pendingIsActive.value }),
      })
      updated = { ...updated, isActive: pendingIsActive.value }
    }

    undertaking.value = updated
    resetLocalState(updated)
  } catch {
    saveError.value = t('railwayUndertakingEdit.saveError')
  } finally {
    isSaving.value = false
  }
}

function toggleActive() {
  pendingIsActive.value = !pendingIsActive.value
}

async function generateApiKey(): Promise<string> {
  const response = await apiFetch('/api/railway-undertakings/generate-api-key')
  const { apiKey } = await response.json()
  return apiKey
}

async function regenerateEvuToBroker() {
  pendingApiKeyEvuToBroker.value = await generateApiKey()
  showApiKeyEvuToBroker.value = true
}

async function regenerateBrokerToEvu() {
  pendingApiKeyBrokerToEvu.value = await generateApiKey()
  showApiKeyBrokerToEvu.value = true
}

async function copyApiKeyEvuToBroker() {
  const key = pendingApiKeyEvuToBroker.value ?? undertaking.value?.apiKeyEvuToBroker
  if (!key) {
    return
  }
  await navigator.clipboard.writeText(key)
  copiedApiKeyEvuToBroker.value = true
  setTimeout(() => (copiedApiKeyEvuToBroker.value = false), 1500)
}

async function copyApiKeyBrokerToEvu() {
  const key = pendingApiKeyBrokerToEvu.value ?? undertaking.value?.apiKeyBrokerToEvu
  if (!key) {
    return
  }
  await navigator.clipboard.writeText(key)
  copiedApiKeyBrokerToEvu.value = true
  setTimeout(() => (copiedApiKeyBrokerToEvu.value = false), 1500)
}

async function triggerConfigUpdate() {
  if (!undertaking.value) {
    return
  }

  isTriggeringConfigUpdate.value = true
  configUpdateResult.value = null
  try {
    await apiFetch(`/api/railway-undertakings/${undertaking.value.id}/trigger-config-update`, { method: 'POST' })
    configUpdateResult.value = 'success'
  } catch {
    configUpdateResult.value = 'error'
  } finally {
    isTriggeringConfigUpdate.value = false
    setTimeout(() => (configUpdateResult.value = null), 4000)
  }
}

async function deleteUndertaking() {
  if (!undertaking.value || !confirm(t('railwayUndertakingEdit.confirmDelete', { name: undertaking.value.name }))) {
    return
  }

  saveError.value = ''
  try {
    await apiFetch(`/api/railway-undertakings/${undertaking.value.id}`, { method: 'DELETE' })
    router.push({ name: 'railway-undertakings' })
  } catch {
    saveError.value = t('railwayUndertakingEdit.deleteError')
  }
}

function openAddAssignmentDialog() {
  editingAssignmentId.value = null
  dialogIsbId.value = ''
  dialogAllowedEvuToBroker.value = ''
  dialogAllowedBrokerToEvu.value = ''
  dialogError.value = ''
  showAssignmentDialog.value = true
}

function openEditAssignmentDialog(assignment: IsbAssignment) {
  editingAssignmentId.value = assignment.infrastructureOperatorId
  dialogIsbId.value = assignment.infrastructureOperatorId
  dialogAllowedEvuToBroker.value = assignment.allowedMessageTypesEvuToBroker.join(', ')
  dialogAllowedBrokerToEvu.value = assignment.allowedMessageTypesBrokerToEvu.join(', ')
  dialogError.value = ''
  showAssignmentDialog.value = true
}

function saveAssignmentDialog() {
  dialogError.value = ''
  if (!dialogIsbId.value) {
    dialogError.value = t('railwayUndertakingEdit.assignmentDialog.selectIsbError')
    return
  }
  if (!editingAssignmentId.value && assignedIsbIds.value.has(dialogIsbId.value)) {
    dialogError.value = t('railwayUndertakingEdit.assignmentDialog.alreadyLinkedError')
    return
  }

  const allowedEvuToBroker = parseMessageTypes(dialogAllowedEvuToBroker.value)
  const allowedBrokerToEvu = parseMessageTypes(dialogAllowedBrokerToEvu.value)

  pendingAssignments.value = editingAssignmentId.value
    ? pendingAssignments.value.map((a) =>
        a.infrastructureOperatorId === editingAssignmentId.value
          ? { ...a, allowedMessageTypesEvuToBroker: allowedEvuToBroker, allowedMessageTypesBrokerToEvu: allowedBrokerToEvu }
          : a,
      )
    : [
        ...pendingAssignments.value,
        {
          infrastructureOperatorId: dialogIsbId.value,
          allowedMessageTypesEvuToBroker: allowedEvuToBroker,
          allowedMessageTypesBrokerToEvu: allowedBrokerToEvu,
          isActive: true,
        },
      ]

  showAssignmentDialog.value = false
}

function toggleAssignmentActive(assignment: IsbAssignment) {
  pendingAssignments.value = pendingAssignments.value.map((a) =>
    a.infrastructureOperatorId === assignment.infrastructureOperatorId ? { ...a, isActive: !a.isActive } : a,
  )
}

function deleteAssignment(infrastructureOperatorId: string) {
  if (!confirm(t('railwayUndertakingEdit.confirmDeleteAssignment'))) {
    return
  }

  pendingAssignments.value = pendingAssignments.value.filter(
    (a) => a.infrastructureOperatorId !== infrastructureOperatorId,
  )
}

onMounted(load)
onUnmounted(() => {
  hasTopbarOverride.value = false
})
</script>

<template>
  <div class="edit-page">
    <p v-if="isLoading" class="empty-state">{{ t('common.loading') }}</p>
    <p v-else-if="loadError" class="error">{{ loadError }}</p>

    <template v-else-if="undertaking">
      <Teleport to="#topbar-custom-title">
        <div class="topbar-title-block">
          <h1 class="topbar__title">{{ undertaking.name }}</h1>
          <RouterLink class="topbar-breadcrumb" :to="{ name: 'railway-undertakings' }">
            {{ t('railwayUndertakingEdit.breadcrumbBack') }}
          </RouterLink>
        </div>
      </Teleport>

      <Teleport to="#topbar-actions">
        <button
          v-if="!undertaking.isActive"
          type="button"
          class="icon-btn-header icon-btn-header--danger icon-btn-header--lg"
          :aria-label="t('common.delete')"
          :title="t('common.delete')"
          @click="deleteUndertaking"
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
          class="icon-btn-header icon-btn-header--lg"
          :class="pendingIsActive ? 'icon-btn-header--deactivate' : 'icon-btn-header--activate'"
          :aria-label="pendingIsActive ? t('railwayUndertakingEdit.lock') : t('railwayUndertakingEdit.unlock')"
          :title="pendingIsActive ? t('railwayUndertakingEdit.lock') : t('railwayUndertakingEdit.unlock')"
          @click="toggleActive"
        >
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path d="M12 3v7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
            <path d="M7 5.5a7 7 0 1 0 10 0" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
        </button>
        <button type="submit" form="stammdaten-form" class="btn btn--primary" :disabled="isSaving">
          {{ t('common.save') }}
        </button>
      </Teleport>

      <p v-if="saveError" class="error">{{ saveError }}</p>
      <p v-if="pendingIsActive !== undertaking.isActive" class="hint hint--pending">
        {{ t('railwayUndertakingEdit.statusChangeHint', { status: pendingIsActive ? t('railwayUndertakingEdit.statusUnlocked') : t('railwayUndertakingEdit.statusLocked') }) }}
      </p>

      <div class="columns">
        <div class="column column--main">
          <section class="card">
            <h3 class="card__title">{{ t('common.masterData') }}</h3>
            <form id="stammdaten-form" @submit.prevent="saveAll">
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
            </form>

            <div class="field">
              <button
                type="button"
                class="btn"
                :disabled="isTriggeringConfigUpdate"
                @click="triggerConfigUpdate"
              >
                {{ isTriggeringConfigUpdate ? t('railwayUndertakingEdit.triggerConfigUpdatePending') : t('railwayUndertakingEdit.triggerConfigUpdate') }}
              </button>
              <p v-if="configUpdateResult === 'success'" class="hint">
                {{ t('railwayUndertakingEdit.triggerConfigUpdateSuccess') }}
              </p>
              <p v-if="configUpdateResult === 'error'" class="error">
                {{ t('railwayUndertakingEdit.triggerConfigUpdateError') }}
              </p>
            </div>
          </section>

          <section class="card">
            <h3 class="card__title">{{ t('railwayUndertakingEdit.apiKeys') }}</h3>

            <div class="field">
              <span class="field__label">{{ t('railwayUndertakingEdit.apiKeyEvuToBroker') }}</span>
              <div class="key-row">
                <label class="key-input-wrap">
                  <input
                    :value="pendingApiKeyEvuToBroker ?? undertaking.apiKeyEvuToBroker"
                    :type="showApiKeyEvuToBroker ? 'text' : 'password'"
                    readonly
                  />
                  <button
                    type="button"
                    class="toggle-password"
                    :aria-label="showApiKeyEvuToBroker ? t('railwayUndertakingEdit.hideKey') : t('railwayUndertakingEdit.showKey')"
                    :title="showApiKeyEvuToBroker ? t('railwayUndertakingEdit.hideKey') : t('railwayUndertakingEdit.showKey')"
                    @click="showApiKeyEvuToBroker = !showApiKeyEvuToBroker"
                  >
                    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                      <path
                        d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"
                        stroke="currentColor"
                        stroke-width="1.8"
                      />
                      <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="1.8" />
                      <path v-if="!showApiKeyEvuToBroker" d="M4 4 L20 20" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                    </svg>
                  </button>
                </label>
                <button
                  type="button"
                  class="icon-btn-header"
                  :aria-label="copiedApiKeyEvuToBroker ? t('common.copied') : t('common.copy')"
                  :title="copiedApiKeyEvuToBroker ? t('common.copied') : t('common.copy')"
                  @click="copyApiKeyEvuToBroker"
                >
                  <svg v-if="!copiedApiKeyEvuToBroker" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                    <rect x="9" y="9" width="12" height="12" rx="1.5" stroke="currentColor" stroke-width="1.8" />
                    <path d="M6 15H4.5A1.5 1.5 0 0 1 3 13.5v-9A1.5 1.5 0 0 1 4.5 3h9A1.5 1.5 0 0 1 15 4.5V6" stroke="currentColor" stroke-width="1.8" />
                  </svg>
                  <svg v-else viewBox="0 0 24 24" fill="none" aria-hidden="true">
                    <path d="M4 12.5l5 5L20 7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                  </svg>
                </button>
                <button
                  type="button"
                  class="icon-btn-header"
                  :aria-label="t('common.regenerate')"
                  :title="t('common.regenerate')"
                  @click="regenerateEvuToBroker"
                >
                  <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                    <path d="M23 4v6h-6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                    <path d="M1 20v-6h6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                    <path
                      d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"
                      stroke="currentColor"
                      stroke-width="1.8"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                    />
                  </svg>
                </button>
              </div>
              <p v-if="pendingApiKeyEvuToBroker" class="hint hint--pending">
                {{ t('railwayUndertakingEdit.apiKeyPendingHint') }}
              </p>
            </div>

            <div class="field">
              <span class="field__label">{{ t('railwayUndertakingEdit.apiKeyBrokerToEvu') }}</span>
              <div class="key-row">
                <label class="key-input-wrap">
                  <input
                    :value="pendingApiKeyBrokerToEvu ?? undertaking.apiKeyBrokerToEvu"
                    :type="showApiKeyBrokerToEvu ? 'text' : 'password'"
                    readonly
                  />
                  <button
                    type="button"
                    class="toggle-password"
                    :aria-label="showApiKeyBrokerToEvu ? t('railwayUndertakingEdit.hideKey') : t('railwayUndertakingEdit.showKey')"
                    :title="showApiKeyBrokerToEvu ? t('railwayUndertakingEdit.hideKey') : t('railwayUndertakingEdit.showKey')"
                    @click="showApiKeyBrokerToEvu = !showApiKeyBrokerToEvu"
                  >
                    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                      <path
                        d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"
                        stroke="currentColor"
                        stroke-width="1.8"
                      />
                      <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="1.8" />
                      <path v-if="!showApiKeyBrokerToEvu" d="M4 4 L20 20" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                    </svg>
                  </button>
                </label>
                <button
                  type="button"
                  class="icon-btn-header"
                  :aria-label="copiedApiKeyBrokerToEvu ? t('common.copied') : t('common.copy')"
                  :title="copiedApiKeyBrokerToEvu ? t('common.copied') : t('common.copy')"
                  @click="copyApiKeyBrokerToEvu"
                >
                  <svg v-if="!copiedApiKeyBrokerToEvu" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                    <rect x="9" y="9" width="12" height="12" rx="1.5" stroke="currentColor" stroke-width="1.8" />
                    <path d="M6 15H4.5A1.5 1.5 0 0 1 3 13.5v-9A1.5 1.5 0 0 1 4.5 3h9A1.5 1.5 0 0 1 15 4.5V6" stroke="currentColor" stroke-width="1.8" />
                  </svg>
                  <svg v-else viewBox="0 0 24 24" fill="none" aria-hidden="true">
                    <path d="M4 12.5l5 5L20 7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                  </svg>
                </button>
                <button
                  type="button"
                  class="icon-btn-header"
                  :aria-label="t('common.regenerate')"
                  :title="t('common.regenerate')"
                  @click="regenerateBrokerToEvu"
                >
                  <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                    <path d="M23 4v6h-6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                    <path d="M1 20v-6h6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                    <path
                      d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"
                      stroke="currentColor"
                      stroke-width="1.8"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                    />
                  </svg>
                </button>
              </div>
              <p v-if="pendingApiKeyBrokerToEvu" class="hint hint--pending">
                {{ t('railwayUndertakingEdit.apiKeyPendingHint') }}
              </p>
            </div>
          </section>
        </div>

        <div class="column column--assignments">
          <section class="card">
            <h3 class="card__title">{{ t('railwayUndertakingEdit.linkedOperators') }}</h3>

            <div class="toolbar">
              <button type="button" class="btn btn--primary" :disabled="isSaving" @click="openAddAssignmentDialog">
                {{ t('railwayUndertakingEdit.addAssignment') }}
              </button>
            </div>

            <p v-if="pendingAssignments.length === 0" class="empty-state">
              {{ t('railwayUndertakingEdit.noAssignments') }}
            </p>

            <table v-else class="data-table data-table--compact">
              <thead>
                <tr>
                  <th>{{ t('common.name') }}</th>
                  <th>{{ t('common.ricsCode') }}</th>
                  <th>{{ t('railwayUndertakingEdit.columns.messagesSend') }}</th>
                  <th>{{ t('railwayUndertakingEdit.columns.messagesReceive') }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="assignment in pendingAssignments"
                  :key="assignment.infrastructureOperatorId"
                  class="data-table__row"
                  :class="{ 'data-table__row--inactive': !assignment.isActive }"
                  :title="t('common.doubleClickToEdit')"
                  @dblclick="openEditAssignmentDialog(assignment)"
                >
                  <td>{{ isbFor(assignment.infrastructureOperatorId)?.name ?? t('railwayUndertakingEdit.unknownOperator') }}</td>
                  <td>{{ isbFor(assignment.infrastructureOperatorId)?.ricsCode }}</td>
                  <td>{{ assignment.allowedMessageTypesEvuToBroker.join(', ') }}</td>
                  <td>{{ assignment.allowedMessageTypesBrokerToEvu.join(', ') }}</td>
                  <td class="data-table__actions">
                    <button
                      type="button"
                      class="icon-btn-header"
                      :class="assignment.isActive ? 'icon-btn-header--deactivate' : 'icon-btn-header--activate'"
                      :aria-label="assignment.isActive ? t('common.deactivate') : t('common.activate')"
                      :title="assignment.isActive ? t('common.deactivate') : t('common.activate')"
                      :disabled="isSaving"
                      @click.stop="toggleAssignmentActive(assignment)"
                    >
                      <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                        <path d="M12 3v7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                        <path d="M7 5.5a7 7 0 1 0 10 0" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                      </svg>
                    </button>
                    <button
                      type="button"
                      class="icon-btn-header icon-btn-header--danger"
                      :disabled="assignment.isActive || isSaving"
                      :aria-label="t('common.delete')"
                      :title="t('common.delete')"
                      @click.stop="deleteAssignment(assignment.infrastructureOperatorId)"
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
                  </td>
                </tr>
              </tbody>
            </table>
          </section>
        </div>
      </div>

      <dialog
        ref="assignmentDialogRef"
        class="modal"
        @close="showAssignmentDialog = false"
        @cancel="showAssignmentDialog = false"
      >
        <form class="modal__form modal__form--lg" @submit.prevent="saveAssignmentDialog">
          <div class="modal__header">
            <h2 class="modal__title">{{ editingAssignmentId ? t('railwayUndertakingEdit.assignmentDialog.editTitle') : t('railwayUndertakingEdit.assignmentDialog.createTitle') }}</h2>
            <button type="button" class="modal__close" :aria-label="t('common.close')" @click="showAssignmentDialog = false">
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
              </svg>
            </button>
          </div>

          <p v-if="!editingAssignmentId && infrastructureOperators.length === 0" class="hint">
            {{ t('railwayUndertakingEdit.assignmentDialog.noOperatorsYet') }}
          </p>
          <p v-else-if="!editingAssignmentId && selectableIsbs.length === 0" class="hint">
            {{ t('railwayUndertakingEdit.assignmentDialog.allLinked') }}
          </p>
          <template v-else>
            <label class="field">
              <span class="field__label">{{ t('railwayUndertakingEdit.assignmentDialog.selectOperator') }}</span>
              <select v-model="dialogIsbId" :disabled="!!editingAssignmentId">
                <option value="">{{ t('railwayUndertakingEdit.assignmentDialog.selectPlaceholder') }}</option>
                <option v-for="isb in dialogSelectableIsbs" :key="isb.id" :value="isb.id">{{ isbLabel(isb) }}</option>
              </select>
            </label>

            <label class="field">
              <span class="field__label">{{ t('railwayUndertakingEdit.assignmentDialog.messagesSendLabel') }}</span>
              <input v-model="dialogAllowedEvuToBroker" type="text" :placeholder="t('railwayUndertakingEdit.assignmentDialog.messagesPlaceholder')" />
            </label>

            <label class="field">
              <span class="field__label">{{ t('railwayUndertakingEdit.assignmentDialog.messagesReceiveLabel') }}</span>
              <input v-model="dialogAllowedBrokerToEvu" type="text" :placeholder="t('railwayUndertakingEdit.assignmentDialog.messagesPlaceholder')" />
            </label>
            <p class="hint">
              {{ t('railwayUndertakingEdit.assignmentDialog.multipleValuesHintPre') }} <code>*</code>
              {{ t('railwayUndertakingEdit.assignmentDialog.multipleValuesHintPost') }}
            </p>

            <p v-if="dialogError" class="error">{{ dialogError }}</p>

            <div class="modal__actions">
              <button type="button" class="btn" @click="showAssignmentDialog = false">{{ t('common.cancel') }}</button>
              <button type="submit" class="btn btn--primary" :disabled="isSaving">
                {{ editingAssignmentId ? t('common.save') : t('railwayUndertakingEdit.assignmentDialog.save') }}
              </button>
            </div>
          </template>
        </form>
      </dialog>
    </template>
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.btn, .field, .modal*, .icon-btn-header*, .card*,
// .data-table*, .hint*, .error, .empty-state, .toolbar, .rics-row,
// .key-row/.key-input-wrap/.toggle-password) come from src/assets/styles —
// only this view's own layout lives here.
.edit-page {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  max-width: 80rem;
}

.columns {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.column {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  min-width: 0;
}

.column--main,
.column--assignments {
  flex: 1 1 0;
}

@media (min-width: 64rem) {
  .columns {
    flex-direction: row;
    align-items: flex-start;
  }
}

// .topbar-title-block / .topbar__title / .topbar-breadcrumb come from the
// shared styles/topbar.less (used here via Teleport into AppTopbar).
</style>
