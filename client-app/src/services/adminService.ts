import api from './api'
import type { AdminCheck, AdminDashboard, ImportRepositoryRequest, ImportRepositoryResult } from '@/types/admin'

export const adminService = {
  async getDashboard(): Promise<AdminDashboard> {
    const response = await api.get<AdminDashboard>('/admin/dashboard')
    return response.data
  },

  async checkStorage(): Promise<AdminCheck> {
    const response = await api.post<AdminCheck>('/admin/checks/storage')
    return response.data
  },

  async checkGitHub(): Promise<AdminCheck> {
    const response = await api.post<AdminCheck>('/admin/checks/github')
    return response.data
  },

  async importRepository(request: ImportRepositoryRequest): Promise<ImportRepositoryResult> {
    const response = await api.post<ImportRepositoryResult>('/admin/import/repository', request)
    return response.data
  },
}
