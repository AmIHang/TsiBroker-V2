import { ref } from 'vue'
import { defineStore } from 'pinia'
import { apiFetch, ApiError } from '@/lib/api'

export const useAuthStore = defineStore('auth', () => {
  const username = ref<string | null>(null)
  const isChecked = ref(false)

  async function fetchMe() {
    try {
      const response = await apiFetch('/api/auth/me')
      const data = await response.json()
      username.value = data.username
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        username.value = null
      } else {
        throw error
      }
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
