import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const evaluationsApi = {
  list: (token?: string) => request<unknown>('/evaluations', { method: 'GET' }, token),
}
