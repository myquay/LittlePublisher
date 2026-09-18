import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import BookSearch from './BookSearch.vue'
import api from '@/services/api'
vi.mock('@/services/api', () => ({ default: { get: vi.fn() } }))
describe('Book search', () => {
  beforeEach(() => vi.clearAllMocks())
  it('searches explicitly and emits editable metadata without an invented ISBN or rating', async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: [
        {
          id: '/works/OL1W',
          title: 'Book',
          author: 'Writer',
          coverUrl: null,
          url: 'https://openlibrary.org/works/OL1W',
          isbn: null,
          year: '1999',
        },
      ],
    })
    const w = mount(BookSearch)
    await w.get('input').setValue('Book Writer')
    expect(api.get).not.toHaveBeenCalled()
    await w.get('button').trigger('click')
    await flushPromises()
    expect(w.text()).toContain('First published 1999')
    await w
      .findAll('button')
      .find((b) => b.text() === 'Use this book')!
      .trigger('click')
    expect(w.emitted('select')![0]![0]).toEqual(
      expect.objectContaining({ 'book-title': ['Book'], 'book-isbn': [], 'book-cover': [] }),
    )
    expect(w.emitted('select')![0]![0]).not.toHaveProperty('rating')
    w.unmount()
  })
  it('keeps manual entry available when the provider fails', async () => {
    vi.mocked(api.get).mockRejectedValue(new Error('unavailable'))
    const w = mount(BookSearch)
    await w.get('input').setValue('Missing book')
    await w.get('button').trigger('click')
    await flushPromises()
    expect(w.get('[role="alert"]').text()).toContain('manually')
    expect(w.emitted('select')).toBeUndefined()
    w.unmount()
  })
  it('reports empty results', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] })
    const w = mount(BookSearch)
    await w.get('input').setValue('Unknown')
    await w.get('button').trigger('click')
    await flushPromises()
    expect(w.text()).toContain('No books found')
    w.unmount()
  })
})
