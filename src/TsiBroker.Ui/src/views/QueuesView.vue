<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { apiFetch } from '@/lib/api'

interface QueueStatus {
  queueName: string
  infrastructureOperatorName: string
  isActive: boolean
  messageCount: number | null
}

interface EvuQueueStatus {
  queueName: string
  railwayUndertakingId: string
  railwayUndertakingName: string
  isActive: boolean
  isPaused: boolean
  pauseReason: string | null
  messageCount: number | null
  errorMessageCount: number | null
}

const { t } = useI18n()

const queues = ref<QueueStatus[]>([])
const evuQueues = ref<EvuQueueStatus[]>([])
const isLoading = ref(true)
const error = ref('')
const resumingIds = ref<Set<string>>(new Set())

async function loadQueues() {
  isLoading.value = true
  error.value = ''
  try {
    const [imResponse, evuResponse] = await Promise.all([
      apiFetch('/api/queues'),
      apiFetch('/api/queues/evu'),
    ])
    queues.value = await imResponse.json()
    evuQueues.value = await evuResponse.json()
  } catch {
    error.value = t('queues.loadError')
  } finally {
    isLoading.value = false
  }
}

async function resumeQueue(railwayUndertakingId: string) {
  resumingIds.value.add(railwayUndertakingId)
  try {
    await apiFetch(`/api/railway-undertakings/${railwayUndertakingId}/resume-queue`, { method: 'POST' })
    await loadQueues()
  } catch {
    error.value = t('queues.resumeError')
  } finally {
    resumingIds.value.delete(railwayUndertakingId)
  }
}

onMounted(loadQueues)
</script>

<template>
  <div class="queues">
    <p v-if="error" class="error">{{ error }}</p>

    <p v-if="isLoading" class="empty-state">{{ t('common.loading') }}</p>

    <template v-else>
      <section>
        <h2 class="queues__section-title">{{ t('queues.sections.im') }}</h2>
        <p v-if="queues.length === 0" class="empty-state">{{ t('queues.empty') }}</p>
        <table v-else class="data-table">
          <thead>
            <tr>
              <th>{{ t('queues.columns.infrastructureOperator') }}</th>
              <th>{{ t('queues.columns.queueName') }}</th>
              <th>{{ t('queues.columns.messageCount') }}</th>
              <th>{{ t('common.status') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="q in queues"
              :key="q.queueName"
              class="data-table__row"
              :class="{ 'data-table__row--inactive': !q.isActive }"
            >
              <td>{{ q.infrastructureOperatorName }}</td>
              <td>{{ q.queueName }}</td>
              <td>{{ q.messageCount ?? '–' }}</td>
              <td>
                <span class="status" :class="q.isActive ? 'status--active' : 'status--inactive'">
                  {{ q.isActive ? t('common.active') : t('common.inactive') }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </section>

      <section>
        <h2 class="queues__section-title">{{ t('queues.sections.evu') }}</h2>
        <p v-if="evuQueues.length === 0" class="empty-state">{{ t('queues.empty') }}</p>
        <table v-else class="data-table">
          <thead>
            <tr>
              <th>{{ t('queues.columns.railwayUndertaking') }}</th>
              <th>{{ t('queues.columns.queueName') }}</th>
              <th>{{ t('queues.columns.messageCount') }}</th>
              <th>{{ t('queues.columns.errorMessageCount') }}</th>
              <th>{{ t('common.status') }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="q in evuQueues"
              :key="q.queueName"
              class="data-table__row"
              :class="{ 'data-table__row--inactive': !q.isActive && !q.isPaused }"
            >
              <td>{{ q.railwayUndertakingName }}</td>
              <td>{{ q.queueName }}</td>
              <td>{{ q.messageCount ?? '–' }}</td>
              <td>{{ q.errorMessageCount ?? '–' }}</td>
              <td>
                <span
                  class="status"
                  :class="q.isPaused ? 'status--paused' : q.isActive ? 'status--active' : 'status--inactive'"
                  :title="q.isPaused ? (q.pauseReason ?? undefined) : undefined"
                >
                  {{ q.isPaused ? t('queues.status.paused') : q.isActive ? t('common.active') : t('common.inactive') }}
                </span>
              </td>
              <td>
                <button
                  v-if="q.isPaused"
                  type="button"
                  class="btn btn--primary btn--small"
                  :disabled="resumingIds.has(q.railwayUndertakingId)"
                  @click="resumeQueue(q.railwayUndertakingId)"
                >
                  {{ t('queues.actions.resume') }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </section>
    </template>
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.data-table*, .status*, .btn*, .error, .empty-state)
// come from src/assets/styles — only this view's own layout lives here.
.queues {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.queues__section-title {
  margin: 0 0 0.75rem;
  font-size: 1rem;
  font-weight: 600;
}

// Rows here aren't clickable (unlike other list views), so the shared
// .data-table__row pointer cursor doesn't apply.
.data-table__row {
  cursor: default;
}
</style>
