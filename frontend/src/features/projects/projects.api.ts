import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const projectsApi = {
  list: (token?: string) => request<unknown>('/projects', { method: 'GET' }, token),
}
