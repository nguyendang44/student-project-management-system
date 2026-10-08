/** Draft shapes: must be revised after Week 3 ERD is approved. */
export type UserRole = 'Student' | 'Lecturer' | 'Admin'
export type TopicStatus = 'DRAFT' | 'PENDING_APPROVAL' | 'APPROVED' | 'REJECTED' | 'IN_PROGRESS' | 'COMPLETED'
export type RequestStatus = 'PENDING' | 'ACCEPTED' | 'REJECTED'
export type ProjectStatus = 'DRAFT' | 'REGISTERED' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED'
export type MilestoneStatus = 'PENDING' | 'IN_PROGRESS' | 'SUBMITTED' | 'REVISION_REQUIRED' | 'APPROVED' | 'OVERDUE'
export type LecturerCapacityStatus = 'AVAILABLE' | 'FULL'
export interface User { id: string; fullName: string; email: string; role: UserRole; isActive: boolean }
export interface Topic { id: string; title: string; description: string; status: TopicStatus; isRegistrationOpen: boolean; proposerId: string }
export interface LecturerCapacity { lecturerId: string; maximumStudents: number; currentStudents: number; remaining: number; status: LecturerCapacityStatus }
export interface LecturerRequest { id: string; studentId: string; lecturerId: string; topicId: string; status: RequestStatus; rejectionReason?: string }
export interface Project { id: string; topicId: string; studentId: string; lecturerId: string; status: ProjectStatus }
export interface Milestone { id: string; projectId: string; title: string; deadline: string; percentComplete: number; status: MilestoneStatus }
export interface CodeAnalysisReport { id: string; projectId: string; repositoryUrl: string; quality?: string; complexity?: string; maintainability?: string; issues: string[]; recommendations: string[] }
