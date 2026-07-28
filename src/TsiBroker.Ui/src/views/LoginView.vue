<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { ApiError } from '@/lib/api'

const username = ref('')
const password = ref('')
const showPassword = ref(false)
const error = ref('')
const isSubmitting = ref(false)

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

async function onSubmit() {
  error.value = ''
  isSubmitting.value = true
  try {
    await auth.login(username.value, password.value)
    const redirect = (route.query.redirect as string) || '/'
    await router.push(redirect)
  } catch (e) {
    error.value = e instanceof ApiError && e.status === 401 ? 'Benutzername oder Passwort ist falsch.' : 'Login fehlgeschlagen.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <main class="login">
    <form class="login-card" @submit.prevent="onSubmit">
      <img class="logo" src="@/assets/logo-icon.svg" alt="TSI-Broker" />

      <h1>TSI-Broker</h1>
      <div class="divider"><span class="dot"></span></div>

      <label class="field">
        <svg class="field-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <circle cx="12" cy="8" r="4" stroke="currentColor" stroke-width="1.8" />
          <path d="M4 20c0-4.4 3.6-7 8-7s8 2.6 8 7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
        </svg>
        <input
          v-model="username"
          type="text"
          placeholder="Username"
          autocomplete="username"
          required
        />
      </label>

      <label class="field">
        <svg class="field-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <rect x="5" y="10.5" width="14" height="9.5" rx="2" stroke="currentColor" stroke-width="1.8" />
          <path d="M8 10.5V7.5a4 4 0 0 1 8 0v3" stroke="currentColor" stroke-width="1.8" />
        </svg>
        <input
          v-model="password"
          :type="showPassword ? 'text' : 'password'"
          placeholder="Password"
          autocomplete="current-password"
          required
        />
        <button
          type="button"
          class="toggle-password"
          :aria-label="showPassword ? 'Passwort verbergen' : 'Passwort anzeigen'"
          @click="showPassword = !showPassword"
        >
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path
              d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"
              stroke="currentColor"
              stroke-width="1.8"
            />
            <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="1.8" />
            <path v-if="!showPassword" d="M4 4 L20 20" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
          </svg>
        </button>
      </label>

      <p v-if="error" class="error">{{ error }}</p>

      <button type="submit" class="submit" :disabled="isSubmitting">Login</button>
    </form>
  </main>
</template>

<style scoped>
.login {
  position: fixed;
  inset: 0;
  display: flex;
  justify-content: center;
  align-items: center;
  overflow: auto;
  padding: 2rem;
  background:
    radial-gradient(circle at 15% 20%, color-mix(in srgb, var(--color-primary) 6%, transparent), transparent 40%),
    radial-gradient(circle at 85% 80%, color-mix(in srgb, var(--color-primary) 6%, transparent), transparent 40%),
    linear-gradient(135deg, #ece9f1 0%, #e7e4ee 100%);
}

.login-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.9rem;
  width: 100%;
  max-width: 380px;
  padding: 2.75rem 2.5rem 2.5rem;
  background: var(--vt-c-white);
  border-radius: 16px;
  box-shadow:
    0 20px 45px rgba(30, 20, 45, 0.12),
    0 2px 8px rgba(30, 20, 45, 0.06);
}

.logo {
  width: 84px;
  height: 84px;
  object-fit: contain;
}

h1 {
  font-size: 1.7rem;
  font-weight: 700;
  color: #2b2b33;
  letter-spacing: 0.01em;
}

.divider {
  position: relative;
  width: 100%;
  height: 1px;
  margin: 0.25rem 0 0.75rem;
  background: linear-gradient(
    to right,
    transparent,
    color-mix(in srgb, var(--color-primary) 45%, transparent),
    transparent
  );
}

.divider .dot {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--color-primary);
  transform: translate(-50%, -50%);
}

.field {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  width: 100%;
  padding: 0.75rem 1rem;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  transition: border-color 0.2s;
}

.field:focus-within {
  border-color: var(--color-primary);
}

.field-icon {
  flex-shrink: 0;
  width: 20px;
  height: 20px;
  color: #8a8a94;
}

.field input {
  flex: 1;
  min-width: 0;
  border: none;
  outline: none;
  font-size: 0.95rem;
  color: #2b2b33;
  background: transparent;
}

.field input::placeholder {
  color: #9a9aa4;
}

.toggle-password {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 20px;
  height: 20px;
  padding: 0;
  border: none;
  background: transparent;
  color: #8a8a94;
  cursor: pointer;
}

.toggle-password svg {
  width: 100%;
  height: 100%;
}

.error {
  align-self: flex-start;
  font-size: 0.85rem;
  color: #d33;
}

.submit {
  width: 100%;
  margin-top: 0.4rem;
  padding: 0.85rem;
  border: none;
  border-radius: 10px;
  background: var(--color-primary);
  color: var(--color-on-primary);
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 0.2s;
}

.submit:hover:not(:disabled) {
  background: var(--color-primary-hover);
}

.submit:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
