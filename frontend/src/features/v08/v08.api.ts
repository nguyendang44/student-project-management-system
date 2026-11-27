import { request } from '../../api/client'
import type { ProgressDeadlineWarning } from '../progress/progressDeadline.types'
export interface NotificationV08 {
  id: string
  type: string
  message: string
  isRead: boolean
  createdAt: string
  targetUrl?: string | null // V0804_NOTIFICATION_LINKS
}
export interface ProjectDeadlineSchedule {
  id: string; projectId: string; title: string; startAt: string | null; deadlineAt: string | null
  percent: number | null; warning: ProgressDeadlineWarning | null
}
export interface DashboardV08 {
  schedules: ProjectDeadlineSchedule[]
  atRisk: number
  projectCount: number
  pendingPlans: number
  pendingReviews: number
  revisions: number
  overdue: number
  upcomingCount: number
  pendingGuidance: number
  upcoming: { id: string; projectId: string; title: string; deadlineAt: string }[]
}
export interface GuidanceV08 {
  id: string
  projectId: string
  createdByUserId: string
  meetingAt: string
  summary: string
  feedback: string
  nextActions: string
  lecturerConfirmedAt: string | null
  status: 'PENDING' | 'CHANGES_REQUESTED' | 'CONFIRMED'
  milestoneId: string | null
  milestoneTitle: string | null
  gitProgressRunId: string | null
  git: { id: string; baseSha: string; headSha: string } | null
  nextTask: { id: string; milestoneId: string; title: string; points: number; isDeleted: boolean; outcome: string } | null
  questions: string
  updatedAt: string
  createdAt: string
}
export interface GuidanceContextV08 {
  canWrite: boolean
  milestones: { id: string; title: string; status: string }[]
  reports: { id: string; baseSha: string; headSha: string; modelName: string; completedAt: string | null }[]
}
export interface GuidanceWriteV08 {
  meetingAt: string; summary: string; questions: string; feedback?: string; nextActions?: string
  milestoneId: string | null; gitProgressRunId: string | null; expectedUpdatedAt?: string
}
export interface ProjectOptionV08 {
  id: string
  topicTitle: string
  studentName: string
  lecturerName: string
}
export const v08Api = {
  notifications: () => request<{ items: NotificationV08[]; unreadCount: number }>('/v08/notifications'),
  markRead: (id: string) => request<void>(`/v08/notifications/${id}/read`, { method: 'POST' }),
  markAllRead: () => request<void>('/v08/notifications/read-all', { method: 'POST' }),
  dashboard: () => request<DashboardV08>('/v08/dashboard'),
  projects: () => request<ProjectOptionV08[]>('/v07/projects'),
  guidance: (id: string) => request<GuidanceV08[]>(`/v08/projects/${id}/guidance`),
  guidanceContext: (id: string) => request<GuidanceContextV08>(`/v08/projects/${id}/guidance-context`),
  updateGuidance: (id: string, body: GuidanceWriteV08) => request(`/v08/guidance/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  reviewGuidance: (id: string, body: { confirm: boolean; feedback: string; nextActions: string; expectedUpdatedAt: string }) =>
    request(`/v08/guidance/${id}/review`, { method: 'POST', body: JSON.stringify(body) }),
  guidanceTask: (id: string, body: { milestoneId: string; title: string; acceptanceCriteria: string; points: number; deadlineAt: string | null; expectedUpdatedAt: string }) =>
    request(`/v08/guidance/${id}/task`, { method: 'POST', body: JSON.stringify(body) }),
  createGuidance: (id: string, body: GuidanceWriteV08) =>
    request<{ id: string }>(`/v08/projects/${id}/guidance`, { method: 'POST', body: JSON.stringify(body) }),
  confirmGuidance: (id: string) => request<{ id: string; lecturerConfirmedAt: string }>(`/v08/guidance/${id}/confirm`, { method: 'POST' }),
}
