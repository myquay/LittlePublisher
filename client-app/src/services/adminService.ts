import api from './api'
import type {
  AdminCheck,
  AdminDashboard,
  ImportRepositoryRequest,
  ImportRepositoryResult,
  MediaUploadResult,
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

  async getStagedMedia(id: string): Promise<Blob> {
    const response = await api.get<Blob>(`/media/staged/${encodeURIComponent(id)}`, {
      responseType: 'blob',
    })
    return response.data
  },

  async uploadMedia(
    file: File,
    onProgress?: (percent: number) => void,
  ): Promise<MediaUploadResult> {
    const form = new FormData()
    form.append('file', file)
    const response = await api.post<MediaUploadResult>('/media', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (event) => {
        if (event.total && onProgress) onProgress(Math.round((event.loaded / event.total) * 100))
      },
    })
    return response.data
  },
}
