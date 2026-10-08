import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const capacityApi = {
  list: (token?: string) => request<unknown>('/lecturer-capacity', { method: 'GET' }, token),
}
