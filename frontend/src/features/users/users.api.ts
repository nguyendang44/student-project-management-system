import { request } from '../../api/client'
import type { CreateUserPayload, UpdateUserPayload, ManagedUser, PaginatedUsers, OwnProfilePayload } from './users.types'
export const usersApi = {
  list: (filters: { role?: string; search?: string; page?: number; pageSize?: number } = {}) => {
    const params = new URLSearchParams()
    if (filters.role) params.set('role', filters.role)
    if (filters.search) params.set('search', filters.search)
    params.set('page', String(filters.page ?? 1))
    params.set('pageSize', String(filters.pageSize ?? 20))
    return request<PaginatedUsers>(`/users?${params.toString()}`)
  },
  get: (id: string) => request<ManagedUser>(`/users/${encodeURIComponent(id)}`),
  create: (payload: CreateUserPayload) => request<ManagedUser>('/users', { method: 'POST', body: JSON.stringify(payload) }),
  update: (id: string, payload: UpdateUserPayload) => request<ManagedUser>(`/users/${encodeURIComponent(id)}`, { method: 'PUT', body: JSON.stringify(payload) }),
  setStatus: (id: string, isActive: boolean) => request<ManagedUser>(`/users/${encodeURIComponent(id)}/status`, { method: 'PATCH', body: JSON.stringify({ isActive }) }),
  ownProfile: () => request<ManagedUser>('/users/me/profile'),
  updateOwnProfile: (payload: OwnProfilePayload) => request<ManagedUser>('/users/me/profile', { method: 'PUT', body: JSON.stringify(payload) }),
}
