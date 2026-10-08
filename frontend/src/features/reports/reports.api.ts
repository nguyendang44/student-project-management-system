import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const reportsApi = {
  list: (token?: string) => request<unknown>('/reports', { method: 'GET' }, token),
}
