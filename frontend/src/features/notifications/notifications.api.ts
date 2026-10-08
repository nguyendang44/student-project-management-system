import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const notificationsApi = {
  list: (token?: string) => request<unknown>('/notifications', { method: 'GET' }, token),
}
