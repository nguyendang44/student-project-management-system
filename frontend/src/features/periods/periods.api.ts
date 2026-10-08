import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const periodsApi = {
  list: (token?: string) => request<unknown>('/registration-periods', { method: 'GET' }, token),
}
