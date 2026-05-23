import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { SetupStatus } from '@/types/setup'

vi.mock('./api', () => ({
  default: {
    get: vi.fn(),
  },
}))

const api = (await import('./api')).default as unknown as { get: ReturnType<typeof vi.fn> }
const { setupService } = await import('./setupService')

const status: SetupStatus = {
  ready: true,
  missingRequiredCount: 0,
  warningCount: 0,
  groups: [],
}

describe('setupService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('loads the public setup status', async () => {
    api.get.mockResolvedValue({ data: status })

    await expect(setupService.getPublicStatus()).resolves.toBe(status)

    expect(api.get).toHaveBeenCalledWith('/setup/public-status')
  })

  it('loads the authenticated setup status', async () => {
    api.get.mockResolvedValue({ data: status })

    await expect(setupService.getStatus()).resolves.toBe(status)

    expect(api.get).toHaveBeenCalledWith('/setup/status')
  })
})
