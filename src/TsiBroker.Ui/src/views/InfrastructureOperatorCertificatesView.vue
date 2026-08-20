<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ApiError, apiFetch } from '@/lib/api'
import { hasTopbarOverride } from '@/composables/useTopbarOverride'

interface InfrastructureOperator {
  id: string
  name: string
  ricsCode: string
  systemUrl: string
  isActive: boolean
}

interface PartnerCertificateBundle {
  id: string
  infrastructureOperatorId: string
  clientCertificateFileName: string | null
  expectedServerCaCertificateFileName: string | null
  expectedServerCommonName: string | null
  expectedClientCaCertificateFileName: string | null
  expectedClientCommonName: string | null
  clientCrlUrl: string | null
  serverCrlUrl: string | null
}

type CertificateSlot = 'client-certificate' | 'server-ca-certificate' | 'client-ca-certificate'

const route = useRoute()
const { t } = useI18n()

const infrastructureOperator = ref<InfrastructureOperator | null>(null)
const bundle = ref<PartnerCertificateBundle | null>(null)
const isLoading = ref(true)
const loadError = ref('')

const expectedServerCommonName = ref('')
const expectedClientCommonName = ref('')
const clientCrlUrl = ref('')
const serverCrlUrl = ref('')
const isSavingIdentity = ref(false)
const identityError = ref('')
const identitySaved = ref(false)

const uploadingSlot = ref<CertificateSlot | null>(null)
const uploadError = ref('')

const isDeleting = ref(false)
const deleteError = ref('')

function resetIdentityFields() {
  expectedServerCommonName.value = bundle.value?.expectedServerCommonName ?? ''
  expectedClientCommonName.value = bundle.value?.expectedClientCommonName ?? ''
  clientCrlUrl.value = bundle.value?.clientCrlUrl ?? ''
  serverCrlUrl.value = bundle.value?.serverCrlUrl ?? ''
}

async function loadBundle() {
  try {
    const response = await apiFetch(`/api/certificates/${route.params.id}`)
    bundle.value = await response.json()
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      bundle.value = null
      return
    }
    throw err
  }
}

async function load() {
  isLoading.value = true
  loadError.value = ''
  try {
    const response = await apiFetch('/api/infrastructure-operators')
    const operators: InfrastructureOperator[] = await response.json()
    const found = operators.find((o) => o.id === route.params.id)
    if (!found) {
      loadError.value = t('infrastructureOperatorCertificates.operatorNotFound')
      return
    }

    infrastructureOperator.value = found
    await loadBundle()
    resetIdentityFields()
    hasTopbarOverride.value = true
  } catch {
    loadError.value = t('infrastructureOperatorCertificates.loadError')
  } finally {
    isLoading.value = false
  }
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => {
      const result = reader.result as string
      resolve(result.slice(result.indexOf(',') + 1))
    }
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(file)
  })
}

function uploadButtonLabel(slot: CertificateSlot, hasFile: boolean) {
  if (uploadingSlot.value === slot) {
    return t('infrastructureOperatorCertificates.uploading')
  }
  return hasFile ? t('infrastructureOperatorCertificates.replace') : t('infrastructureOperatorCertificates.upload')
}

function certificateErrorMessage(err: unknown): string | null {
  if (!(err instanceof ApiError) || !err.body || typeof err.body !== 'object') {
    return null
  }
  const body = err.body as { error?: string }
  if (body.error === 'invalid_certificate') {
    return t('infrastructureOperatorCertificates.invalidCertificateError')
  }
  if (body.error === 'invalid_base64') {
    return t('infrastructureOperatorCertificates.invalidFileError')
  }
  return null
}

async function uploadCertificate(slot: CertificateSlot, event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) {
    return
  }

  uploadError.value = ''
  uploadingSlot.value = slot
  try {
    const certificateBase64 = await fileToBase64(file)
    const response = await apiFetch(`/api/certificates/${route.params.id}/${slot}`, {
      method: 'POST',
      body: JSON.stringify({ certificateBase64 }),
    })
    bundle.value = await response.json()
  } catch (err) {
    uploadError.value = certificateErrorMessage(err) ?? t('infrastructureOperatorCertificates.uploadError')
  } finally {
    uploadingSlot.value = null
    input.value = ''
  }
}

