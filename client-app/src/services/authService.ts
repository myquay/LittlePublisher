import api from './api'
import type { User } from '@/types/auth'

export const authService = {

  async getMe(): Promise<User> {
    const response = await api.get<User>('/auth/me')
    return response.data
  },
}
