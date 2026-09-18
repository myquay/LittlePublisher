import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter, RouterView } from 'vue-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import PostEditorView from './PostEditorView.vue'
import { adminService } from '@/services/adminService'
import type { Post } from '@/types/admin'
vi.mock('@/services/adminService', () => ({
  adminService: {
    getPost: vi.fn(),
    createPost: vi.fn(),
    updatePost: vi.fn(),
    publishPost: vi.fn(),
  },
}))
const post: Post = {
  id: 'saved-1',
  title: 'Draft',
  content: 'Words',
  summary: null,
  categories: [],
  slug: 'draft',
  postType: 'reply',
  properties: { 'in-reply-to': ['https://example.com/one', 'https://example.com/two'] },
  requestedPublishedUtc: '2026-12-01T00:00:00Z',
  state: 'draft',
  workingRevision: 1,
  publishedRevision: null,
  createdUtc: '2026-09-18T00:00:00Z',
  updatedUtc: '2026-09-18T00:00:00Z',
  eTag: 'original-tag',
  hasUnpublishedChanges: true,
}
async function setup(path: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<p>Home</p>' } },
      { path: '/posts/new', component: PostEditorView },
      { path: '/posts/:id', component: PostEditorView },
    ],
  })
  await router.push(path)
  await router.isReady()
  const wrapper = mount(RouterView, { global: { plugins: [router] } })
  await flushPromises()
  return { router, wrapper }
}
describe('Editor navigation and concurrency', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(adminService.getPost).mockResolvedValue(post)
    vi.mocked(adminService.createPost).mockResolvedValue(post)
    vi.mocked(adminService.updatePost).mockResolvedValue({ ...post, eTag: 'next-tag' })
  })
  it('saves before returning home, preserving arrays and requested publication date', async () => {
    const { router, wrapper } = await setup('/posts/saved-1')
    await wrapper.get('[aria-label="Post title"]').setValue('Changed')
    await router.push('/')
    expect(router.currentRoute.value.path).toBe('/')
    expect(adminService.updatePost).toHaveBeenCalledWith(
      'saved-1',
      expect.objectContaining({
        title: 'Changed',
        properties: post.properties,
        requestedPublishedUtc: post.requestedPublishedUtc,
      }),
      'original-tag',
    )
    wrapper.unmount()
  })
  it('stays in the editor when a concurrency conflict prevents saving', async () => {
    vi.mocked(adminService.updatePost).mockRejectedValue({
      response: { data: { message: 'The post has changed.' } },
    })
    const { router, wrapper } = await setup('/posts/saved-1')
    await wrapper.get('[aria-label="Post title"]').setValue('Changed')
    await router.push('/')
    expect(router.currentRoute.value.path).toBe('/posts/saved-1')
    expect(wrapper.text()).toContain('The post has changed.')
    wrapper.unmount()
  })
  it('moves a newly saved draft to its permanent route without creating it twice', async () => {
    const { router, wrapper } = await setup('/posts/new')
    await wrapper.get('[aria-label="Post title"]').setValue('Draft')
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Save draft'))!
      .trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/posts/saved-1')
    expect(adminService.createPost).toHaveBeenCalledOnce()
    wrapper.unmount()
  })
})
