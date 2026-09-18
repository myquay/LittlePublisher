import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import MediaUpload from './MediaUpload.vue'
import { adminService } from '@/services/adminService'
vi.mock('@/services/adminService', () => ({ adminService: { uploadMedia: vi.fn() } }))
const props = {
  kind: 'photo' as const,
  accept: 'image/jpeg,image/png,image/gif,image/webp',
  modelValue: 'https://example.com/old.jpg',
  alt: 'Old photo',
}
describe('Photo dropzone', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.stubGlobal(
      'Image',
      class {
        src = ''
        decode() {
          return Promise.resolve()
        }
      },
    )
    URL.createObjectURL = vi.fn(() => 'blob:test')
    URL.revokeObjectURL = vi.fn()
  })
  afterEach(() => vi.unstubAllGlobals())
  it('uploads a dropped image and returns the permanent URL', async () => {
    vi.mocked(adminService.uploadMedia).mockResolvedValue({
      url: 'https://example.com/new.png',
      repositoryPath: 'media/new.png',
    })
    const w = mount(MediaUpload, { props })
    const file = new File(['png'], 'new.png', { type: 'image/png' })
    await w.get('button').trigger('drop', { dataTransfer: { files: [file] } })
    await flushPromises()
    expect(adminService.uploadMedia).toHaveBeenCalledWith(file, expect.any(Function))
    expect(w.emitted('update:modelValue')).toEqual([['https://example.com/new.png']])
    expect(w.emitted('busy')).toEqual([[true], [false]])
    w.unmount()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test')
  })
  it('keeps the previous image when an upload fails', async () => {
    vi.mocked(adminService.uploadMedia).mockRejectedValue(new Error('offline'))
    const w = mount(MediaUpload, { props })
    await w
      .get('button')
      .trigger('drop', {
        dataTransfer: { files: [new File(['png'], 'new.png', { type: 'image/png' })] },
      })
    await flushPromises()
    expect(w.emitted('update:modelValue')).toBeUndefined()
    expect(w.get('img').attributes('src')).toBe(props.modelValue)
    expect(w.get('[role="alert"]').text()).toContain('could not be completed')
    w.unmount()
  })
  it('rejects unsupported files without uploading', async () => {
    const w = mount(MediaUpload, { props })
    await w
      .get('button')
      .trigger('drop', {
        dataTransfer: { files: [new File(['text'], 'file.txt', { type: 'text/plain' })] },
      })
    await flushPromises()
    expect(adminService.uploadMedia).not.toHaveBeenCalled()
    expect(w.get('img').attributes('src')).toBe(props.modelValue)
    w.unmount()
  })
})
