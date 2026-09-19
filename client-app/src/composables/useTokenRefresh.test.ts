import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useTokenRefresh } from './useTokenRefresh'
const { setToken, logout, refreshSession } = vi.hoisted(() => ({
  setToken: vi.fn(),
  logout: vi.fn(),
  refreshSession: vi.fn(),
}))
vi.mock('@/stores/auth', () => ({ useAuthStore: () => ({ setToken, logout }) }))
vi.mock('@/services/session', () => ({ refreshSession, tokenNeedsRefresh: () => true }))

describe('background renewal', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.clearAllMocks()
    localStorage.setItem('token', 'expiring')
  })
  afterEach(() => {
    vi.useRealTimers()
    localStorage.clear()
  })
  const component = {
    setup() {
      useTokenRefresh()
      return () => null
    },
  }

  it('renews instead of logging out near expiry and checks again on focus', async () => {
    refreshSession.mockResolvedValue('fresh')
    const wrapper = mount(component)
    await flushPromises()
    expect(setToken).toHaveBeenCalledWith('fresh')
    window.dispatchEvent(new Event('focus'))
    await flushPromises()
    expect(refreshSession).toHaveBeenCalledTimes(2)
    expect(logout).not.toHaveBeenCalled()
    wrapper.unmount()
    window.dispatchEvent(new Event('focus'))
    await vi.advanceTimersByTimeAsync(60000)
    expect(refreshSession).toHaveBeenCalledTimes(2)
  })

  it('keeps the session state and retries later after a failed renewal', async () => {
    refreshSession.mockRejectedValueOnce(new Error('offline')).mockResolvedValue('fresh')
    const wrapper = mount(component)
    await flushPromises()
    expect(logout).not.toHaveBeenCalled()
    expect(localStorage.getItem('token')).toBe('expiring')
    await vi.advanceTimersByTimeAsync(60000)
    expect(setToken).toHaveBeenCalledWith('fresh')
    wrapper.unmount()
  })
})
