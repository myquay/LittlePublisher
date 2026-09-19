import axios, { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import api from './api'
import { refreshSession } from './session'
vi.mock('./session', () => ({ refreshSession: vi.fn() }))

function unauthorized(config: InternalAxiosRequestConfig) {
  return new AxiosError('Unauthorized', '401', config, undefined, {
    status: 401,
    statusText: 'Unauthorized',
    data: {},
    headers: {},
    config,
  })
}

describe('API session recovery', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.setItem('token', 'expired')
  })

  it('renews and retries the same save with its body and concurrency header', async () => {
    vi.mocked(refreshSession).mockImplementation(async () => {
      localStorage.setItem('token', 'fresh')
      return 'fresh'
    })
    const adapter = vi.fn(async (config: InternalAxiosRequestConfig) => {
      if (config.headers.Authorization === 'Bearer expired') throw unauthorized(config)
      return { status: 200, data: 'saved', statusText: 'OK', headers: {}, config }
    })
    const result = await api.put(
      '/posts/one',
      { content: 'Keep these words' },
      {
        adapter,
        headers: { 'If-Match': 'revision-1' },
      },
    )
    expect(result.data).toBe('saved')
    expect(adapter).toHaveBeenCalledTimes(2)
    const retried = adapter.mock.calls[1]![0]
    expect(retried.headers.Authorization).toBe('Bearer fresh')
    expect(retried.headers['If-Match']).toBe('revision-1')
    expect(JSON.parse(retried.data)).toEqual({ content: 'Keep these words' })
  })

  it('retains the page and token and explains how to recover if renewal expires', async () => {
    const location = window.location.href
    vi.mocked(refreshSession).mockRejectedValue(
      unauthorized({ headers: new AxiosHeaders() } as InternalAxiosRequestConfig),
    )
    const adapter = vi.fn(async (config: InternalAxiosRequestConfig) => {
      throw unauthorized(config)
    })
    await expect(api.post('/posts', { content: 'Unsaved' }, { adapter })).rejects.toMatchObject({
      response: { data: { message: expect.stringContaining('Keep this editor open') } },
    })
    expect(adapter).toHaveBeenCalledOnce()
    expect(window.location.href).toBe(location)
    expect(localStorage.getItem('token')).toBe('expired')
  })

  it('preserves a save conflict returned after successful renewal', async () => {
    vi.mocked(refreshSession).mockImplementation(async () => {
      localStorage.setItem('token', 'fresh')
      return 'fresh'
    })
    const adapter = vi.fn(async (config: InternalAxiosRequestConfig) => {
      if (config.headers.Authorization === 'Bearer expired') throw unauthorized(config)
      throw new AxiosError('Conflict', '409', config, undefined, {
        status: 409,
        statusText: 'Conflict',
        data: { message: 'Draft changed elsewhere' },
        headers: {},
        config,
      })
    })
    await expect(api.put('/posts/one', {}, { adapter })).rejects.toMatchObject({
      response: { status: 409, data: { message: 'Draft changed elsewhere' } },
    })
  })

  it('does not loop when the retried request is also unauthorized', async () => {
    vi.mocked(refreshSession).mockResolvedValue('fresh')
    const adapter = vi.fn(async (config: InternalAxiosRequestConfig) => {
      throw unauthorized(config)
    })
    await expect(api.get('/auth/me', { adapter })).rejects.toBeInstanceOf(axios.AxiosError)
    expect(adapter).toHaveBeenCalledTimes(2)
    expect(refreshSession).toHaveBeenCalledOnce()
  })
})
