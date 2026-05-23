import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import SetupView from './SetupView.vue'
import { setupService } from '@/services/setupService'
import type { SetupStatus } from '@/types/setup'

vi.mock('@/services/setupService', () => ({
  setupService: {
    getPublicStatus: vi.fn(),
    getStatus: vi.fn(),
  },
}))

const readyStatus: SetupStatus = {
  ready: true,
  missingRequiredCount: 0,
  warningCount: 0,
  groups: [
    {
      name: 'Authentication',
      checks: [
        {
          key: 'App:Jwt:SecretKey',
          description: 'JWT signing key.',
          required: true,
          secret: true,
          configured: true,
          warning: false,
          displayValue: null,
        },
      ],
    },
  ],
}

describe('SetupView', () => {
  it('renders an initial public setup status without loading from the API', () => {
    const wrapper = mount(SetupView, {
      props: {
        initialStatus: readyStatus,
        publicMode: true,
      },
    })

    expect(wrapper.text()).toContain('Authentication setup')
    expect(wrapper.text()).toContain('Ready to publish')
    expect(wrapper.text()).toContain('App:Jwt:SecretKey')
    expect(setupService.getPublicStatus).not.toHaveBeenCalled()
  })

  it('loads full setup status when mounted without an initial status', async () => {
    vi.mocked(setupService.getStatus).mockResolvedValue(readyStatus)

    const wrapper = mount(SetupView)
    await vi.dynamicImportSettled()

    expect(setupService.getStatus).toHaveBeenCalled()
    expect(wrapper.text()).toContain('Configuration status')
    expect(wrapper.text()).toContain('Ready to publish')
  })

  it('shows a setup error when loading fails', async () => {
    vi.mocked(setupService.getPublicStatus).mockRejectedValue(new Error('offline'))

    const wrapper = mount(SetupView, {
      props: {
        publicMode: true,
      },
    })
    await vi.dynamicImportSettled()

    expect(wrapper.text()).toContain('Setup status could not be loaded.')
  })
})
