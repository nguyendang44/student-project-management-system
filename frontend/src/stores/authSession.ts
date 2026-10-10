import type { CurrentUser } from '../features/auth/auth.types'

/** A browser session persisted across reloads, NOT an extension of the JWT lifetime. */
export type SavedAuthSession = {
  accessToken: string
  expiresAt: string
  currentUser: CurrentUser
}

const STORAGE_KEY = 'studentprojects.auth.v1'

function validUser(value: unknown): value is CurrentUser {
  if (typeof value !== 'object' || value === null) return false
  const user = value as Partial<CurrentUser>
  return typeof user.userId === 'string' && user.userId.length > 0
    && typeof user.email === 'string'
    && typeof user.fullName === 'string'
    && (user.role === 'Student' || user.role === 'Lecturer' || user.role === 'Admin')
}

export function removeAuthSession(): void {
  try { window.localStorage.removeItem(STORAGE_KEY) } catch { /* Storage may be disabled. */ }
}

export function readAuthSession(): SavedAuthSession | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const data: unknown = JSON.parse(raw)
    if (typeof data !== 'object' || data === null) { removeAuthSession(); return null }
    const value = data as Partial<SavedAuthSession>
    if (typeof value.accessToken !== 'string' || !value.accessToken
      || typeof value.expiresAt !== 'string' || !validUser(value.currentUser)
      || !Number.isFinite(Date.parse(value.expiresAt)) || Date.parse(value.expiresAt) <= Date.now()) {
      removeAuthSession()
      return null
    }
    return { accessToken: value.accessToken, expiresAt: value.expiresAt, currentUser: value.currentUser }
  } catch {
    removeAuthSession()
    return null
  }
}

export function writeAuthSession(value: SavedAuthSession): void {
  try { window.localStorage.setItem(STORAGE_KEY, JSON.stringify(value)) } catch { /* Login still works in memory. */ }
}
