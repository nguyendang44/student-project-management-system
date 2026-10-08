import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const statisticsApi = {
  list: (token?: string) => request<unknown>('/statistics', { method: 'GET' }, token),
}
