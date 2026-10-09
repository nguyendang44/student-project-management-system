import { request } from '../../api/client'
import type { Topic, TopicPayload } from '../topics/topics.types'
export const proposalsApi = {
  list: () => request<Topic[]>('/topic-proposals'),
  create: (body:TopicPayload) => request<Topic>('/topic-proposals', {method:'POST',body:JSON.stringify(body)}),
  submit: (id:string) => request<Topic>(`/topic-proposals/${id}/submit`,{method:'POST'}),
  approve: (id:string) => request<Topic>(`/topic-proposals/${id}/approve`,{method:'POST'}),
  reject: (id:string, reason:string) => request<Topic>(`/topic-proposals/${id}/reject`,{method:'POST',body:JSON.stringify({reason})}),
}
