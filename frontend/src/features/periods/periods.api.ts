import { request } from '../../api/client'
import type { RegistrationPeriod } from '../topics/topics.types'
export const periodsApi = {
  list: () => request<RegistrationPeriod[]>('/registration-periods'),
  create: (body:{name:string;startsAt:string;endsAt:string;isOpen:boolean}) => request<RegistrationPeriod>('/registration-periods',{method:'POST',body:JSON.stringify(body)}),
  setState: (id:string,isOpen:boolean) => request<RegistrationPeriod>(`/registration-periods/${id}/status`,{method:'PATCH',body:JSON.stringify({isOpen})}),
}
