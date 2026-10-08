export type Role = 'Student' | 'Lecturer' | 'Admin'
export interface LoginRequest { email: string; password: string }
export interface LoginResponse {
  accessToken: string
  expiresAt: string
  userId: string
  email: string
  fullName: string
  role: Role
}
export interface CurrentUser { userId: string; email: string; fullName: string; role: Role }
