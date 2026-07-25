import api from './api'
import type {
  AdminCheck,
  AdminDashboard,
  ImportRepositoryRequest,
  ImportRepositoryResult,
  Post,
  SavePostRequest,
} from '@/types/admin'

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

  async listPosts(): Promise<Post[]> {
    const response = await api.get<Post[]>('/posts')
    return response.data
  },

  async getPost(id: string): Promise<Post> {
    const response = await api.get<Post>(`/posts/${id}`)
    return response.data
  },

  async createPost(request: SavePostRequest): Promise<Post> {
    const response = await api.post<Post>('/posts', request)
    return response.data
  },

  async updatePost(id: string, request: SavePostRequest, eTag: string): Promise<Post> {
    const response = await api.put<Post>(`/posts/${id}`, request, {
      headers: { 'If-Match': eTag },
    })
    return response.data
  },

  async publishPost(id: string): Promise<Post> {
    const response = await api.post<Post>(`/posts/${id}/publish`)
    return response.data
  },
}