async function saveIdentity() {
  identityError.value = ''
  isSavingIdentity.value = true
  try {
    const response = await apiFetch(`/api/certificates/${route.params.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        expectedServerCommonName: expectedServerCommonName.value.trim() || null,
        expectedClientCommonName: expectedClientCommonName.value.trim() || null,
        clientCrlUrl: clientCrlUrl.value.trim() || null,
        serverCrlUrl: serverCrlUrl.value.trim() || null,
      }),
    })
    bundle.value = await response.json()
    resetIdentityFields()
    identitySaved.value = true
    setTimeout(() => (identitySaved.value = false), 2500)
  } catch {
    identityError.value = t('infrastructureOperatorCertificates.saveError')
  } finally {
    isSavingIdentity.value = false
  }
}

async function deleteAllCertificates() {
  if (!bundle.value || !confirm(t('infrastructureOperatorCertificates.confirmDelete'))) {
    return
  }

  deleteError.value = ''
  isDeleting.value = true
  try {
    await apiFetch(`/api/certificates/${route.params.id}`, { method: 'DELETE' })
    bundle.value = null
    resetIdentityFields()
  } catch {
    deleteError.value = t('infrastructureOperatorCertificates.deleteError')
  } finally {
    isDeleting.value = false
  }
}

onMounted(load)
onUnmounted(() => {
  hasTopbarOverride.value = false
})
</script>

<template>
  <div class="certificates-page">
    <p v-if="isLoading" class="empty-state">{{ t('common.loading') }}</p>
    <p v-else-if="loadError" class="error">{{ loadError }}</p>

    <template v-else-if="infrastructureOperator">
      <Teleport to="#topbar-custom-title">
        <div class="topbar-title-block">
          <h1 class="topbar__title">{{ infrastructureOperator.name }}</h1>
          <RouterLink class="topbar-breadcrumb" :to="{ name: 'infrastructure-operators' }">
            {{ t('infrastructureOperatorCertificates.breadcrumbBack') }}
          </RouterLink>
        </div>
      </Teleport>

      <Teleport to="#topbar-actions">
        <button
          v-if="bundle"
          type="button"
          class="icon-btn-header icon-btn-header--danger icon-btn-header--lg"
          :disabled="isDeleting"
          :aria-label="t('infrastructureOperatorCertificates.deleteAll')"
          :title="t('infrastructureOperatorCertificates.deleteAll')"
          @click="deleteAllCertificates"
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
      </Teleport>

      <p v-if="deleteError" class="error">{{ deleteError }}</p>

      <div class="columns">
        <section class="card">
          <h3 class="card__title">{{ t('infrastructureOperatorCertificates.clientCertificate') }}</h3>
          <p class="hint">{{ t('infrastructureOperatorCertificates.clientCertificateHint') }}</p>

          <p v-if="bundle?.clientCertificateFileName" class="status status--active">
            {{ t('infrastructureOperatorCertificates.configured', { fileName: bundle.clientCertificateFileName }) }}
          </p>
          <p v-else class="status status--inactive">{{ t('infrastructureOperatorCertificates.notConfigured') }}</p>

          <label
            class="btn file-upload"
            :class="{ 'file-upload--busy': uploadingSlot === 'client-certificate' }"
          >
            <input
              type="file"
              accept=".pfx,.p12"
              class="file-upload__input"
              :disabled="uploadingSlot === 'client-certificate'"
              @change="uploadCertificate('client-certificate', $event)"
            />
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M12 15V4m0 0-4 4m4-4 4 4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
              <path d="M4 15v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
            <span>{{ uploadButtonLabel('client-certificate', !!bundle?.clientCertificateFileName) }}</span>
          </label>
        </section>

        <section class="card">
          <h3 class="card__title">{{ t('infrastructureOperatorCertificates.serverCaCertificate') }}</h3>
          <p class="hint">{{ t('infrastructureOperatorCertificates.serverCaCertificateHint') }}</p>

          <p v-if="bundle?.expectedServerCaCertificateFileName" class="status status--active">
            {{ t('infrastructureOperatorCertificates.configured', { fileName: bundle.expectedServerCaCertificateFileName }) }}
          </p>
          <p v-else class="status status--inactive">{{ t('infrastructureOperatorCertificates.notConfigured') }}</p>

          <label
            class="btn file-upload"
            :class="{ 'file-upload--busy': uploadingSlot === 'server-ca-certificate' }"
          >
            <input
              type="file"
              accept=".cer,.crt,.pem"
              class="file-upload__input"
              :disabled="uploadingSlot === 'server-ca-certificate'"
              @change="uploadCertificate('server-ca-certificate', $event)"
            />
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M12 15V4m0 0-4 4m4-4 4 4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
              <path d="M4 15v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
            <span>{{ uploadButtonLabel('server-ca-certificate', !!bundle?.expectedServerCaCertificateFileName) }}</span>
          </label>
        </section>

        <section class="card">
          <h3 class="card__title">{{ t('infrastructureOperatorCertificates.clientCaCertificate') }}</h3>
          <p class="hint">{{ t('infrastructureOperatorCertificates.clientCaCertificateHint') }}</p>

          <p v-if="bundle?.expectedClientCaCertificateFileName" class="status status--active">
            {{ t('infrastructureOperatorCertificates.configured', { fileName: bundle.expectedClientCaCertificateFileName }) }}
          </p>
          <p v-else class="status status--inactive">{{ t('infrastructureOperatorCertificates.notConfigured') }}</p>

          <label
            class="btn file-upload"
            :class="{ 'file-upload--busy': uploadingSlot === 'client-ca-certificate' }"
          >
            <input
              type="file"
              accept=".cer,.crt,.pem"
              class="file-upload__input"
              :disabled="uploadingSlot === 'client-ca-certificate'"
              @change="uploadCertificate('client-ca-certificate', $event)"
            />
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M12 15V4m0 0-4 4m4-4 4 4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
              <path d="M4 15v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
            <span>{{ uploadButtonLabel('client-ca-certificate', !!bundle?.expectedClientCaCertificateFileName) }}</span>
          </label>
        </section>
      </div>

      <p v-if="uploadError" class="error">{{ uploadError }}</p>

      <section class="card">
        <h3 class="card__title">{{ t('infrastructureOperatorCertificates.identityTitle') }}</h3>
        <form @submit.prevent="saveIdentity">
          <label class="field">
            <span class="field__label">{{ t('infrastructureOperatorCertificates.expectedServerCommonName') }}</span>
            <input v-model="expectedServerCommonName" type="text" />
          </label>
          <label class="field">
            <span class="field__label">{{ t('infrastructureOperatorCertificates.expectedClientCommonName') }}</span>
            <input v-model="expectedClientCommonName" type="text" />
          </label>
          <label class="field">
            <span class="field__label">{{ t('infrastructureOperatorCertificates.clientCrlUrl') }}</span>
            <input v-model="clientCrlUrl" type="url" />
          </label>
          <label class="field">
            <span class="field__label">{{ t('infrastructureOperatorCertificates.serverCrlUrl') }}</span>
            <input v-model="serverCrlUrl" type="url" />
          </label>

          <p v-if="identityError" class="error">{{ identityError }}</p>
          <p v-if="identitySaved" class="hint">{{ t('infrastructureOperatorCertificates.identitySaved') }}</p>

          <div class="toolbar">
            <button type="submit" class="btn btn--primary" :disabled="isSavingIdentity">{{ t('common.save') }}</button>
          </div>
        </form>
      </section>
    </template>
  </div>
</template>

<style scoped lang="less">
// Shared building blocks (.btn, .field, .card*, .status*, .hint, .error,
// .empty-state, .toolbar, .icon-btn-header*) come from src/assets/styles —
// only this view's own layout lives here.

// A native <input type="file"> renders as an unstyled OS-native "Browse..."
// widget — visually hide it (not display:none, which some assistive tech
// skips) inside a .btn-styled <label>, which triggers the file picker on
// click/keyboard activation the same way a real file input does.
.file-upload {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  width: fit-content;

  svg {
    width: 16px;
    height: 16px;
  }

  &--busy {
    opacity: 0.6;
    pointer-events: none;
  }
}

.file-upload__input {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

.certificates-page {
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

@media (min-width: 64rem) {
  .columns {
    flex-direction: row;
    align-items: flex-start;

    .card {
      flex: 1 1 0;
      min-width: 0;
    }
  }
}

// .topbar-title-block / .topbar__title / .topbar-breadcrumb come from
// @tsibroker/ui-kit's styles/topbar.less (used here via Teleport into the
// kit's Topbar component, rendered by AppShell.vue).
</style>
