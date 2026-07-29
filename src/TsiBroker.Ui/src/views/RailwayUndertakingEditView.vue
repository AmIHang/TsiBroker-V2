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
const stammdatenError = ref('')
const isSavingStammdaten = ref(false)

const showApiKeyEvuToBroker = ref(false)
const showApiKeyBrokerToEvu = ref(false)
const isRegeneratingEvuToBroker = ref(false)
const isRegeneratingBrokerToEvu = ref(false)
const apiKeyError = ref('')

const statusError = ref('')
const assignmentError = ref('')
const isSavingAssignment = ref(false)

const showAssignmentDialog = ref(false)
const assignmentDialogRef = ref<HTMLDialogElement | null>(null)
const editingAssignmentId = ref<string | null>(null)
const dialogIsbId = ref('')
const dialogAllowedEvuToBroker = ref('')
const dialogAllowedBrokerToEvu = ref('')
const dialogError = ref('')

const assignedIsbIds = computed(
  () => new Set((undertaking.value?.infrastructureOperatorAssignments ?? []).map((a) => a.infrastructureOperatorId)),
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

function resetStammdatenFields(u: RailwayUndertaking) {
  name.value = u.name
  ricsCodes.value = u.ricsCodes.length > 0 ? [...u.ricsCodes] : ['']
  systemUrl.value = u.systemUrl
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
    resetStammdatenFields(found)
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

function buildPutBody(overrides: Partial<{
  name: string
  ricsCodes: string[]
  systemUrl: string
  apiKeyEvuToBroker: string
  apiKeyBrokerToEvu: string
  infrastructureOperatorAssignments: IsbAssignment[]
}>) {
  const current = undertaking.value!
  return JSON.stringify({
    name: current.name,
    ricsCodes: current.ricsCodes,
    systemUrl: current.systemUrl,
    apiKeyEvuToBroker: current.apiKeyEvuToBroker,
    apiKeyBrokerToEvu: current.apiKeyBrokerToEvu,
    infrastructureOperatorAssignments: current.infrastructureOperatorAssignments,
    ...overrides,
  })
}

async function putAssignments(nextAssignments: IsbAssignment[]) {
  const body = buildPutBody({ infrastructureOperatorAssignments: nextAssignments })
  const response = await apiFetch(`/api/railway-undertakings/${undertaking.value!.id}`, { method: 'PUT', body })
  undertaking.value = await response.json()
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

async function saveStammdaten() {
  if (!undertaking.value) {
    return
  }

  stammdatenError.value = ''
  isSavingStammdaten.value = true
  try {
    const cleanedRicsCodes = ricsCodes.value.map((code) => code.trim()).filter((code) => code.length > 0)
    const body = buildPutBody({ name: name.value, ricsCodes: cleanedRicsCodes, systemUrl: systemUrl.value })
    const response = await apiFetch(`/api/railway-undertakings/${undertaking.value.id}`, { method: 'PUT', body })
    const updated: RailwayUndertaking = await response.json()
    undertaking.value = updated
    resetStammdatenFields(updated)
  } catch {
    stammdatenError.value = 'Stammdaten konnten nicht gespeichert werden.'
  } finally {
    isSavingStammdaten.value = false
  }
}

async function regenerateApiKeyEvuToBroker() {
  if (!undertaking.value || !confirm('Neuen API-Key (EVU → Broker) generieren? Der bisherige Key wird ungültig.')) {
    return
  }

  apiKeyError.value = ''
  isRegeneratingEvuToBroker.value = true
  try {
    const response = await apiFetch(`/api/railway-undertakings/${undertaking.value.id}/api-key-evu-to-broker/regenerate`, {
      method: 'POST',
    })
    undertaking.value = await response.json()
    showApiKeyEvuToBroker.value = true
  } catch {
    apiKeyError.value = 'API-Key konnte nicht neu generiert werden.'
  } finally {
    isRegeneratingEvuToBroker.value = false
  }
}

async function regenerateApiKeyBrokerToEvu() {
  if (!undertaking.value || !confirm('Neuen API-Key (Broker → EVU) generieren? Der bisherige Key wird ungültig.')) {
    return
  }

  apiKeyError.value = ''
  isRegeneratingBrokerToEvu.value = true
  try {
    const response = await apiFetch(`/api/railway-undertakings/${undertaking.value.id}/api-key-broker-to-evu/regenerate`, {
      method: 'POST',
    })
    undertaking.value = await response.json()
    showApiKeyBrokerToEvu.value = true
  } catch {
    apiKeyError.value = 'API-Key konnte nicht neu generiert werden.'
  } finally {
    isRegeneratingBrokerToEvu.value = false
  }
}

async function toggleActive() {
  if (!undertaking.value) {
    return
  }

  statusError.value = ''
  const nextActive = !undertaking.value.isActive
  try {
    await apiFetch(`/api/railway-undertakings/${undertaking.value.id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ isActive: nextActive }),
    })
    undertaking.value.isActive = nextActive
  } catch {
    statusError.value = 'Status konnte nicht geändert werden.'
  }
}

async function deleteUndertaking() {
  if (!undertaking.value || !confirm(`"${undertaking.value.name}" wirklich löschen?`)) {
    return
  }

  statusError.value = ''
  try {
    await apiFetch(`/api/railway-undertakings/${undertaking.value.id}`, { method: 'DELETE' })
    router.push({ name: 'railway-undertakings' })
  } catch {
    statusError.value = 'Eisenbahnverkehrsunternehmen konnte nicht gelöscht werden.'
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

async function saveAssignmentDialog() {
  if (!undertaking.value) {
    return
  }

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

  const nextAssignments = editingAssignmentId.value
    ? undertaking.value.infrastructureOperatorAssignments.map((a) =>
        a.infrastructureOperatorId === editingAssignmentId.value
          ? { ...a, allowedMessageTypesEvuToBroker: allowedEvuToBroker, allowedMessageTypesBrokerToEvu: allowedBrokerToEvu }
          : a,
      )
    : [
        ...undertaking.value.infrastructureOperatorAssignments,
        {
          infrastructureOperatorId: dialogIsbId.value,
          allowedMessageTypesEvuToBroker: allowedEvuToBroker,
          allowedMessageTypesBrokerToEvu: allowedBrokerToEvu,
          isActive: true,
        },
      ]

  isSavingAssignment.value = true
  try {
    await putAssignments(nextAssignments)
    showAssignmentDialog.value = false
  } catch {
    dialogError.value = 'Verknüpfung konnte nicht gespeichert werden.'
  } finally {
    isSavingAssignment.value = false
  }
}

async function toggleAssignmentActive(assignment: IsbAssignment) {
  if (!undertaking.value) {
    return
  }

  assignmentError.value = ''
  const nextAssignments = undertaking.value.infrastructureOperatorAssignments.map((a) =>
    a.infrastructureOperatorId === assignment.infrastructureOperatorId ? { ...a, isActive: !a.isActive } : a,
  )

  isSavingAssignment.value = true
  try {
    await putAssignments(nextAssignments)
  } catch {
    assignmentError.value = 'Status der Verknüpfung konnte nicht geändert werden.'
  } finally {
    isSavingAssignment.value = false
  }
}

async function deleteAssignment(infrastructureOperatorId: string) {
  if (!undertaking.value || !confirm('Verknüpfung wirklich löschen?')) {
    return
  }

  assignmentError.value = ''
  const nextAssignments = undertaking.value.infrastructureOperatorAssignments.filter(
    (a) => a.infrastructureOperatorId !== infrastructureOperatorId,
  )

  isSavingAssignment.value = true
  try {
    await putAssignments(nextAssignments)
  } catch {
    assignmentError.value = 'Verknüpfung konnte nicht gelöscht werden.'
  } finally {
    isSavingAssignment.value = false
  }
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
          class="icon-btn-header icon-btn-header--danger topbar-icon-btn"
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
          class="icon-btn-header topbar-icon-btn"
          :class="undertaking.isActive ? 'icon-btn-header--deactivate' : 'icon-btn-header--activate'"
          :aria-label="undertaking.isActive ? 'Sperren' : 'Entsperren'"
          :title="undertaking.isActive ? 'Sperren' : 'Entsperren'"
          @click="toggleActive"
        >
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path d="M12 3v7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
            <path d="M7 5.5a7 7 0 1 0 10 0" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
        </button>
        <button type="submit" form="stammdaten-form" class="btn btn--primary" :disabled="isSavingStammdaten">
          Speichern
        </button>
      </Teleport>

      <p v-if="statusError" class="error">{{ statusError }}</p>

      <section class="card">
        <h3 class="card__title">Stammdaten</h3>
        <form id="stammdaten-form" @submit.prevent="saveStammdaten">
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

          <p v-if="stammdatenError" class="error">{{ stammdatenError }}</p>
        </form>
      </section>

      <section class="card">
        <h3 class="card__title">API-Keys</h3>

        <div class="field">
          <span class="field__label">API-Key (EVU → Broker)</span>
          <div class="key-row">
            <input :value="undertaking.apiKeyEvuToBroker" :type="showApiKeyEvuToBroker ? 'text' : 'password'" readonly />
            <button
              type="button"
              class="icon-btn-inline"
              :aria-label="showApiKeyEvuToBroker ? 'Key verbergen' : 'Key anzeigen'"
              :title="showApiKeyEvuToBroker ? 'Key verbergen' : 'Key anzeigen'"
              @click="showApiKeyEvuToBroker = !showApiKeyEvuToBroker"
            >
              {{ showApiKeyEvuToBroker ? 'Verbergen' : 'Anzeigen' }}
            </button>
            <button
              type="button"
              class="btn btn--small"
              :disabled="isRegeneratingEvuToBroker"
              @click="regenerateApiKeyEvuToBroker"
            >
              Neu generieren
            </button>
          </div>
        </div>

        <div class="field">
          <span class="field__label">API-Key (Broker → EVU)</span>
          <div class="key-row">
            <input :value="undertaking.apiKeyBrokerToEvu" :type="showApiKeyBrokerToEvu ? 'text' : 'password'" readonly />
            <button
              type="button"
              class="icon-btn-inline"
              :aria-label="showApiKeyBrokerToEvu ? 'Key verbergen' : 'Key anzeigen'"
              :title="showApiKeyBrokerToEvu ? 'Key verbergen' : 'Key anzeigen'"
              @click="showApiKeyBrokerToEvu = !showApiKeyBrokerToEvu"
            >
              {{ showApiKeyBrokerToEvu ? 'Verbergen' : 'Anzeigen' }}
            </button>
            <button
              type="button"
              class="btn btn--small"
              :disabled="isRegeneratingBrokerToEvu"
              @click="regenerateApiKeyBrokerToEvu"
            >
              Neu generieren
            </button>
          </div>
        </div>

        <p v-if="apiKeyError" class="error">{{ apiKeyError }}</p>
      </section>

      <section class="card">
        <h3 class="card__title">Verknüpfte Infrastrukturbetreiber</h3>

        <div class="card__toolbar">
          <button type="button" class="btn btn--primary" @click="openAddAssignmentDialog">
            + Verknüpfung hinzufügen
          </button>
        </div>

        <p v-if="undertaking.infrastructureOperatorAssignments.length === 0" class="empty-state">
          Noch keine Verknüpfungen hinterlegt.
        </p>

        <table v-else class="assignment-table">
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
              v-for="assignment in undertaking.infrastructureOperatorAssignments"
              :key="assignment.infrastructureOperatorId"
              class="assignment-table__row"
              :class="{ 'assignment-table__row--inactive': !assignment.isActive }"
              title="Doppelklick zum Bearbeiten"
              @dblclick="openEditAssignmentDialog(assignment)"
            >
              <td>{{ isbFor(assignment.infrastructureOperatorId)?.name ?? '(unbekannter Infrastrukturbetreiber)' }}</td>
              <td>{{ isbFor(assignment.infrastructureOperatorId)?.ricsCode }}</td>
              <td>{{ assignment.allowedMessageTypesEvuToBroker.join(', ') }}</td>
              <td>{{ assignment.allowedMessageTypesBrokerToEvu.join(', ') }}</td>
              <td class="assignment-table__actions">
                <button
                  type="button"
                  class="icon-btn-header"
                  :class="assignment.isActive ? 'icon-btn-header--deactivate' : 'icon-btn-header--activate'"
                  :aria-label="assignment.isActive ? 'Deaktivieren' : 'Aktivieren'"
                  :title="assignment.isActive ? 'Deaktivieren' : 'Aktivieren'"
                  :disabled="isSavingAssignment"
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
                  :disabled="assignment.isActive || isSavingAssignment"
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

        <p v-if="assignmentError" class="error">{{ assignmentError }}</p>
      </section>

      <dialog
        ref="assignmentDialogRef"
        class="modal"
        @close="showAssignmentDialog = false"
        @cancel="showAssignmentDialog = false"
      >
        <form class="modal__form" @submit.prevent="saveAssignmentDialog">
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
              <button type="submit" class="btn btn--primary" :disabled="isSavingAssignment">
                {{ editingAssignmentId ? 'Speichern' : 'Verknüpfung hinzufügen' }}
              </button>
            </div>
          </template>
        </form>
      </dialog>
    </template>
  </div>
</template>

<style scoped>
.edit-page {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  max-width: 56rem;
}

.empty-state {
  text-align: center;
}

.error {
  font-size: 0.85rem;
  color: #d33;
}

.topbar-title-block {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.topbar__title {
  margin: 0;
  font-size: 1.15rem;
  font-weight: 600;
  color: var(--color-topbar-text);
}

.topbar-breadcrumb {
  font-size: 0.8rem;
  color: var(--color-topbar-text);
  opacity: 0.7;
  text-decoration: none;
  background: transparent;
}

.topbar-breadcrumb:hover {
  text-decoration: underline;
  background: transparent;
}

.icon-btn-header {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 28px;
  height: 28px;
  padding: 0;
  border: 1px solid rgba(120, 120, 120, 0.4);
  border-radius: 6px;
  background: transparent;
  color: inherit;
  cursor: pointer;
  transition: background-color 0.15s, border-color 0.15s, color 0.15s, opacity 0.15s;
}

.icon-btn-header:hover {
  border-color: rgba(120, 120, 120, 0.65);
}

.icon-btn-header:disabled {
  opacity: 0.35;
  cursor: not-allowed;
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

.icon-btn-header--danger {
  color: #d33;
  border-color: #d33;
}

.icon-btn-header--danger:hover {
  background: color-mix(in srgb, #d33 10%, transparent);
}

.topbar-icon-btn {
  width: 37px;
  height: 37px;
  border-radius: 8px;
}

.topbar-icon-btn svg {
  width: 18px;
  height: 18px;
}

.card {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.5rem;
  border: 1px solid var(--color-border);
  border-radius: 14px;
  background: var(--color-background);
}

.card form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.card__toolbar {
  display: flex;
  justify-content: flex-start;
}

.card__title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--color-heading);
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

.field input,
.field select {
  padding: 0.6rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-background);
  color: var(--color-text);
  font-size: 0.9rem;
}

.field input:focus,
.field select:focus {
  outline: none;
  border-color: var(--color-primary);
}

.field select:disabled {
  opacity: 0.6;
  cursor: not-allowed;
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
  gap: 0.5rem;
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

.icon-btn-inline {
  padding: 0.35rem 0.7rem;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-background);
  color: var(--color-text);
  font-size: 0.82rem;
  cursor: pointer;
  white-space: nowrap;
}

.icon-btn-inline:hover {
  border-color: var(--color-border-hover);
}

.hint {
  font-size: 0.82rem;
  opacity: 0.7;
}

.assignment-table {
  width: 100%;
  border-collapse: collapse;
}

.assignment-table th,
.assignment-table td {
  padding: 0.6rem 0.7rem;
  text-align: left;
  border-bottom: 1px solid var(--color-border);
  font-size: 0.85rem;
}

.assignment-table th {
  font-weight: 600;
  opacity: 0.75;
}

.assignment-table__row {
  cursor: pointer;
  transition: background-color 0.15s;
}

.assignment-table__row:hover {
  background: var(--color-background-soft);
}

.assignment-table__row--inactive {
  opacity: 0.55;
}

.assignment-table__actions {
  display: flex;
  gap: 0.4rem;
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

.modal__actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-top: 0.25rem;
}
</style>
