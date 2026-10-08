export interface LoginRequest { email: string; password: string }
export interface LoginResponse { accessToken: string; refreshToken: string; userId: string; role: 'Student' | 'Lecturer' | 'Admin' }
