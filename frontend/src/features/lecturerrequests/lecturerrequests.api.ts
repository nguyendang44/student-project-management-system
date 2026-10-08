import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const lecturerrequestsApi = {
  list: (token?: string) => request<unknown>('/lecturer-requests', { method: 'GET' }, token),
}
