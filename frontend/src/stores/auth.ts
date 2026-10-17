import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { authApi } from '../features/auth/auth.api'
import { HttpError } from '../api/client'
import { readAuthSession, writeAuthSession, removeAuthSession } from './authSession'
import type { CurrentUser } from '../features/auth/auth.types'

/** Login survives F5; Backend remains the source of truth for session validity. */
export const useAuthStore = defineStore('auth', () => {
  const saved = readAuthSession()
  const accessToken = ref<string | null>(saved?.accessToken ?? null)
  const currentUser = ref<CurrentUser | null>(saved?.currentUser ?? null)
  const expiresAt = ref<string | null>(saved?.expiresAt ?? null)
  let requiresValidation = Boolean(saved)
  let validation: Promise<void> | null = null
  const isAuthenticated = computed(() => Boolean(accessToken.value && currentUser.value))
  const role = computed(() => currentUser.value?.role ?? null)
  async function login(email: string, password: string) {
    const response = await authApi.login({ email, password })
    accessToken.value = response.accessToken
    const signedInUser: CurrentUser = {
      userId: response.userId, email: response.email, fullName: response.fullName, role: response.role,
    }
    currentUser.value = signedInUser
    expiresAt.value = response.expiresAt
    requiresValidation = false
    writeAuthSession({ accessToken: response.accessToken, currentUser: signedInUser, expiresAt: response.expiresAt })
  }
  /** Restore login across a reload and check with /auth/me before allowing navigation. */
  async function initialize(): Promise<void> {
    if (!accessToken.value) return
    if (!expiresAt.value || Date.parse(expiresAt.value) <= Date.now()) { clear(); return }
    if (!requiresValidation) return
    if (validation) return validation
    const validatingToken = accessToken.value
    validation = (async () => {
      try {
        const user = await authApi.me()
        // A different login/logout may have happened while the request was in flight.
        if (accessToken.value !== validatingToken) return
        currentUser.value = user
        requiresValidation = false
        if (expiresAt.value) {
          writeAuthSession({ accessToken: validatingToken, currentUser: user, expiresAt: expiresAt.value })
        }
      } catch (error) {
        if (accessToken.value !== validatingToken) return
        // A revoked/disabled/expired account MUST be signed out.
        // A transient network/server error MUST NOT erase the browser session.
        if (error instanceof HttpError && (error.status === 401 || error.status === 403)) clear()
      }
    })()
    try { await validation } finally { validation = null }
  }
  function clear() {
    accessToken.value = null
    currentUser.value = null
    expiresAt.value = null
    requiresValidation = false
    removeAuthSession()
  }
  async function logout() {
    try { if (accessToken.value) await authApi.logout() }
    finally { clear() }
  }
  return { accessToken, currentUser, isAuthenticated, role, login, logout, clear, initialize }
})
