import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { authApi } from '../features/auth/auth.api'
import type { CurrentUser } from '../features/auth/auth.types'

/** Access token is intentionally held in memory only, not localStorage/sessionStorage. */
export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(null)
  const currentUser = ref<CurrentUser | null>(null)
  const isAuthenticated = computed(() => Boolean(accessToken.value && currentUser.value))
  const role = computed(() => currentUser.value?.role ?? null)
  async function login(email: string, password: string) {
    const response = await authApi.login({ email, password })
    accessToken.value = response.accessToken
    currentUser.value = {
      userId: response.userId, email: response.email, fullName: response.fullName, role: response.role,
    }
  }
  function clear() {
    accessToken.value = null
    currentUser.value = null
  }
  async function logout() {
    try { if (accessToken.value) await authApi.logout() }
    finally { clear() }
  }
  return { accessToken, currentUser, isAuthenticated, role, login, logout, clear }
})
