export type ApiFailure = { code: string; message: string; module?: string; useCases?: string[] }
export class HttpError extends Error {
  constructor(readonly status: number, readonly payload: ApiFailure | null) {
    super(payload?.message ?? `HTTP ${status}`)
  }
}
export const API_BASE = import.meta.env.VITE_API_BASE_URL || '/api/v1'
let getToken: () => string | null = () => null
export function setAuthTokenProvider(provider: () => string | null) { getToken = provider }
export async function request<T>(path: string, init: RequestInit = {}, explicitToken?: string): Promise<T> {
  const token = explicitToken ?? getToken()
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: { ...(init.body != null ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}), ...init.headers },
  })
  if (response.status === 204) return undefined as T
  const body: unknown = await response.json().catch(() => null)
  if (!response.ok) {
    const payload = body && typeof body === 'object' && 'message' in body ? body as ApiFailure : null
    throw new HttpError(response.status, payload)
  }
  return body as T
}
