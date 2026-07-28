<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { ApiError } from '@/lib/api'

const username = ref('')
const password = ref('')
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
    <form class="login-form" @submit.prevent="onSubmit">
      <h1>Anmelden</h1>
      <label>
        Benutzername
        <input v-model="username" type="text" autocomplete="username" required />
      </label>
      <label>
        Passwort
        <input v-model="password" type="password" autocomplete="current-password" required />
      </label>
      <p v-if="error" class="error">{{ error }}</p>
      <button type="submit" :disabled="isSubmitting">Anmelden</button>
    </form>
  </main>
</template>

<style scoped>
.login {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 60vh;
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  width: 100%;
  max-width: 320px;
}

label {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.error {
  color: #d33;
}
</style>
