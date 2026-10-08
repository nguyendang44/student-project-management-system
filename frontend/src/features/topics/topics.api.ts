import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const topicsApi = {
  list: (token?: string) => request<unknown>('/topics', { method: 'GET' }, token),
}
