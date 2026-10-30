import { request } from '../../api/client'
/** Planned endpoint only. Backend deliberately responds 501 until this feature is implemented. */
export const githubApi = {
  list: (token?: string) => request<unknown>('/repositories', { method: 'GET' }, token),
}
