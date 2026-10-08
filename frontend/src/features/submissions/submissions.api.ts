import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const submissionsApi = {
  list: (token?: string) => request<unknown>('/submissions', { method: 'GET' }, token),
}
