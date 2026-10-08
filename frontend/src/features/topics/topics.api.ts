import { request } from '../../api/client'
import type { Topic, TopicPage, TopicPayload } from './topics.types'
export const topicsApi = {
  list: (search = '', page = 1, pageSize = 20) => request<TopicPage>(`/topics?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}`),
  get: (id:string) => request<Topic>(`/topics/${id}`),
  mine: () => request<Topic[]>('/topics/mine'),
  withdraw: (id:string) => request<{topicId:string;status:string;reservedForStudentUserId:null}>(`/topics/${id}/withdraw`,{method:'POST'}),
  history: (id:string) => request<Array<{fromStatus:string;toStatus:string;reason:string|null;at:string}>>(`/topics/${id}/history`),
  create: (body:TopicPayload) => request<Topic>('/topics', {method:'POST', body:JSON.stringify(body)}),
  update: (id:string, body:TopicPayload) => request<Topic>(`/topics/${id}`,{method:'PATCH',body:JSON.stringify(body)}),
  remove: (id:string) => request<void>(`/topics/${id}`,{method:'DELETE'}),
  open: (id:string, isOpen:boolean) => request<Topic>(`/topics/${id}/registration`,{method:'PATCH',body:JSON.stringify({isOpen})}),
}
