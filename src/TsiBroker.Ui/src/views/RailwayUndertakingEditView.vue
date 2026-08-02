<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
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

const showApiKeyEvuToBroker = ref(false)
const showApiKeyBrokerToEvu = ref(false)

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
      loadError.value = 'Eisenbahnverkehrsunternehmen wurde nicht gefunden.'
      return
    }

    undertaking.value = found
    resetLocalState(found)
    hasTopbarOverride.value = true
  } catch {
    loadError.value = 'Eisenbahnverkehrsunternehmen konnte nicht geladen werden.'
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
    saveError.value = 'Änderungen konnten nicht gespeichert werden.'
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

async function deleteUndertaking() {
  if (!undertaking.value || !confirm(`"${undertaking.value.name}" wirklich löschen?`)) {
    return
  }

  saveError.value = ''
  try {
    await apiFetch(`/api/railway-undertakings/${undertaking.value.id}`, { method: 'DELETE' })
    router.push({ name: 'railway-undertakings' })
  } catch {
    saveError.value = 'Eisenbahnverkehrsunternehmen konnte nicht gelöscht werden.'
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
    dialogError.value = 'Bitte einen Infrastrukturbetreiber auswählen.'
    return
  }
  if (!editingAssignmentId.value && assignedIsbIds.value.has(dialogIsbId.value)) {
    dialogError.value = 'Dieser Infrastrukturbetreiber ist bereits verknüpft.'
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
  if (!confirm('Verknüpfung wirklich löschen?')) {
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
    <p v-if="isLoading" class="empty-state">Lädt…</p>
    <p v-else-if="loadError" class="error">{{ loadError }}</p>

    <template v-else-if="undertaking">
      <Teleport to="#topbar-custom-title">
        <div class="topbar-title-block">
          <h1 class="topbar__title">{{ undertaking.name }}</h1>
          <RouterLink class="topbar-breadcrumb" :to="{ name: 'railway-undertakings' }">
            &larr; Eisenbahnverkehrsunternehmen
          </RouterLink>
        </div>
      </Teleport>

      <Teleport to="#topbar-actions">
        <button
          v-if="!undertaking.isActive"
          type="button"
          class="icon-btn-header icon-btn-header--danger icon-btn-header--lg"
          aria-label="Löschen"
          title="Löschen"
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
          :aria-label="pendingIsActive ? 'Sperren' : 'Entsperren'"
          :title="pendingIsActive ? 'Sperren' : 'Entsperren'"
          @click="toggleActive"
        >
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path d="M12 3v7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
            <path d="M7 5.5a7 7 0 1 0 10 0" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
        </button>
        <button type="submit" form="stammdaten-form" class="btn btn--primary" :disabled="isSaving">
          Speichern
        </button>
      </Teleport>

      <p v-if="saveError" class="error">{{ saveError }}</p>
      <p v-if="pendingIsActive !== undertaking.isActive" class="hint hint--pending">
        Status-Änderung ({{ pendingIsActive ? 'Entsperrt' : 'Gesperrt' }}) wird beim Speichern übernommen.
      </p>

      <div class="columns">
        <div class="column column--main">
          <section class="card">
            <h3 class="card__title">Stammdaten</h3>
            <form id="stammdaten-form" @submit.prevent="saveAll">
              <label class="field">
                <span class="field__label">Name</span>
                <input v-model="name" type="text" required />
              </label>

              <div class="field">
                <span class="field__label">RicsCodes</span>
                <div class="toolbar">
                  <button type="button" class="btn btn--primary" @click="addRicsCodeField">+ RicsCode hinzufügen</button>
                </div>
                <div v-for="(code, index) in ricsCodes" :key="index" class="rics-row">
                  <input v-model="ricsCodes[index]" type="text" required />
                  <button
                    type="button"
                    class="icon-btn-header icon-btn-header--danger"
                    :disabled="ricsCodes.length === 1"
                    aria-label="Entfernen"
                    title="Entfernen"
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
                <span class="field__label">SystemUrl</span>
                <input v-model="systemUrl" type="url" required />
              </label>
            </form>
          </section>

          <section class="card">
            <h3 class="card__title">API-Keys</h3>

            <div class="field">
              <span class="field__label">API-Key (EVU → Broker)</span>
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
                    :aria-label="showApiKeyEvuToBroker ? 'Key verbergen' : 'Key anzeigen'"
                    :title="showApiKeyEvuToBroker ? 'Key verbergen' : 'Key anzeigen'"
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
                  aria-label="Neu generieren"
                  title="Neu generieren"
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
                Wird beim Speichern übernommen. Der bisherige Key wird dann ungültig.
              </p>
            </div>

            <div class="field">
              <span class="field__label">API-Key (Broker → EVU)</span>
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
                    :aria-label="showApiKeyBrokerToEvu ? 'Key verbergen' : 'Key anzeigen'"
                    :title="showApiKeyBrokerToEvu ? 'Key verbergen' : 'Key anzeigen'"
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
                  aria-label="Neu generieren"
                  title="Neu generieren"
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
                Wird beim Speichern übernommen. Der bisherige Key wird dann ungültig.
              </p>
            </div>
          </section>
        </div>

        <div class="column column--assignments">
          <section class="card">
            <h3 class="card__title">Verknüpfte Infrastrukturbetreiber</h3>

            <div class="toolbar">
              <button type="button" class="btn btn--primary" :disabled="isSaving" @click="openAddAssignmentDialog">
                + Verknüpfung hinzufügen
              </button>
            </div>

            <p v-if="pendingAssignments.length === 0" class="empty-state">
              Noch keine Verknüpfungen hinterlegt.
            </p>

            <table v-else class="data-table data-table--compact">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>RicsCode</th>
                  <th>Nachrichten Senden</th>
                  <th>Nachrichten Empfangen</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="assignment in pendingAssignments"
                  :key="assignment.infrastructureOperatorId"
                  class="data-table__row"
                  :class="{ 'data-table__row--inactive': !assignment.isActive }"
                  title="Doppelklick zum Bearbeiten"
                  @dblclick="openEditAssignmentDialog(assignment)"
                >
                  <td>{{ isbFor(assignment.infrastructureOperatorId)?.name ?? '(unbekannter Infrastrukturbetreiber)' }}</td>
                  <td>{{ isbFor(assignment.infrastructureOperatorId)?.ricsCode }}</td>
                  <td>{{ assignment.allowedMessageTypesEvuToBroker.join(', ') }}</td>
                  <td>{{ assignment.allowedMessageTypesBrokerToEvu.join(', ') }}</td>
                  <td class="data-table__actions">
                    <button
                      type="button"
                      class="icon-btn-header"
                      :class="assignment.isActive ? 'icon-btn-header--deactivate' : 'icon-btn-header--activate'"
                      :aria-label="assignment.isActive ? 'Deaktivieren' : 'Aktivieren'"
                      :title="assignment.isActive ? 'Deaktivieren' : 'Aktivieren'"
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
                      aria-label="Löschen"
                      title="Löschen"
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
            <h2 class="modal__title">{{ editingAssignmentId ? 'Verknüpfung bearbeiten' : 'Verknüpfung hinzufügen' }}</h2>
            <button type="button" class="modal__close" aria-label="Schließen" @click="showAssignmentDialog = false">
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
              </svg>
            </button>
          </div>

          <p v-if="!editingAssignmentId && infrastructureOperators.length === 0" class="hint">
            Noch keine Infrastrukturbetreiber im System angelegt.
          </p>
          <p v-else-if="!editingAssignmentId && selectableIsbs.length === 0" class="hint">
            Alle im System gepflegten Infrastrukturbetreiber sind bereits verknüpft.
          </p>
          <template v-else>
            <label class="field">
              <span class="field__label">Infrastrukturbetreiber</span>
              <select v-model="dialogIsbId" :disabled="!!editingAssignmentId">
                <option value="">-- ISB wählen --</option>
                <option v-for="isb in dialogSelectableIsbs" :key="isb.id" :value="isb.id">{{ isbLabel(isb) }}</option>
              </select>
            </label>

            <label class="field">
              <span class="field__label">Nachrichten Senden (EVU &rarr; Broker)</span>
              <input v-model="dialogAllowedEvuToBroker" type="text" placeholder="z.B. 3003 oder *" />
            </label>

            <label class="field">
              <span class="field__label">Nachrichten Empfangen (Broker &rarr; EVU)</span>
              <input v-model="dialogAllowedBrokerToEvu" type="text" placeholder="z.B. 3003 oder *" />
            </label>
            <p class="hint">Mehrere Werte kommagetrennt. <code>*</code> bedeutet: alle erlaubt.</p>

            <p v-if="dialogError" class="error">{{ dialogError }}</p>

            <div class="modal__actions">
              <button type="button" class="btn" @click="showAssignmentDialog = false">Abbrechen</button>
              <button type="submit" class="btn btn--primary" :disabled="isSaving">
                {{ editingAssignmentId ? 'Speichern' : 'Verknüpfung hinzufügen' }}
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
