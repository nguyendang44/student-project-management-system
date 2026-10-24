import { request } from '../../api/client'
export interface NotificationV08 {
  id: string
  type: string
  message: string
  isRead: boolean
  createdAt: string
  targetUrl?: string | null // V0804_NOTIFICATION_LINKS
}
export interface DashboardV08 {
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
  createdAt: string
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
  createGuidance: (id: string, body: { meetingAt: string; summary: string; feedback: string; nextActions: string }) =>
    request<{ id: string }>(`/v08/projects/${id}/guidance`, { method: 'POST', body: JSON.stringify(body) }),
  confirmGuidance: (id: string) => request<{ id: string; lecturerConfirmedAt: string }>(`/v08/guidance/${id}/confirm`, { method: 'POST' }),
}
