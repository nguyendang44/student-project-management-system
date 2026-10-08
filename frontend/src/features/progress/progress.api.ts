import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const progressApi = {
  list: (token?: string) => request<unknown>('/progress', { method: 'GET' }, token),
}
