import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const lecturersApi = {
  list: (token?: string) => request<unknown>('/lecturers', { method: 'GET' }, token),
}
