import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const topicregistrationsApi = {
  list: (token?: string) => request<unknown>('/topic-registrations', { method: 'GET' }, token),
}
