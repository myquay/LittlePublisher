import api from './api'
import type { SetupStatus } from '@/types/setup'

export const setupService = {
  async getStatus(): Promise<SetupStatus> {
    const response = await api.get<SetupStatus>('/setup/status')
    return response.data
  },
}
