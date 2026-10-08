import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const dashboardApi = {
  list: (token?: string) => request<unknown>('/dashboard', { method: 'GET' }, token),
}
