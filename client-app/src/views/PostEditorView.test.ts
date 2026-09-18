import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import PostEditorView from './PostEditorView.vue'
import { adminService } from '@/services/adminService'
import type { Post } from '@/types/admin'

const replace = vi.fn()

vi.mock('vue-router', () => ({
  useRoute: () => ({ params: {} }),
  useRouter: () => ({ replace }),
  onBeforeRouteLeave: vi.fn(),
  onBeforeRouteUpdate: vi.fn(),
}))

vi.mock('@/services/adminService', () => ({
  adminService: {
    getPost: vi.fn(),
    createPost: vi.fn(),
    updatePost: vi.fn(),
    publishPost: vi.fn(),
    uploadMedia: vi.fn(),
  },
}))

function savedPhoto(): Post {
  return {
    id: 'photo-1',
    title: 'Nagano snow',
    content: 'The best snow',
    summary: null,
    categories: ['travel'],
    slug: 'nagano-snow',
    postType: 'photo',
    properties: {
      photo: ['https://example.com/media/snow.jpg'],
      alt: ['A snowboarder in deep snow'],
    },
    state: 'draft',
    workingRevision: 1,
    publishedRevision: null,
    createdUtc: '2026-09-18T00:00:00Z',
    updatedUtc: '2026-09-18T00:00:00Z',
    eTag: 'etag',
    hasUnpublishedChanges: true,
  }
}

describe('PostEditorView photo editing', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    HTMLDialogElement.prototype.showModal = function () {
      this.setAttribute('open', '')
    }
    HTMLDialogElement.prototype.close = function () {
      this.removeAttribute('open')
    }
    vi.mocked(adminService.createPost).mockResolvedValue(savedPhoto())
  })

  it('shows photo fields and serializes photo properties when saving a draft', async () => {
    const wrapper = mount(PostEditorView)

    await wrapper.get('select').setValue('photo')
    await wrapper.get('#property-photo').setValue('https://example.com/media/snow.jpg')
    await wrapper.get('#property-alt').setValue('A snowboarder in deep snow')
    await wrapper.get('[aria-label="Post content in Markdown"]').setValue('The best snow')
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Save draft'))!
      .trigger('click')
    await flushPromises()

    expect(adminService.createPost).toHaveBeenCalledWith(
      expect.objectContaining({
        postType: 'photo',
        properties: {
          photo: ['https://example.com/media/snow.jpg'],
          alt: ['A snowboarder in deep snow'],
        },
      }),
    )
  })
  it('preserves photo details when switching types', async () => {
    const w = mount(PostEditorView)
    await w.get('select').setValue('photo')
    await w.get('#property-photo').setValue('https://example.com/photo.jpg')
    await w.get('#property-alt').setValue('Snow')
    await w.get('select').setValue('event')
    await w.get('#property-start').setValue('2026-09-20T12:00:00+12:00')
    await w.get('select').setValue('photo')
    expect((w.get('#property-alt').element as HTMLTextAreaElement).value).toBe('Snow')
    w.unmount()
  })
  it('blocks publication without photo alternative text', async () => {
    const w = mount(PostEditorView)
    await w.get('select').setValue('photo')
    await w.get('[aria-label="Post title"]').setValue('Snow')
    await w.get('#property-photo').setValue('https://example.com/photo.jpg')
    await w
      .findAll('button')
      .find((b) => b.text() === 'Publish ↗')!
      .trigger('click')
    expect(w.text()).toContain('Add alternative text before publishing.')
    expect(adminService.publishPost).not.toHaveBeenCalled()
    w.unmount()
  })
  it('does not publish after a failed save', async () => {
    vi.mocked(adminService.createPost).mockRejectedValue({
      response: { data: { message: 'Conflict: reload the post.' } },
    })
    const w = mount(PostEditorView)
    await w.get('[aria-label="Post title"]').setValue('A new thought')
    await w
      .findAll('button')
      .find((b) => b.text() === 'Publish ↗')!
      .trigger('click')
    await w
      .findAll('button')
      .find((b) => b.text() === 'Publish post ↗')!
      .trigger('click')
    await flushPromises()
    expect(w.text()).toContain('Conflict: reload the post.')
    expect(adminService.publishPost).not.toHaveBeenCalled()
    w.unmount()
  })
})
