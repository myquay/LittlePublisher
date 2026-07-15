import api from './api'
import type { IncomingWebmention, OutgoingWebmention, SitePage, WebmentionList } from '@/types/webmentions'

export const webmentionService = {
  async incoming(state?: string): Promise<IncomingWebmention[]> {
    const response = await api.get<WebmentionList<IncomingWebmention>>('/admin/webmentions/incoming', { params: { state: state || undefined } })
    return response.data.items
  },
  async approveIncoming(id: string) { return (await api.post<IncomingWebmention>(`/admin/webmentions/incoming/${id}/approve`)).data },
  async rejectIncoming(id: string, reason: string, blockDomain = false) { return (await api.post<IncomingWebmention>(`/admin/webmentions/incoming/${id}/reject`, { reason, blockDomain })).data },
  async reverifyIncoming(id: string) { await api.post(`/admin/webmentions/incoming/${id}/reverify`) },
  async outgoing(state?: string): Promise<OutgoingWebmention[]> {
    const response = await api.get<WebmentionList<OutgoingWebmention>>('/admin/webmentions/outgoing', { params: { state: state || undefined } })
    return response.data.items
  },
  async sendOutgoing(id: string) { await api.post(`/admin/webmentions/outgoing/${id}/send`) },
  async retryOutgoing(id: string) { await api.post(`/admin/webmentions/outgoing/${id}/retry`) },
  async declineOutgoing(id: string) { return (await api.post<OutgoingWebmention>(`/admin/webmentions/outgoing/${id}/decline`)).data },
  async pages(): Promise<SitePage[]> { return (await api.get<WebmentionList<SitePage>>('/admin/site-pages')).data.items },
  async scan(sourceUrl: string, forceResend = false) { await api.post('/admin/webmentions/outgoing/scan', { sourceUrl, forceResend }) },
  async createLike(targetUrl: string, comment: string) { return (await api.post('/admin/content/likes', { targetUrl, comment: comment || null })).data },
  async createReply(targetUrl: string, comment: string, targetTitle: string) { return (await api.post('/admin/content/replies', { targetUrl, comment, targetTitle: targetTitle || null })).data },
  async createBlogroll(name: string, siteUrl: string, feedUrl: string, comment: string) { return (await api.post('/admin/blogroll', { name, siteUrl, feedUrl: feedUrl || null, comment: comment || null })).data },
}
