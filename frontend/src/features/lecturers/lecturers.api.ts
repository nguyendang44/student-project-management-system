import { request } from '../../api/client'
import type { LecturerDirectoryEntry } from './lecturers.types'
export const lecturersApi = { list: () => request<LecturerDirectoryEntry[]>('/lecturers') }
