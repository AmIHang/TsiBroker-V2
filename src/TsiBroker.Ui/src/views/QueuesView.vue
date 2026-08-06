<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { apiFetch } from '@/lib/api'

interface QueueStatus {
  queueName: string
  railwayUndertakingName: string
  isActive: boolean
  messageCount: number | null
}

const { t } = useI18n()

const queues = ref<QueueStatus[]>([])
const isLoading = ref(true)
const error = ref('')

async function loadQueues() {
  isLoading.value = true
  error.value = ''
  try {
    const response = await apiFetch('/api/queues')
    queues.value = await response.json()
  } catch {
    error.value = t('queues.loadError')
  } finally {
    isLoading.value = false
  }
}

onMounted(loadQueues)
</script>

<template>
  <div class="queues">
    <p v-if="error" class="error">{{ error }}</p>

    <p v-if="isLoading" class="empty-state">{{ t('common.loading') }}</p>
    <p v-else-if="queues.length === 0" class="empty-state">{{ t('queues.empty') }}</p>

    <table v-else class="data-table">
      <thead>
        <tr>
          <th>{{ t('queues.columns.railwayUndertaking') }}</th>
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
          <td>{{ q.railwayUndertakingName }}</td>
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
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.data-table*, .status*, .error, .empty-state)
// come from src/assets/styles — only this view's own layout lives here.
.queues {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

// Rows here aren't clickable (unlike other list views), so the shared
// .data-table__row pointer cursor doesn't apply.
.data-table__row {
  cursor: default;
}
</style>
