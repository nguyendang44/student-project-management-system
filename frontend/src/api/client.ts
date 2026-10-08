/** Contract only: no live auth token acquisition or retry/refresh implementation. */
export type ApiFailure = { code: string; message: string; module?: string; useCases?: string[] }
export class HttpError extends Error {
  constructor(readonly status: number, readonly payload: ApiFailure | null) {
    super(payload?.message ?? `HTTP ${status}`)
  }
}
export const API_BASE = import.meta.env.VITE_API_BASE_URL || '/api/v1'
export async function request<T>(path: string, init: RequestInit = {}, accessToken?: string): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}), ...init.headers },
  })
  const body: unknown = await response.json().catch(() => null)
  if (!response.ok) {
    const payload = body && typeof body === 'object' && 'message' in body ? body as ApiFailure : null
    throw new HttpError(response.status, payload)
  }
  return body as T
}
