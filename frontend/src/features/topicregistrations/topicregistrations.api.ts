import { request } from '../../api/client'
import type { TopicRegistration } from '../topics/topics.types'
export const topicregistrationsApi = {
  list: () => request<TopicRegistration[]>('/topic-registrations'),
  create: (topicId:string,registrationPeriodId:string) => request<TopicRegistration>('/topic-registrations',{method:'POST',body:JSON.stringify({topicId,registrationPeriodId})}),
  cancel: (id:string) => request<{id:string;status:string}>(`/topic-registrations/${id}/cancel`,{method:'POST'}),
  accept: (id:string) => request<{id:string;status:string}>(`/topic-registrations/${id}/accept`,{method:'POST'}),
  reject: (id:string) => request<{id:string;status:string}>(`/topic-registrations/${id}/reject`,{method:'POST'}),
}
