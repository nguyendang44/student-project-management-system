import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const milestonesApi = {
  list: (token?: string) => request<unknown>('/milestones', { method: 'GET' }, token),
}
