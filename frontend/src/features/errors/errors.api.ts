import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const errorsApi = {
  list: (token?: string) => request<unknown>('/system-errors', { method: 'GET' }, token),
}
