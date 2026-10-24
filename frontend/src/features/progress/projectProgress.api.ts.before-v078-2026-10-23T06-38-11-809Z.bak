import { request } from '../../api/client'

export type ProjectV07 = {
  id: string; topicId: string; topicTitle: string; studentUserId: string; studentName: string;
  lecturerUserId: string; lecturerName: string; status: string; description: string | null;
  milestoneCount: number; approvedCount: number; percentComplete: number
}
export type MilestoneV07 = {
  id: string; projectId: string; title: string; description: string | null;
  startAt: string; deadlineAt: string; expectedResult: string | null;
  percentComplete: number; status: string; isOverdue: boolean; createdAt: string; updatedAt: string;
  proposedByStudent: boolean; proposalStatus: 'CONFIRMED' | 'PENDING_REVIEW' | 'CHANGES_REQUESTED'; proposalFeedback: string | null
}
export type MilestoneForm = { title: string; description: string; startAt: string; deadlineAt: string; expectedResult: string }
export type ProgressHistory = {
  progress: { id: string; percentComplete: number; workDone: string; note: string | null; createdAt: string }[]
  submissions: { id: string; resultSummary: string; documentReference: string | null; submittedAt: string }[]
  reviews: { id: string; feedback: string | null; isApproved: boolean; createdAt: string }[]
}
export const projectProgressApi = {
  projects: () => request<ProjectV07[]>('/v07/projects'),
  milestones: (id: string) => request<MilestoneV07[]>(`/v07/projects/${id}/milestones`),
  create: (id: string, data: MilestoneForm) => request<{ id: string }>(`/v07/projects/${id}/milestones`, {method:'POST',body:JSON.stringify(data)}),
  update: (id: string, data: MilestoneForm) => request<{ id: string }>(`/v07/milestones/${id}`, {method:'PUT',body:JSON.stringify(data)}),
  history: (id: string) => request<ProgressHistory>(`/v07/milestones/${id}/history`),
  proposalReview: (id: string, isConfirmed: boolean, feedback: string) =>
    request(`/v07/milestones/${id}/proposal-review`, {method:'POST',body:JSON.stringify({isConfirmed,feedback})}),
  progress: (id: string, percentComplete: number, workDone: string, note: string) =>
    request(`/v07/milestones/${id}/progress`,{method:'POST',body:JSON.stringify({percentComplete,workDone,note})}),
  submit: (id: string, resultSummary: string, documentReference: string) =>
    request(`/v07/milestones/${id}/submit`,{method:'POST',body:JSON.stringify({resultSummary,documentReference})}),
  review: (id: string, isApproved: boolean, feedback: string) =>
    request(`/v07/milestones/${id}/review`,{method:'POST',body:JSON.stringify({isApproved,feedback})})
}
