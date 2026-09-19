import { beforeEach, describe, expect, it, vi } from 'vitest'
const { post } = vi.hoisted(() => ({ post: vi.fn() }))
vi.mock('axios', () => ({ default: { create: () => ({ post }) } }))
import { endSession, refreshSession, tokenNeedsRefresh } from './session'

describe('session renewal', () => {
  beforeEach(() => {
    post.mockReset()
    localStorage.clear()
  })
  it('shares one renewal between simultaneous callers', async () => {
    post.mockResolvedValue({ data: { token: 'new-token' } })
    const [first, second] = await Promise.all([refreshSession(), refreshSession()])
    expect(first).toBe(second)
    expect(post).toHaveBeenCalledOnce()
    expect(localStorage.getItem('token')).toBe('new-token')
  })
  it('allows a retry after a temporary failure', async () => {
    post
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce({ data: { token: 'recovered' } })
    await expect(refreshSession()).rejects.toThrow('offline')
    await expect(refreshSession()).resolves.toBe('recovered')
  })
  it('does not restore a token when renewal finishes after sign-out', async () => {
    let resolve!: (value: unknown) => void
    post.mockImplementationOnce(
      () =>
        new Promise((done) => {
          resolve = done
        }),
    )
    const pending = refreshSession()
    await endSession()
    resolve({ data: { token: 'too-late' } })
    await expect(pending).rejects.toThrow('signed out')
    expect(localStorage.getItem('token')).toBeNull()
  })
  it('renews near expiry and after sleeping past expiry', () => {
    const token = (seconds: number) =>
      `header.${btoa(JSON.stringify({ exp: Date.now() / 1000 + seconds }))}.sig`
    expect(tokenNeedsRefresh(token(3600))).toBe(false)
    expect(tokenNeedsRefresh(token(240))).toBe(true)
    expect(tokenNeedsRefresh(token(-3600))).toBe(true)
  })
})
