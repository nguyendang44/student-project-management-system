export type ManagedRole = 'Student' | 'Lecturer'
export interface ManagedUser {
  id: string
  fullName: string
  email: string
  role: ManagedRole
  isActive: boolean
  studentCode: string | null
  faculty: string | null
  specialty: string | null
}
export interface PaginatedUsers {
  items: ManagedUser[]
  total: number
  page: number
  pageSize: number
}
export interface CreateUserPayload {
  fullName: string
  email: string
  password: string
  role: ManagedRole
  studentCode: string
  faculty: string
  specialty: string
}
export type UpdateUserPayload = Omit<CreateUserPayload, 'password' | 'role'>
export type OwnProfilePayload = Pick<CreateUserPayload, 'fullName' | 'faculty' | 'specialty'>
