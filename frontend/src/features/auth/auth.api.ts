import { request } from '../../api/client'
import type { LoginRequest, LoginResponse } from './auth.types'
/** Contract only: POST /auth/login returns 501 until Auth module is implemented. */
export const authApi = {
  login: (payload: LoginRequest) => request<LoginResponse>('/auth/login', { method: 'POST', body: JSON.stringify(payload) }),
  logout: (token: string) => request<unknown>('/auth/logout', { method: 'POST' }, token),
}
