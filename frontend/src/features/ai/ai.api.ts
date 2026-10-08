import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const aiApi = {
  list: (token?: string) => request<unknown>('/ai-analysis', { method: 'GET' }, token),
}
