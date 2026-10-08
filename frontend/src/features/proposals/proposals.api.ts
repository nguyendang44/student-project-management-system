import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const proposalsApi = {
  list: (token?: string) => request<unknown>('/topic-proposals', { method: 'GET' }, token),
}
