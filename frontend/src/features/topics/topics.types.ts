export type TopicStatus = 'DRAFT'|'PENDING_APPROVAL'|'APPROVED'|'REJECTED'|'IN_PROGRESS'|'COMPLETED'|'CANCELLED'|'PUBLISHED'
export interface Topic { id:string; title:string; description:string; objective:string|null; expectedContent:string|null; proposedTechnology:string|null; proposedByUserId:string; proposedBy:string; status:TopicStatus; isRegistrationOpen:boolean; createdAt:string; reservedForStudentUserId:string|null; canDelete:boolean }
export type TopicPayload = { title:string; description:string; objective:string; expectedContent:string; proposedTechnology:string }
export interface TopicPage { items: Topic[]; total:number; page:number; pageSize:number }
export interface RegistrationPeriod { id:string; name:string; startsAt:string; endsAt:string; isOpen:boolean }
export interface TopicRegistration { id:string; topicId:string; topicTitle:string; studentUserId:string; studentName:string; registrationPeriodId:string; periodName:string; status:string; createdAt:string }
