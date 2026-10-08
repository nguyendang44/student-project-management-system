import { request } from '../../api/client'
import type { LoginRequest, LoginResponse, CurrentUser } from './auth.types'
export const authApi = {
  login: (payload: LoginRequest) => request<LoginResponse>('/auth/login', { method: 'POST', body: JSON.stringify(payload) }),
  me: () => request<CurrentUser>('/auth/me'),
  logout: () => request<void>('/auth/logout', { method: 'POST' }),
}
