import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const auditApi = {
  list: (token?: string) => request<unknown>('/audit-log', { method: 'GET' }, token),
}
