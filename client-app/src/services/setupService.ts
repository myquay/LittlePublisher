import api from './api'
import type { SetupStatus } from '@/types/setup'

export const setupService = {
  async getPublicStatus(): Promise<SetupStatus> {
    const response = await api.get<SetupStatus>('/setup/public-status')
    return response.data
  },

  async getStatus(): Promise<SetupStatus> {
    const response = await api.get<SetupStatus>('/setup/status')
    return response.data
  },
}
