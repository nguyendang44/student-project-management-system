import { request } from '../../api/client'
export interface LecturerCapacityV06 {
  lecturerUserId: string
  lecturerName: string
  specialty: string | null
  registrationPeriodId: string
  periodName: string
  maxStudents: number
  currentStudents: number
  remaining: number
  status: 'AVAILABLE' | 'FULL'
}
export interface LecturerRequestV06 {
  id: string
  topicId: string
  topicTitle: string
  studentUserId: string
  studentName: string
  lecturerUserId: string
  lecturerName: string
  registrationPeriodId: string
  periodName: string
  status: 'PENDING' | 'OFFERED' | 'ACCEPTED' | 'REJECTED' | 'CANCELLED' | 'REVISION_REQUIRED'
  rejectionReason: string | null
  isCombined: boolean
  draftTitle: string | null
  draftDescription: string | null
  draftObjective: string | null
  draftExpectedContent: string | null
  draftProposedTechnology: string | null
  createdAt: string
}
export interface ProjectV06 {
  id: string
  topicId: string
  topicTitle: string
  studentUserId: string
  studentName: string
  lecturerUserId: string
  lecturerName: string
  registrationPeriodId: string
  periodName: string
  status: 'DRAFT' | 'REGISTERED' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED'
  createdAt: string
}
export type JointTopicDraft = { title: string; description: string; objective: string; expectedContent: string; proposedTechnology: string }
export const supervisionApi = {
  capacities: (periodId: string) => request<LecturerCapacityV06[]>(`/lecturer-capacity?periodId=${encodeURIComponent(periodId)}`),
  updateCapacity: (registrationPeriodId: string, maxStudents: number) => request<void>(
    '/lecturer-capacity/me', { method: 'PUT', body: JSON.stringify({ registrationPeriodId, maxStudents }) }),
  requests: () => request<LecturerRequestV06[]>('/lecturer-requests'),
  submit: (topicId: string, lecturerUserId: string, registrationPeriodId: string) => request<{ id: string; status: string }>(
    '/lecturer-requests', { method: 'POST', body: JSON.stringify({ topicId, lecturerUserId, registrationPeriodId }) }),
  combined: (body: { topicId: string | null; lecturerUserId: string; registrationPeriodId: string; draft: JointTopicDraft | null }) =>
    request<{ id: string; topicId: string; status: string }>('/lecturer-requests/combined',
      { method: 'POST', body: JSON.stringify(body) }),
  revision: (id: string, reason: string) => request<void>(`/lecturer-requests/${id}/request-revision`,
    { method: 'POST', body: JSON.stringify({ reason }) }),
  resubmit: (id: string, draft: JointTopicDraft) => request<void>(`/lecturer-requests/${id}/resubmit`,
    { method: 'POST', body: JSON.stringify({ draft }) }),
  cancel: (id: string) => request<{ status: string; deletedTopic: boolean }>(`/lecturer-requests/${id}/cancel`, { method: 'POST' }),
  accept: (id: string) => request<{ status: string; projectCreated: boolean }>(`/lecturer-requests/${id}/accept`, { method: 'POST' }),
  selectOffer: (id: string) => request<{ status: string; projectId: string }>(`/lecturer-requests/${id}/select`, { method: 'POST' }),
  reject: (id: string, reason: string) => request<void>(`/lecturer-requests/${id}/reject`, {
    method: 'POST', body: JSON.stringify({ reason }),
  }),
  projects: () => request<ProjectV06[]>('/projects'),
}
