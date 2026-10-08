import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const automationApi = {
  list: (token?: string) => request<unknown>('/automation/runs', { method: 'GET' }, token),
}
