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
    error.value = e instanceof ApiError && e.status === 401 ? 'Invalid username or password.' : 'Login failed.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <main class="login">
    <form class="login-card" @submit.prevent="onSubmit">
      <svg class="logo" viewBox="0 0 166 138" role="img" aria-label="TSI-Broker">
        <g fill="none" stroke-linecap="round" stroke-linejoin="round">
          <!-- Offene Hexagonkontur -->
          <path
            d="M123.5 48.5 L80.5 24.7 L35.1 51.4 L35.1 97.2 L80.7 123.4 L123.7 98.6 L123.7 88.3"
            stroke="currentColor"
            stroke-width="4.2"
          />

          <!-- Gebogenes Gleis -->
          <g stroke="#3E4349" stroke-width="3.1">
            <path d="M62.8 40.8 C66.0 57.2 63.0 76.7 49.9 101.6" />
            <path d="M74.0 38.2 C77.7 58.2 74.3 82.0 59.9 107.2" />

            <!-- Schwellen -->
            <path d="M59.4 47.8 L74.5 44.8" />
            <path d="M60.8 59.5 L76.1 58.2" />
            <path d="M59.8 73.4 L74.5 75.4" />
            <path d="M55.7 86.3 L69.9 91.0" />
            <path d="M49.4 97.8 L63.4 104.0" />
          </g>

          <!-- Übergang / Weiche -->
          <g stroke="#3E4349" stroke-width="3.2">
            <path d="M60.3 107.0 L102.6 64.0 L136.8 64.0" />
            <path d="M77.3 112.0 L104.0 84.5" />
            <path d="M104.0 84.5 L116.0 72.6" />
            <path d="M104.0 84.5 L116.2 84.5 L127.3 84.5" />

            <!-- kleine Verbindungsdetails -->
            <path d="M91.7 75.1 L99.4 82.3" />
            <circle cx="101.8" cy="84.4" r="4.4" fill="#ffffff" />
            <circle cx="116.3" cy="72.5" r="3.2" fill="#ffffff" />

            <!-- Endpunkte -->
            <circle cx="141.7" cy="64.0" r="5.0" fill="#ffffff" />
            <circle cx="132.4" cy="84.5" r="5.0" fill="#ffffff" />
          </g>
        </g>
      </svg>

      <h1>TsiBroker.Im.Mock</h1>
      <div class="divider"><span class="dot"></span></div>

      <label class="login-field">
        <svg class="login-field__icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
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

      <label class="login-field">
        <svg class="login-field__icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
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
          class="login-toggle-password"
          :aria-label="showPassword ? 'Hide password' : 'Show password'"
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

      <button type="submit" class="submit" :disabled="isSubmitting">Sign in</button>
    </form>
  </main>
</template>

<style scoped lang="less">
// This view keeps its own `.login-field`/`.login-toggle-password` instead of
// the shared `.field`/`.toggle-password` components (see src/assets/styles):
// the login card is a standalone, always-light surface with its own input
// styling (icon-prefixed, larger radius), not the app-shell form look.
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
  width: 100px;
  height: 84px;
  color: var(--color-primary);
}

h1 {
  font-size: 1.4rem;
  font-weight: 700;
  color: #2b2b33;
  letter-spacing: 0.01em;
  text-align: center;
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

  .dot {
    position: absolute;
    top: 50%;
    left: 50%;
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: var(--color-primary);
    transform: translate(-50%, -50%);
  }
}

.login-field {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  width: 100%;
  padding: 0.75rem 1rem;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  transition: border-color 0.2s;

  &:focus-within {
    border-color: var(--color-primary);
  }

  &__icon {
    flex-shrink: 0;
    width: 20px;
    height: 20px;
    color: #8a8a94;
  }

  input {
    flex: 1;
    min-width: 0;
    border: none;
    outline: none;
    font-size: 0.95rem;
    color: #2b2b33;
    background: transparent;

    &::placeholder {
      color: #9a9aa4;
    }
  }
}

.login-toggle-password {
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

  svg {
    width: 100%;
    height: 100%;
  }
}

// Supplements the shared global `.error` (same color/font-size) with the
// alignment needed inside this centered flex column.
.error {
  align-self: flex-start;
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

  &:hover:not(:disabled) {
    background: var(--color-primary-hover);
  }

  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }
}
</style>
