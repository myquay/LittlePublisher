export interface SetupStatus {
  ready: boolean
  missingRequiredCount: number
  warningCount: number
  groups: SetupGroup[]
}

export interface SetupGroup {
  name: string
  checks: SetupCheck[]
}

export interface SetupCheck {
  key: string
  description: string
  required: boolean
  secret: boolean
  configured: boolean
  warning: boolean
  displayValue: string | null
}
