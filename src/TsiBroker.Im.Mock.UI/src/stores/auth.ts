import { ref } from 'vue'
import { defineStore } from 'pinia'
import { apiFetch } from '@/lib/api'

export const useAuthStore = defineStore('auth', () => {
  const username = ref<string | null>(null)
  const isChecked = ref(false)

  async function fetchMe() {
    try {
      const response = await apiFetch('/api/auth/me')
      const data = await response.json()
      username.value = data.username
    } catch {
      // Any failure (401, network error, malformed response, ...) means we can't
      // confirm the session, so fail closed and treat the user as logged out.
      username.value = null
    } finally {
      isChecked.value = true
    }
  }

  async function login(usernameInput: string, password: string) {
    const response = await apiFetch('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username: usernameInput, password }),
    })
    const data = await response.json()
    username.value = data.username
  }

  async function logout() {
    await apiFetch('/api/auth/logout', { method: 'POST' })
    username.value = null
  }

  return { username, isChecked, fetchMe, login, logout }
})
