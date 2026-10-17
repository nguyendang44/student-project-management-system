import { request } from '../../api/client'
import type { LecturerCapacityV06 } from '../supervision/supervision.api'

export const globalCapacityApi = {
  list: () => request<LecturerCapacityV06[]>('/lecturer-capacities/global'),
  update: (lecturerId: string, maxStudents: number) => request<{
    lecturerUserId: string; maxStudents: number; currentStudents: number;
    remaining: number; updatedPeriods: number;
  }>(`/lecturer-capacities/global/${lecturerId}`, {
    method: 'PUT', body: JSON.stringify({ maxStudents })
  })
}
